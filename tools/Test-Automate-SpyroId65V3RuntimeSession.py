#!/usr/bin/env python3
"""Offline and non-posting native tests for the ID65 V3 input foundation."""

from __future__ import annotations

import contextlib
import dataclasses
import hashlib
import importlib.util
import io
import json
import os
import pathlib
import select
import shutil
import stat
import subprocess
import sys
import tempfile
import threading
import types
from collections.abc import Mapping, Sequence
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
        external_home_real_path="/pinned/home",
        allowed_process_cache_real_path=(
            "/pinned/home/Library/Application Support/DuckStation/"
            "cache/vulkan_shaders.bin"
        ),
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
        receipt_closure_sha256=automation.EXPECTED_RECEIPT_CLOSURE_SHA256,
        home_output_tree_manifest_sha256=(
            automation.EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256
        ),
        candidate_manifest_sha256=automation.EXPECTED_CANDIDATE_MANIFEST_SHA256,
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


def test_input_timing_static_provenance_cross_check() -> None:
    repository_root = pathlib.Path(__file__).resolve().parent.parent
    artifact_path = (
        repository_root / automation.INPUT_TIMING_STATIC_PROVENANCE_RELATIVE_PATH
    )
    raw = artifact_path.read_bytes()
    before_native_counts = (
        ACTUAL_DUCKSTATION_CALLS,
        ACTUAL_GDB_CONNECTION_ATTEMPTS,
        NATIVE_EVENT_POST_ATTEMPTS,
        PRODUCTION_EVENT_POSTS,
    )
    verified = automation.verify_checked_in_input_timing_static_provenance(
        repository_root
    )
    if (
        verified.sha256
        != automation.EXPECTED_INPUT_TIMING_STATIC_PROVENANCE_SHA256
        or verified.pad_read_packet != automation.GDB_PAD_READ_PACKET
        or verified.vsync_counter_read_packet
        != automation.GDB_VBLANK_READ_PACKET
        or verified.button_actions
        != (
            automation.KeyAction.PAD_R1_TOGGLE,
            automation.KeyAction.PAD_R2_TOGGLE,
            automation.KeyAction.PAD_L1_TOGGLE,
            automation.KeyAction.PAD_L2_TOGGLE,
            automation.KeyAction.PAD_LEFT_TOGGLE,
            automation.KeyAction.PAD_DOWN_TOGGLE,
        )
        or verified.false_scope_boundaries
        != (
            "live-process-use",
            "four-byte-pad-read-atomicity",
            "one-rendered-frame-cadence",
            "globally-monotonic-vblank-clock",
            "runtime-acceptance",
            "release-acceptance",
        )
    ):
        raise AssertionError("Checked-in static provenance projection changed.")

    def changed(
        mutate: Any,
    ) -> bytes:
        payload = json.loads(raw)
        mutate(payload)
        return automation.canonical_json(payload)

    expect_refusal(
        lambda: automation.parse_input_timing_static_provenance(
            raw.replace(b'"candidate":', b'"candidate" :', 1)
        ),
        "canonical",
    )
    duplicate = b'{"candidate":null,' + raw[1:]
    expect_refusal(
        lambda: automation.parse_input_timing_static_provenance(duplicate),
        "duplicate",
    )
    expect_refusal(
        lambda: automation.parse_input_timing_static_provenance(
            changed(lambda value: value["candidate"]["bin"].__setitem__(
                "sha256", "0" * 64
            ))
        ),
        "candidate bin sha256",
    )
    expect_refusal(
        lambda: automation.parse_input_timing_static_provenance(
            changed(lambda value: value["pad"].__setitem__(
                "rspReadPacket", "$m800786a0,4#00"
            ))
        ),
        "checksum",
    )
    expect_refusal(
        lambda: automation.parse_input_timing_static_provenance(
            changed(lambda value: value["vsyncCounter"].__setitem__(
                "rspReadPacket", "$m800758c8,4#00"
            ))
        ),
        "checksum",
    )
    expect_refusal(
        lambda: automation.parse_input_timing_static_provenance(
            changed(lambda value: value["pad"]["buttons"][0].__setitem__(
                "rawBit", 10
            ))
        ),
        "rawbit",
    )
    expect_refusal(
        lambda: automation.parse_input_timing_static_provenance(
            changed(lambda value: value["pad"]["buttons"][0].__setitem__(
                "actionSpecProfileBinding", ["Pad1", "Macro1", "Keyboard/9"]
            ))
        ),
        "actionspecprofilebinding",
    )
    expect_refusal(
        lambda: automation.parse_input_timing_static_provenance(
            changed(lambda value: value["scope"]["doesNotProve"].__setitem__(
                0, "live-process-use-proven"
            ))
        ),
        "false scope",
    )
    expect_refusal(
        lambda: automation.parse_input_timing_static_provenance(
            changed(lambda value: value["scope"]["doesNotProve"].__setitem__(
                1, "four-byte-pad-read-atomicity-proven"
            ))
        ),
        "false scope",
    )
    expect_refusal(
        lambda: automation.parse_input_timing_static_provenance(
            changed(lambda value: value["scope"]["doesNotProve"].__setitem__(
                3, "globally-monotonic-vblank-clock-proven"
            ))
        ),
        "false scope",
    )
    # This remains schema-valid static witness data, so only the independent
    # whole-artifact identity pin can reject it.
    expect_refusal(
        lambda: automation.parse_input_timing_static_provenance(
            changed(lambda value: value["witnesses"][0]["instructions"][0].__setitem__(
                "word", "0x3c048009"
            ))
        ),
        "artifact identity",
    )

    provenance_calls: list[pathlib.Path] = []
    plan_reads: list[pathlib.Path] = []
    original_provenance_verifier = (
        automation.verify_checked_in_input_timing_static_provenance
    )
    original_plan_reader = automation._read_plan

    def refusing_provenance(root: pathlib.Path) -> Any:
        provenance_calls.append(root)
        raise automation.AutomationRefusal("offline provenance sentinel")

    def forbidden_plan_read(path: pathlib.Path) -> Any:
        plan_reads.append(path)
        raise AssertionError("Plan read crossed refused provenance preflight.")

    automation.verify_checked_in_input_timing_static_provenance = refusing_provenance
    automation._read_plan = forbidden_plan_read
    stderr = io.StringIO()
    try:
        with contextlib.redirect_stderr(stderr):
            return_code = automation.main([
                "--preflight",
                "--repository-root",
                os.fspath(repository_root),
                "--pid",
                "700",
                "--input-plan",
                os.fspath(repository_root / "must-not-be-read.json"),
            ])
    finally:
        automation.verify_checked_in_input_timing_static_provenance = (
            original_provenance_verifier
        )
        automation._read_plan = original_plan_reader
    if (
        return_code != 2
        or provenance_calls != [repository_root]
        or plan_reads
        or "offline provenance sentinel" not in stderr.getvalue()
    ):
        raise AssertionError(
            "Production preflight did not refuse provenance before plan/native work."
        )
    if before_native_counts != (
        ACTUAL_DUCKSTATION_CALLS,
        ACTUAL_GDB_CONNECTION_ATTEMPTS,
        NATIVE_EVENT_POST_ATTEMPTS,
        PRODUCTION_EVENT_POSTS,
    ):
        raise AssertionError("Static provenance cross-check reached a native boundary.")


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


def test_pure_gdb_session_input_lifecycle_policy() -> None:
    paired = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        ("PAD_LEFT_TOGGLE", "HELD"),
        ("PAD_LEFT_TOGGLE", "RELEASED"),
        nonce="11112222333344445555666677778888",
    )
    lifecycle = automation.validate_gdb_session_input_lifecycle(paired)
    if lifecycle != automation.GdbSessionInputLifecycle(
        (
            automation.PlannedEmulationState.PAUSED,
            automation.PlannedEmulationState.RUNNING,
            automation.PlannedEmulationState.RUNNING,
            automation.PlannedEmulationState.RUNNING,
        ),
        2,
        automation.PlannedEmulationState.RUNNING,
    ):
        raise AssertionError("Pure TogglePause lifecycle derivation changed.")

    odd = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        nonce="99990000111122223333444455556666",
    )
    expect_refusal(
        lambda: automation.validate_gdb_session_input_lifecycle(odd),
        "pairs and terminate RUNNING",
    )
    input_while_paused = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        ("PAD_LEFT_TOGGLE", "HELD"),
        ("PAD_LEFT_TOGGLE", "RELEASED"),
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        nonce="abcdabcdabcdabcd1234123412341234",
    )
    expect_refusal(
        lambda: automation.validate_gdb_session_input_lifecycle(input_while_paused),
        "Only the matching TogglePause resume",
    )
    power_off = parse_plan(
        ("POWER_OFF", "NOT_APPLICABLE"),
        nonce="deadbeefdeadbeefcafebabecafebabe",
    )
    expect_refusal(
        lambda: automation.validate_gdb_session_input_lifecycle(power_off),
        "PowerOff is unavailable",
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


def gdb_connection_fields(
    *,
    descriptor: int,
    device: bytes,
    endpoint: bytes,
    state: bytes = b"TST=ESTABLISHED",
    receive_queue: bytes = b"TQR=0",
    send_queue: bytes = b"TQS=0",
    socket_type: bytes = b"tIPv4",
) -> tuple[bytes, ...]:
    return (
        f"f{descriptor}".encode("ascii"),
        b"au",
        socket_type,
        device,
        b"PTCP",
        endpoint,
        state,
        receive_queue,
        send_queue,
    )


def gdb_connection_set_raw(
    *,
    duckstation_pid: int = 700,
    client_pid: int | None = None,
    client_port: int = 54321,
    client_descriptor: int = 41,
    client_first: bool = False,
    server_changes: Mapping[str, object] | None = None,
    client_changes: Mapping[str, object] | None = None,
) -> bytes:
    listener = gdb_listener_fields()
    if client_pid is None:
        return gdb_lsof_records((f"p{duckstation_pid}".encode("ascii"),), listener)
    server_values: dict[str, object] = {
        "descriptor": 19,
        "device": b"d0xaaa1",
        "endpoint": f"n127.0.0.1:2345->127.0.0.1:{client_port}".encode("ascii"),
    }
    client_values: dict[str, object] = {
        "descriptor": client_descriptor,
        "device": b"d0xbbb2",
        "endpoint": f"n127.0.0.1:{client_port}->127.0.0.1:2345".encode("ascii"),
    }
    server_values.update(server_changes or {})
    client_values.update(client_changes or {})
    server = gdb_connection_fields(**server_values)
    client = gdb_connection_fields(**client_values)
    duck_records: tuple[Sequence[bytes], ...] = (
        (f"p{duckstation_pid}".encode("ascii"),),
        listener,
        server,
    )
    client_records: tuple[Sequence[bytes], ...] = (
        (f"p{client_pid}".encode("ascii"),),
        client,
    )
    return gdb_lsof_records(
        *(client_records + duck_records if client_first else duck_records + client_records)
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


def make_gdb_connection_set(
    listener: Any,
    client_endpoint: Any | None,
    **changes: object,
) -> Any:
    values: dict[str, object] = {
        "listener": listener,
        "client_endpoint": client_endpoint,
        "server_descriptor": None if client_endpoint is None else 19,
        "server_device_token": None if client_endpoint is None else "0xaaa1",
        "client_descriptor": (
            None if client_endpoint is None else client_endpoint.descriptor
        ),
        "client_device_token": None if client_endpoint is None else "0xbbb2",
    }
    values.update(changes)
    return automation.GdbObservedConnectionSet(**values)


def make_gdb_client_endpoint(**changes: object) -> Any:
    values: dict[str, object] = {
        "pid": os.getpid(),
        "descriptor": 41,
        "address": automation.GDB_HOST,
        "port": 54321,
    }
    values.update(changes)
    return automation.GdbClientEndpointIdentity(**values)


class FakeGdbEndpointAttestor:
    def __init__(
        self,
        listeners: Sequence[Any],
        *,
        connection_observer: Any | None = None,
    ) -> None:
        if not listeners:
            raise AssertionError("Fake GDB attestor requires an identity.")
        self.listeners = list(listeners)
        self.default_listener = self.listeners[0]
        self.processes: list[Any] = []
        self.connection_calls: list[tuple[Any, Any | None]] = []
        self.connection_observer = connection_observer

    def observe(self, process: Any) -> Any:
        self.processes.append(process)
        if len(self.listeners) == 1:
            return self.listeners[0]
        return self.listeners.pop(0)

    def observe_connection_set(
        self, process: Any, expected_client: Any | None
    ) -> Any:
        self.connection_calls.append((process, expected_client))
        if self.connection_observer is not None:
            return self.connection_observer(
                process, expected_client, len(self.connection_calls)
            )
        return make_gdb_connection_set(self.default_listener, expected_client)


class FakeFragmentedGdbSocket:
    def __init__(
        self,
        chunks: Sequence[bytes | BaseException],
        *,
        peer: object = ("127.0.0.1", 2345),
        local: object = ("127.0.0.1", 54321),
        descriptor: object = 41,
        fail_send_number: int | None = None,
        fail_close: bool = False,
    ) -> None:
        self.chunks = list(chunks)
        self.peer = peer
        self.local = local
        self.descriptor = descriptor
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

    def getsockname(self) -> object:
        return self.local

    def fileno(self) -> object:
        return self.descriptor

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
    local: object = ("127.0.0.1", 54321),
    descriptor: object = 41,
    fail_send_number: int | None = None,
    connector_error: BaseException | None = None,
) -> tuple[Any, FakeFragmentedGdbSocket, FakeGdbConnector, FakeGdbEndpointAttestor]:
    selected_process = process or make_process()
    fake_socket = FakeFragmentedGdbSocket(
        chunks,
        peer=peer,
        local=local,
        descriptor=descriptor,
        fail_send_number=fail_send_number,
    )
    connector = FakeGdbConnector(fake_socket, error=connector_error)
    attestor = FakeGdbEndpointAttestor(
        listeners or (make_gdb_listener(pid=selected_process.pid),)
    )
    reader = automation._PersistentDuckStationGdbTransport(
        observer or FakeObserver([selected_process]),
        attestor,
        connector=connector,
    )
    return reader, fake_socket, connector, attestor


def gdb_u32_reply(value: int) -> bytes:
    if type(value) is not int or not 0 <= value <= 0xFFFFFFFF:
        raise AssertionError("Offline VBlank fixture is not uint32.")
    return b"+" + gdb_rsp_packet(value.to_bytes(4, "little").hex().encode("ascii"))


def make_gdb_session(
    chunks: Sequence[bytes | BaseException],
    *,
    process: Any | None = None,
    observer: Any | None = None,
    listeners: Sequence[Any] | None = None,
    peer: object = ("127.0.0.1", 2345),
    local: object = ("127.0.0.1", 54321),
    descriptor: object = 41,
    fail_send_number: int | None = None,
    connector_error: BaseException | None = None,
    clock: Any | None = None,
    sleeper: Any | None = None,
    timeout_seconds: float = 0.1,
    interval_seconds: float = 0.01,
    paused_minimum_seconds: float = 0.015,
    maximum_samples: int = 8,
    connection_observer: Any | None = None,
) -> tuple[Any, FakeFragmentedGdbSocket, FakeGdbConnector, Any, Any]:
    selected_process = process or make_process()
    selected_clock = clock or FakeClock()
    fake_socket = FakeFragmentedGdbSocket(
        chunks,
        peer=peer,
        local=local,
        descriptor=descriptor,
        fail_send_number=fail_send_number,
    )
    connector = FakeGdbConnector(fake_socket, error=connector_error)
    attestor = FakeGdbEndpointAttestor(
        listeners or (make_gdb_listener(pid=selected_process.pid),),
        connection_observer=connection_observer,
    )
    session = automation.PersistentDuckStationGdbSession(
        observer or FakeObserver([selected_process]),
        attestor,
        connector=connector,
        clock=selected_clock.clock if clock is None else clock,
        sleeper=(
            selected_clock.sleep
            if sleeper is None and clock is None
            else (sleeper if sleeper is not None else (lambda _seconds: None))
        ),
        timeout_seconds=timeout_seconds,
        interval_seconds=interval_seconds,
        paused_minimum_seconds=paused_minimum_seconds,
        maximum_samples=maximum_samples,
    )
    return session, fake_socket, connector, attestor, selected_clock


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
        native_attestor = automation.MacGdbEndpointAttestor()
        expect_any_refusal(
            lambda: native_attestor.observe(native_process)
        )
        expect_any_refusal(
            lambda: native_attestor.observe_connection_set(native_process, None)
        )


def test_strict_gdb_connection_set_parser_and_attestor() -> None:
    literal_command = (
        "/usr/sbin/lsof",
        "-nP",
        "-Tqs",
        "-a",
        "-iTCP:2345",
        "-F0pfadDikstnPT",
    )
    if automation.GDB_CONNECTION_SET_LSOF_COMMAND != literal_command:
        raise AssertionError("Whole GDB topology lsof command changed.")
    process = make_process()
    client = make_gdb_client_endpoint()
    pre_raw = gdb_connection_set_raw()
    expected_pre = make_gdb_connection_set(make_gdb_listener(), None)
    if automation._parse_gdb_connection_set_lsof(pre_raw, 700, None) != expected_pre:
        raise AssertionError("Pre-connect GDB topology did not prove zero clients.")

    expected_post = make_gdb_connection_set(make_gdb_listener(), client)
    for client_first in (False, True):
        raw = gdb_connection_set_raw(
            client_pid=os.getpid(), client_first=client_first
        )
        observed = automation._parse_gdb_connection_set_lsof(raw, 700, client)
        if observed != expected_post:
            raise AssertionError("Exact GDB reversed endpoint pair changed by PID order.")

    queued = gdb_connection_set_raw(
        client_pid=os.getpid(),
        server_changes={"receive_queue": b"TQR=7", "send_queue": b"TQS=11"},
        client_changes={"receive_queue": b"TQR=13", "send_queue": b"TQS=17"},
    )
    if automation._parse_gdb_connection_set_lsof(queued, 700, client) != expected_post:
        raise AssertionError("Established queue variation changed socket identity.")

    extra_server = gdb_connection_fields(
        descriptor=20,
        device=b"d0xccc3",
        endpoint=b"n127.0.0.1:2345->127.0.0.1:60000",
    )
    extra_raw = gdb_lsof_records(
        (b"p700",),
        gdb_listener_fields(),
        gdb_connection_fields(
            descriptor=19,
            device=b"d0xaaa1",
            endpoint=b"n127.0.0.1:2345->127.0.0.1:54321",
        ),
        extra_server,
        (f"p{os.getpid()}".encode("ascii"),),
        gdb_connection_fields(
            descriptor=41,
            device=b"d0xbbb2",
            endpoint=b"n127.0.0.1:54321->127.0.0.1:2345",
        ),
    )
    invalid_post = (
        pre_raw,
        gdb_connection_set_raw(
            client_pid=os.getpid(), client_changes={"descriptor": 42}
        ),
        gdb_connection_set_raw(
            client_pid=os.getpid(), server_changes={"state": b"TST=CLOSE_WAIT"}
        ),
        gdb_connection_set_raw(
            client_pid=os.getpid(), client_changes={"socket_type": b"tIPv6"}
        ),
        gdb_connection_set_raw(
            client_pid=os.getpid(),
            server_changes={"endpoint": b"n127.0.0.1:2345->127.0.0.1:54322"},
        ),
        gdb_connection_set_raw(
            client_pid=os.getpid(), server_changes={"device": b"d0x2cb90ff55f15ccdc"}
        ),
        gdb_connection_set_raw(
            client_pid=os.getpid(), client_changes={"send_queue": b"TQS=-1"}
        ),
        extra_raw,
    )
    for index, raw in enumerate(invalid_post):
        refusal = expect_any_refusal(
            lambda raw=raw: automation._parse_gdb_connection_set_lsof(
                raw, 700, client
            )
        )
        if not refusal:
            raise AssertionError(f"Invalid GDB topology {index} had no diagnostic.")
    expect_refusal(
        lambda: automation._parse_gdb_connection_set_lsof(
            gdb_connection_set_raw(client_pid=os.getpid()), 700, None
        ),
        "already established",
    )

    calls: list[tuple[str, ...]] = []
    post_raw = gdb_connection_set_raw(client_pid=os.getpid())

    def runner(command: Sequence[str]) -> subprocess.CompletedProcess[bytes]:
        calls.append(tuple(command))
        return subprocess.CompletedProcess(tuple(command), 0, post_raw, b"")

    attestor = automation.MacGdbEndpointAttestor(runner=runner)
    if attestor.observe_connection_set(process, client) != expected_post:
        raise AssertionError("Native topology attestor changed the exact socket pair.")
    if calls != [automation.GDB_CONNECTION_SET_LSOF_COMMAND]:
        raise AssertionError("Topology attestor did not use its one global query.")

    def noisy_runner(command: Sequence[str]) -> subprocess.CompletedProcess[bytes]:
        return subprocess.CompletedProcess(tuple(command), 0, post_raw, b"warning")

    expect_refusal(
        lambda: automation.MacGdbEndpointAttestor(
            runner=noisy_runner
        ).observe_connection_set(process, client),
        "failed closed",
    )


def test_persistent_gdb_reader_fixed_packets_and_fragmentation() -> None:
    process = make_process()
    literal_pad_packet = b"$m800786a0,4#9b"
    literal_vblank_packet = b"$m800758c8,4#a4"
    if (
        automation.GDB_PAD_READ_PACKET != literal_pad_packet
        or automation.GDB_VBLANK_READ_PACKET != literal_vblank_packet
        or automation.GDB_READ_ONLY_OUTBOUND
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
    initial_status = reader.status()
    if initial_status != automation._GdbTransportStatus(
        "NEW", None, None, None, 0
    ):
        raise AssertionError("Fresh GDB reader status performed work or changed state.")
    if not callable(reader.require_existing_connection):
        raise AssertionError("Existing-connection guard is unavailable.")
    expect_refusal(
        lambda: reader.require_existing_connection(process),
        "existing connection",
    )
    if connector.calls or fake_socket.sent or attestor.processes:
        raise AssertionError("Existing-connection guard opened or inspected a NEW reader.")
    if reader.read_pad_word(process) != 0xFF7F:
        raise AssertionError("Digital-pad reply was not decoded little-endian.")
    connected_status = reader.status()
    if connected_status != automation._GdbTransportStatus(
        "CONNECTED",
        process,
        make_gdb_listener(pid=process.pid),
        make_gdb_client_endpoint(),
        1,
    ):
        raise AssertionError("Connected GDB reader status lost its one-attempt binding.")
    reader.require_existing_connection(process)
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
    if any(packet not in automation.GDB_READ_ONLY_OUTBOUND for packet in fake_socket.sent):
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
    if reader.status() != automation._GdbTransportStatus(
        "CLOSED",
        process,
        make_gdb_listener(pid=process.pid),
        make_gdb_client_endpoint(),
        1,
    ):
        raise AssertionError("Closed GDB reader status changed its attempt binding.")
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
    if setup_reader.status() != automation._GdbTransportStatus(
        "POISONED", None, None, None, 1
    ):
        raise AssertionError("Failed GDB connector did not consume its sole attempt.")

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
    if not callable(automation.PersistentDuckStationGdbSession):
        raise AssertionError("The reviewed one-shot GDB session is not public.")
    if (
        "PersistentDuckStationGdbSession" in main_names
        or "UnavailableNativePadConfirmer" not in main_names
    ):
        raise AssertionError(
            "Production main wired the persistent session before lifecycle review."
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

    malformed_locals: tuple[tuple[str, object, object], ...] = (
        ("wrong-host", ("127.0.0.2", 54321), 41),
        ("server-port", ("127.0.0.1", 2345), 41),
        ("zero-port", ("127.0.0.1", 0), 41),
        ("boolean-port", ("127.0.0.1", True), 41),
        ("text-port", ("127.0.0.1", "54321"), 41),
        ("list-not-tuple", ["127.0.0.1", 54321], 41),
        ("negative-fd", ("127.0.0.1", 54321), -1),
        ("boolean-fd", ("127.0.0.1", 54321), True),
        ("text-fd", ("127.0.0.1", 54321), "41"),
    )
    for name, local, descriptor in malformed_locals:
        local_reader, local_socket, local_connector, _ = make_gdb_reader(
            (), process=process, local=local, descriptor=descriptor
        )
        expect_any_refusal(lambda: local_reader.read_pad_word(process))
        if (
            len(local_connector.calls) != 1
            or local_socket.sent
            or local_socket.close_calls != 1
        ):
            raise AssertionError(
                f"Malformed GDB local endpoint {name} reached an RSP packet."
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


def test_one_shot_gdb_session_resume_and_shared_reads() -> None:
    process = make_process()
    literal_continue = b"$c#63"
    if (
        automation.GDB_SOURCE_COMMIT
        != "e7f2f101c2489e68446deff6dae9083e7a2114ea"
        or automation.GDB_CONTINUE_PACKET != literal_continue
        or automation.GDB_SESSION_OUTBOUND
        != frozenset({
            b"$m800786a0,4#9b",
            b"$m800758c8,4#a4",
            literal_continue,
            b"+",
            b"-",
        })
    ):
        raise AssertionError("Pinned DuckStation source or one-shot packet set changed.")

    # First read accepts the socket after DuckStation has paused.  Two more
    # equal ticks span the injected 15ms minimum.  Continue has one raw '+',
    # then one unchanged and one advancing tick prove RUNNING.  Pad/VBlank
    # reads after that remain on the same socket.
    transcript = b"".join((
        gdb_u32_reply(100),
        gdb_u32_reply(100),
        gdb_u32_reply(100),
        b"+",
        gdb_u32_reply(100),
        gdb_u32_reply(101),
        b"+" + gdb_rsp_packet(b"0041ffff"),
        gdb_u32_reply(102),
    ))
    session, fake_socket, connector, attestor, _ = make_gdb_session(
        [bytes((value,)) for value in transcript], process=process
    )
    initial = session.status()
    if initial != automation.GdbSessionStatus(
        "NEW", None, None, 0, 0, False, False
    ):
        raise AssertionError("Fresh GDB session status performed work.")
    if connector.calls or fake_socket.sent or attestor.processes:
        raise AssertionError("Fresh GDB session crossed a native boundary.")

    session.connect_resume_once(process)
    expected_listener = make_gdb_listener(pid=process.pid)
    if session.status() != automation.GdbSessionStatus(
        "RUNNING", process, expected_listener, 1, 1, True, True
    ):
        raise AssertionError("One-shot GDB session did not prove RUNNING exactly.")
    session.require_running(process)
    if session.read_pad_word(process) != 0xFFFF:
        raise AssertionError("RUNNING session did not share its pad read.")
    if session.read_vblank_tick(process) != 102:
        raise AssertionError("RUNNING session did not share its VBlank read.")

    read_packets = [
        automation.GDB_VBLANK_READ_PACKET,
        automation.GDB_VBLANK_READ_PACKET,
        automation.GDB_VBLANK_READ_PACKET,
        automation.GDB_CONTINUE_PACKET,
        automation.GDB_VBLANK_READ_PACKET,
        automation.GDB_VBLANK_READ_PACKET,
        automation.GDB_PAD_READ_PACKET,
        automation.GDB_VBLANK_READ_PACKET,
    ]
    sent_commands = [
        packet for packet in fake_socket.sent if packet not in {b"+", b"-"}
    ]
    if sent_commands != read_packets:
        raise AssertionError(f"One-shot command order changed: {sent_commands!r}")
    if fake_socket.sent.count(automation.GDB_CONTINUE_PACKET) != 1:
        raise AssertionError("One-shot GDB session did not send exactly one continue.")
    # The raw continue '+' is consumed and never ACKed.  Each of the seven
    # checksum-framed memory replies receives exactly one client ACK.
    if fake_socket.sent.count(b"+") != 7 or b"-" in fake_socket.sent:
        raise AssertionError("One-shot GDB acknowledgement ownership changed.")
    if len(connector.calls) != 1:
        raise AssertionError("Shared GDB session opened more than one connection.")
    if (
        len(attestor.connection_calls) != 18
        or attestor.connection_calls[0] != (process, None)
        or any(
            call != (process, make_gdb_client_endpoint())
            for call in attestor.connection_calls[1:]
        )
    ):
        raise AssertionError(
            "One-shot session skipped whole-set attestation around an RSP operation."
        )

    session.close()
    session.close()
    if session.status() != automation.GdbSessionStatus(
        "CLOSED", process, expected_listener, 1, 1, True, False
    ):
        raise AssertionError("RUNNING GDB session close lost its exact status.")
    if fake_socket.close_calls != 1:
        raise AssertionError("One-shot GDB session close is not idempotent.")
    sent_before = tuple(fake_socket.sent)
    expect_any_refusal(lambda: session.connect_resume_once(process))
    if tuple(fake_socket.sent) != sent_before or len(connector.calls) != 1:
        raise AssertionError("Closed one-shot GDB session retried control work.")


def test_gdb_runtime_session_sentinel_exact_and_refusals() -> None:
    process = make_process()
    connect_transcript = b"".join((
        gdb_u32_reply(100),
        gdb_u32_reply(100),
        gdb_u32_reply(100),
        b"+",
        gdb_u32_reply(100),
        gdb_u32_reply(101),
    ))
    session, fake_socket, connector, attestor, _ = make_gdb_session(
        [bytes((value,)) for value in connect_transcript], process=process
    )
    session.connect_resume_once(process)
    sent_before = tuple(fake_socket.sent)
    receives_before = tuple(fake_socket.recv_sizes)
    timeouts_before = tuple(fake_socket.timeouts)
    topology_before = len(attestor.connection_calls)
    expected_set = make_gdb_connection_set(
        make_gdb_listener(pid=process.pid), make_gdb_client_endpoint()
    )
    expected = automation.GdbRuntimeSessionSentinel(
        "RUNNING",
        process,
        make_gdb_listener(pid=process.pid),
        expected_set,
        1,
        1,
        True,
        True,
        None,
    )
    first = session.attest_running_owned(process)
    second = session.attest_running_owned(process)
    if first != expected or second != expected or first is second:
        raise AssertionError("RUNNING GDB sentinel was not exact and immutable-valued.")
    if (
        tuple(fake_socket.sent) != sent_before
        or tuple(fake_socket.recv_sizes) != receives_before
        or tuple(fake_socket.timeouts) != timeouts_before
        or len(connector.calls) != 1
        or len(attestor.connection_calls) - topology_before != 2
        or fake_socket.close_calls
    ):
        raise AssertionError("Runtime sentinel sent RSP bytes, read the socket, or reconnected.")
    try:
        first.state = "PAUSED_BY_PLAN"
    except dataclasses.FrozenInstanceError:
        pass
    else:
        raise AssertionError("GDB runtime sentinel was mutable.")

    class RunningText(str):
        pass

    class RunningEqual:
        def __eq__(self, other: object) -> bool:
            return other == "RUNNING"

    for listener_changes in (
        {"pid": 2_147_483_648},
        {"descriptor": 2_147_483_648},
        {"access": RunningText("u")},
        {"socket_type": RunningText("IPv4")},
        {"device_token": RunningText("0x2cb90ff55f15ccdc")},
        {"protocol": RunningText("TCP")},
        {"endpoint": RunningText("127.0.0.1:2345")},
        {"tcp_state": RunningText("LISTEN")},
        {"device_token": "0x" + ("a" * 17)},
    ):
        expect_any_refusal(
            lambda changes=listener_changes: dataclasses.replace(
                make_gdb_listener(), **changes
            )
        )
    expect_any_refusal(
        lambda: dataclasses.replace(
            make_gdb_client_endpoint(),
            address=RunningText(automation.GDB_HOST),
        )
    )
    expect_any_refusal(
        lambda: dataclasses.replace(
            expected_set,
            server_device_token=RunningText("0xaaa1"),
        )
    )
    expect_any_refusal(
        lambda: dataclasses.replace(
            expected_set,
            client_device_token=RunningText("0xbbb2"),
        )
    )
    expect_any_refusal(
        lambda: dataclasses.replace(
            expected_set,
            server_device_token="0x" + ("a" * 17),
        )
    )

    malformed = (
        {"state": "PAUSED_BY_PLAN"},
        {"state": RunningText("RUNNING")},
        {"state": RunningEqual()},
        {"bound_listener": make_gdb_listener(pid=701)},
        {
            "observed_connection_set": make_gdb_connection_set(
                make_gdb_listener(pid=process.pid), None
            )
        },
        {"connection_attempts": 0},
        {"connection_attempts": 2},
        {"continue_attempts": 0},
        {"continue_attempts": 2},
        {"pause_state_certain": False},
        {"single_client_observed": False},
        {"planned_paused_tick": 100},
    )
    for changes in malformed:
        expect_any_refusal(lambda changes=changes: dataclasses.replace(first, **changes))

    session._operation_lock.acquire()
    try:
        expect_refusal(
            lambda: session.attest_running_owned(process),
            "Concurrent DuckStation GDB runtime attestation",
        )
    finally:
        session._operation_lock.release()
    if session.status().state != "RUNNING" or fake_socket.close_calls:
        raise AssertionError("Contended read-only sentinel poisoned its RUNNING owner.")

    new_session, new_socket, new_connector, new_attestor, _ = make_gdb_session(
        (), process=process
    )
    expect_refusal(
        lambda: new_session.attest_running_owned(process),
        "requires exact RUNNING ownership",
    )
    if (
        new_session.status().state != "NEW"
        or new_connector.calls
        or new_attestor.connection_calls
        or new_socket.sent
        or new_socket.recv_sizes
        or new_socket.close_calls
    ):
        raise AssertionError("NEW sentinel refusal changed state or crossed a boundary.")

    armed_session, armed_socket, armed_connector, armed_attestor, _ = make_gdb_session(
        [bytes((value,)) for value in connect_transcript], process=process
    )
    armed_session.connect_resume_once(process)
    armed_session.arm_planned_pause(process)
    armed_sent = tuple(armed_socket.sent)
    armed_receives = tuple(armed_socket.recv_sizes)
    armed_topology_calls = len(armed_attestor.connection_calls)
    expect_refusal(
        lambda: armed_session.attest_running_owned(process),
        "requires exact RUNNING ownership",
    )
    if (
        armed_session.status().state != "EXPECTING_PLAN_PAUSE"
        or tuple(armed_socket.sent) != armed_sent
        or tuple(armed_socket.recv_sizes) != armed_receives
        or len(armed_connector.calls) != 1
        or len(armed_attestor.connection_calls) != armed_topology_calls
        or armed_socket.close_calls
    ):
        raise AssertionError("Armed sentinel refusal destroyed its recovery state.")

    pause_transcript = connect_transcript + b"".join((
        b"+" + gdb_rsp_packet(b"S05"),
        gdb_u32_reply(200),
        gdb_u32_reply(200),
        gdb_u32_reply(200),
        b"+",
        gdb_u32_reply(201),
    ))
    paused_session, paused_socket, paused_connector, paused_attestor, _ = make_gdb_session(
        [bytes((value,)) for value in pause_transcript], process=process
    )
    paused_session.connect_resume_once(process)
    paused_session.arm_planned_pause(process)
    paused_session.confirm_planned_pause(process)
    paused_sent = tuple(paused_socket.sent)
    paused_receives = tuple(paused_socket.recv_sizes)
    paused_topology_calls = len(paused_attestor.connection_calls)
    expect_refusal(
        lambda: paused_session.attest_running_owned(process),
        "requires exact RUNNING ownership",
    )
    if (
        paused_session.status().state != "PAUSED_BY_PLAN"
        or tuple(paused_socket.sent) != paused_sent
        or tuple(paused_socket.recv_sizes) != paused_receives
        or len(paused_connector.calls) != 1
        or len(paused_attestor.connection_calls) != paused_topology_calls
        or paused_socket.close_calls
    ):
        raise AssertionError("PAUSED sentinel refusal destroyed exact pause state.")
    paused_session.arm_planned_resume(process)
    resume_armed_sent = tuple(paused_socket.sent)
    resume_armed_receives = tuple(paused_socket.recv_sizes)
    resume_armed_topology_calls = len(paused_attestor.connection_calls)
    expect_refusal(
        lambda: paused_session.attest_running_owned(process),
        "requires exact RUNNING ownership",
    )
    if (
        paused_session.status().state != "EXPECTING_PLAN_RESUME"
        or tuple(paused_socket.sent) != resume_armed_sent
        or tuple(paused_socket.recv_sizes) != resume_armed_receives
        or len(paused_attestor.connection_calls) != resume_armed_topology_calls
        or paused_socket.close_calls
    ):
        raise AssertionError("Resume-armed sentinel refusal destroyed recovery state.")
    paused_session.confirm_planned_resume(process)
    resumed_sent = tuple(paused_socket.sent)
    resumed = paused_session.attest_running_owned(process)
    if (
        resumed.state != "RUNNING"
        or paused_session.status().state != "RUNNING"
        or tuple(paused_socket.sent) != resumed_sent
    ):
        raise AssertionError("Preserved resume recovery did not return to RUNNING.")
    paused_session.close()

    topology_gate = {"drift": False}

    def sentinel_topology(
        observed_process: Any, expected_client: Any | None, _call: int
    ) -> Any:
        changes: dict[str, object] = {}
        if topology_gate["drift"] and expected_client is not None:
            changes["server_device_token"] = "0xcccc"
        return make_gdb_connection_set(
            make_gdb_listener(pid=observed_process.pid),
            expected_client,
            **changes,
        )

    drift_session, drift_socket, drift_connector, _, _ = make_gdb_session(
        [bytes((value,)) for value in connect_transcript],
        process=process,
        connection_observer=sentinel_topology,
    )
    drift_session.connect_resume_once(process)
    topology_gate["drift"] = True
    drift_sent = tuple(drift_socket.sent)
    drift_receives = tuple(drift_socket.recv_sizes)
    expect_any_refusal(lambda: drift_session.attest_running_owned(process))
    if (
        drift_session.status().state != "POISONED"
        or drift_session.status().pause_state_certain
        or tuple(drift_socket.sent) != drift_sent
        or tuple(drift_socket.recv_sizes) != drift_receives
        or len(drift_connector.calls) != 1
        or drift_socket.close_calls != 1
    ):
        raise AssertionError("Topology-drift sentinel sent RSP bytes or stayed reusable.")
    poisoned_sent = tuple(drift_socket.sent)
    expect_refusal(
        lambda: drift_session.attest_running_owned(process),
        "requires exact RUNNING ownership",
    )
    if (
        drift_session.status().state != "POISONED"
        or tuple(drift_socket.sent) != poisoned_sent
        or drift_socket.close_calls != 1
    ):
        raise AssertionError("POISONED sentinel refusal retried or changed state.")

    changed_process = dataclasses.replace(process, start_microseconds=654321)
    process_session, process_socket, _, process_attestor, _ = make_gdb_session(
        [bytes((value,)) for value in connect_transcript], process=process
    )
    process_session.connect_resume_once(process)
    process_sent = tuple(process_socket.sent)
    process_topology_calls = len(process_attestor.connection_calls)
    expect_refusal(
        lambda: process_session.attest_running_owned(object()),
        "process is malformed",
    )
    expect_refusal(
        lambda: process_session.attest_running_owned(changed_process),
        "requires exact RUNNING ownership",
    )
    if (
        process_session.status().state != "RUNNING"
        or tuple(process_socket.sent) != process_sent
        or process_socket.close_calls
        or len(process_attestor.connection_calls) != process_topology_calls
    ):
        raise AssertionError("Process-rebind sentinel performed I/O or poisoned RUNNING.")
    process_session.close()
    session.close()
    closed_sent = tuple(fake_socket.sent)
    expect_refusal(
        lambda: session.attest_running_owned(process),
        "requires exact RUNNING ownership",
    )
    if (
        session.status().state != "CLOSED"
        or tuple(fake_socket.sent) != closed_sent
        or fake_socket.close_calls != 1
    ):
        raise AssertionError("CLOSED sentinel refusal retried or changed state.")

    poison_gate = {"active": False}
    poison_entered = threading.Event()
    poison_continue = threading.Event()

    def blocking_sentinel_topology(
        observed_process: Any, expected_client: Any | None, _call: int
    ) -> Any:
        if poison_gate["active"] and expected_client is not None:
            poison_entered.set()
            if not poison_continue.wait(timeout=1):
                raise AssertionError("Sentinel poison-race fixture timed out.")
        return make_gdb_connection_set(
            make_gdb_listener(pid=observed_process.pid), expected_client
        )

    race_session, race_socket, race_connector, _, _ = make_gdb_session(
        [bytes((value,)) for value in connect_transcript],
        process=process,
        connection_observer=blocking_sentinel_topology,
    )
    race_session.connect_resume_once(process)
    race_sent = tuple(race_socket.sent)
    race_results: list[Any] = []
    race_errors: list[BaseException] = []

    def emit_during_poison() -> None:
        try:
            race_results.append(race_session.attest_running_owned(process))
        except BaseException as exc:
            race_errors.append(exc)

    poison_gate["active"] = True
    race_thread = threading.Thread(target=emit_during_poison, daemon=True)
    race_thread.start()
    if not poison_entered.wait(timeout=1):
        raise AssertionError("Sentinel poison race did not reach topology attestation.")
    expect_refusal(
        lambda: race_session.require_running_owned(process),
        "Concurrent DuckStation GDB ownership check",
    )
    poison_continue.set()
    race_thread.join(timeout=1)
    if (
        race_thread.is_alive()
        or race_results
        or len(race_errors) != 1
        or not isinstance(race_errors[0], automation.AutomationRefusal)
        or race_session.status().state != "POISONED"
        or tuple(race_socket.sent) != race_sent
        or len(race_connector.calls) != 1
        or race_socket.close_calls != 1
    ):
        raise AssertionError("Concurrent transport poison escaped as a RUNNING sentinel.")

    main_names = frozenset(automation.main.__code__.co_names)
    foundation_names = frozenset(
        automation.UnattendedFoundation.run.__code__.co_names
    ) | frozenset(
        automation.UnattendedFoundation._run_with_coordinator_lease.__code__.co_names
    ) | frozenset(automation.UnattendedFoundation._execute.__code__.co_names)
    coordinator_names = frozenset(
        automation.GdbInputPulseLifecycleCoordinator.__init__.__code__.co_names
    ) | frozenset(
        automation.GdbInputPulseLifecycleCoordinator.start_execute.__code__.co_names
    ) | frozenset(
        automation.GdbInputPulseLifecycleCoordinator.execute_next.__code__.co_names
    )
    if (
        "GdbRuntimeSessionSentinel" in main_names
        or "attest_running_owned" in main_names
        or "attest_running_owned" in foundation_names
        or "attest_running_owned" in coordinator_names
    ):
        raise AssertionError("Production main crossed the offline GDB sentinel gate.")


def test_exact_runtime_session_attestor_and_disc_binding() -> None:
    def exact_identity(
        relative_path: str,
        digest: str,
        byte_length: int,
        mode: int,
        inode: int,
    ) -> Any:
        return automation.FileIdentity(
            real_path=os.fspath(
                automation.AUTOMATION_REPOSITORY_ROOT / relative_path
            ),
            device=8,
            inode=inode,
            byte_length=byte_length,
            owner_uid=501,
            mode=mode,
            link_count=1,
            sha256=digest,
        )

    profile = make_profile()
    profile = dataclasses.replace(
        profile,
        executable=exact_identity(
            automation.DUCKSTATION_EXECUTABLE_RELATIVE_PATH,
            automation.EXPECTED_DUCKSTATION_SHA256,
            automation.EXPECTED_EXECUTABLE_SIZE,
            0o755,
            9,
        ),
        settings=exact_identity(
            automation.SETTINGS_RELATIVE_PATH,
            automation.EXPECTED_SETTINGS_SHA256,
            automation.EXPECTED_SETTINGS_SIZE,
            0o600,
            10,
        ),
        candidate_cue=exact_identity(
            automation.CANDIDATE_CUE_RELATIVE_PATH,
            automation.EXPECTED_CANDIDATE_CUE_SHA256,
            automation.EXPECTED_CANDIDATE_CUE_SIZE,
            0o644,
            11,
        ),
        candidate_bin=exact_identity(
            automation.CANDIDATE_BIN_RELATIVE_PATH,
            automation.EXPECTED_CANDIDATE_BIN_SHA256,
            automation.EXPECTED_CANDIDATE_BIN_SIZE,
            0o644,
            12,
        ),
        external_home_real_path=os.fspath(
            automation.AUTOMATION_REPOSITORY_ROOT
            / automation.EXTERNAL_HOME_RELATIVE_PATH
        ),
        allowed_process_cache_real_path=os.fspath(
            automation.AUTOMATION_REPOSITORY_ROOT
            / automation.ALLOWED_PROCESS_CACHE_PATH
        ),
    )
    process = make_process(executable=profile.executable)

    def nul_sha256(values: Sequence[str]) -> str:
        return hashlib.sha256(
            ("\0".join(values) + "\0").encode("utf-8", "strict")
        ).hexdigest()

    def opened(
        identity: Any,
        fd: str,
        **changes: object,
    ) -> Any:
        values: dict[str, object] = {
            "fd": fd,
            "device": identity.device,
            "inode": identity.inode,
            "byte_length": identity.byte_length,
            "link_count": identity.link_count,
            "real_path": identity.real_path,
        }
        values.update(changes)
        return automation.RuntimeSessionOpenFileObservation(**values)

    default_argv = (
        profile.executable.real_path,
        profile.candidate_cue.real_path,
    )
    default_environment = automation.runtime_environment_assignments(
        profile.external_home_real_path
    )

    def observation(
        *,
        observed_process: Any = process,
        argv: Sequence[str] = default_argv,
        argv_sha256: str | None = None,
        environment: Sequence[str] = default_environment,
        environment_sha256: str | None = None,
        external_home: str = profile.external_home_real_path,
        open_files: Sequence[Any] | None = None,
        current_cue: Any = profile.candidate_cue,
        current_bin: Any = profile.candidate_bin,
        current_cache: Any | None = None,
    ) -> Any:
        argv_tuple = tuple(argv)
        environment_tuple = tuple(environment)
        files = tuple(open_files) if open_files is not None else (
            opened(profile.candidate_bin, "17r"),
        )
        return automation.RuntimeSessionObservation(
            process=observed_process,
            argv=argv_tuple,
            argv_nul_sha256=(
                argv_sha256
                if argv_sha256 is not None
                else nul_sha256(argv_tuple)
            ),
            environment=environment_tuple,
            environment_nul_sha256=(
                environment_sha256
                if environment_sha256 is not None
                else nul_sha256(environment_tuple)
            ),
            external_home_real_path=external_home,
            open_files=tuple(
                sorted(files, key=lambda item: (item.real_path, item.fd))
            ),
            current_candidate_cue=current_cue,
            current_candidate_bin=current_bin,
            current_allowed_process_cache=current_cache,
        )

    class SequenceRuntimeObserver:
        def __init__(self, values: Sequence[Any]) -> None:
            self.values = list(values)
            self.calls: list[tuple[Any, Any]] = []

        def observe(self, observed_profile: Any, observed_process: Any) -> Any:
            self.calls.append((observed_profile, observed_process))
            if not self.values:
                raise AssertionError("Runtime-observation fixture was exhausted.")
            return self.values.pop(0)

    base = observation()
    binding = automation.validate_runtime_session_observation(
        profile, process, base
    )
    if (
        binding.process != process
        or binding.argv != default_argv
        or binding.environment != default_environment
        or binding.candidate_cue != profile.candidate_cue
        or binding.candidate_bin != profile.candidate_bin
        or binding.candidate_bin_descriptor.fd != "17r"
        or binding.allowed_process_cache is not None
    ):
        raise AssertionError("Exact runtime process/disc binding was incomplete.")

    connect_transcript = b"".join((
        gdb_u32_reply(100),
        gdb_u32_reply(100),
        gdb_u32_reply(100),
        b"+",
        gdb_u32_reply(100),
        gdb_u32_reply(101),
    ))
    session, fake_socket, connector, gdb_attestor, _ = make_gdb_session(
        [bytes((value,)) for value in connect_transcript], process=process
    )
    session.connect_resume_once(process)
    sent_before = tuple(fake_socket.sent)
    receives_before = tuple(fake_socket.recv_sizes)
    timeouts_before = tuple(fake_socket.timeouts)
    topology_before = len(gdb_attestor.connection_calls)
    runtime_observer = SequenceRuntimeObserver((base,) * 6)
    exact = automation.ExactRuntimeSessionAttestor(runtime_observer, session)
    readiness = exact.readiness()
    if readiness != automation.RuntimeSessionAttestationReadiness(
        automation.EXACT_RUNTIME_SESSION_ATTESTOR_ID, True, None
    ):
        raise AssertionError("Exact runtime-session readiness was not exact.")
    class AttestorText(str):
        pass

    for malformed_readiness in (
        (123, True, None),
        (object(), False, object()),
        (AttestorText("exact-runtime-session-attestor-v1"), True, None),
        ("Uppercase", True, None),
        ("offline", 1, None),
        ("offline", True, "unexpected"),
        ("offline", False, None),
        ("offline", False, object()),
        ("offline", False, " leading"),
        ("offline", False, "line\nbreak"),
    ):
        expect_any_refusal(
            lambda values=malformed_readiness: (
                automation.RuntimeSessionAttestationReadiness(*values)
            )
        )
    first = exact.attest_exact(profile, process)
    second = exact.attest_exact(profile, process)
    if exact.attest(profile, process) is not None:
        raise AssertionError("RuntimeSessionAttestor compatibility leaked a witness.")
    if (
        type(first) is not automation.RuntimeSessionAttestation
        or first != second
        or first is second
        or first.process_binding != binding
        or first.gdb.bound_process != process
        or first.attestor_id != automation.EXACT_RUNTIME_SESSION_ATTESTOR_ID
        or first.profile_id != automation.PROFILE_ID
        or tuple(fake_socket.sent) != sent_before
        or tuple(fake_socket.recv_sizes) != receives_before
        or tuple(fake_socket.timeouts) != timeouts_before
        or len(connector.calls) != 1
        or len(gdb_attestor.connection_calls) - topology_before != 6
        or fake_socket.close_calls
        or runtime_observer.calls != [(profile, process)] * 6
    ):
        raise AssertionError(
            "Exact runtime attestor sent RSP work, reconnected, or tore its binding."
        )
    serialized = first.serialize()
    payload = json.loads(serialized)
    if (
        serialized != second.serialize()
        or first.canonical_sha256 != hashlib.sha256(serialized).hexdigest()
        or payload["format"] != automation.RUNTIME_SESSION_ATTESTATION_FORMAT
        or payload["formatVersion"]
        != automation.RUNTIME_SESSION_ATTESTATION_FORMAT_VERSION
        or payload["allowedProcessCache"] is not None
        or payload["disc"]["candidateBinDescriptor"]["fd"] != "17r"
        or payload["gdb"]["state"] != "RUNNING"
        or payload["gdb"]["clientEndpoint"]["pid"] != os.getpid()
        or payload["profile"]["externalHomeSettings"]["sha256"]
        != automation.EXPECTED_SETTINGS_SHA256
        or payload["profile"]["handoffReceiptSha256"]
        != automation.EXPECTED_HANDOFF_RECEIPT_SHA256
        or payload["profile"]["receiptClosureSha256"]
        != automation.EXPECTED_RECEIPT_CLOSURE_SHA256
        or payload["profile"]["homeOutputTreeManifestSha256"]
        != automation.EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256
        or payload["profile"]["candidateManifestSha256"]
        != automation.EXPECTED_CANDIDATE_MANIFEST_SHA256
        or payload["profile"]["enabledActions"]
        != sorted(action.value for action in profile.enabled_actions)
        or b"capturedAt" in serialized
        or b"nonce" in serialized.lower()
        or not serialized.endswith(b"\n")
    ):
        raise AssertionError("Exact runtime attestation serialization drifted.")
    try:
        first.profile_id = "changed"
    except dataclasses.FrozenInstanceError:
        pass
    else:
        raise AssertionError("Exact runtime attestation was mutable.")
    try:
        automation.RuntimeSessionAttestation(
            first.attestor_id,
            first.profile,
            first.process_binding,
            first.gdb,
            FORMAT="spoofed",
        )
    except TypeError:
        pass
    else:
        raise AssertionError("Runtime attestation format constant became an input field.")
    expect_any_refusal(
        lambda: dataclasses.replace(first, attestor_id="lookalike-attestor")
    )
    expect_any_refusal(
        lambda: dataclasses.replace(
            first,
            profile=dataclasses.replace(first.profile, profile_id="other-profile"),
        )
    )

    irrelevant_before = observation(
        open_files=(
            opened(profile.candidate_bin, "17r"),
            automation.RuntimeSessionOpenFileObservation(
                "9w", 8, 80, 12, 1, "/private/tmp/runtime-log.txt"
            ),
        )
    )
    irrelevant_after = observation(
        open_files=(
            opened(profile.candidate_bin, "17r"),
            automation.RuntimeSessionOpenFileObservation(
                "10r", 8, 81, 99, 1, "/private/tmp/unrelated.dat"
            ),
        )
    )
    irrelevant_observer = SequenceRuntimeObserver(
        (irrelevant_before, irrelevant_after)
    )
    irrelevant_attestation = automation.ExactRuntimeSessionAttestor(
        irrelevant_observer, session
    ).attest_exact(profile, process)
    if irrelevant_attestation.process_binding != binding:
        raise AssertionError("Unrelated non-disc descriptor churn caused false drift.")

    cache = automation.FileIdentity(
        real_path=profile.allowed_process_cache_real_path,
        device=8,
        inode=13,
        byte_length=65_536,
        owner_uid=profile.effective_uid,
        mode=0o644,
        link_count=1,
        sha256="4" * 64,
    )
    with_cache = observation(
        open_files=(
            opened(profile.candidate_bin, "17r"),
            opened(cache, "21u"),
        ),
        current_cache=cache,
    )
    cache_attestation = automation.ExactRuntimeSessionAttestor(
        SequenceRuntimeObserver((with_cache, with_cache)), session
    ).attest_exact(profile, process)
    if (
        cache_attestation.process_binding.allowed_process_cache != cache
        or cache_attestation.to_payload()["allowedProcessCache"]["descriptor"]["fd"]
        != "21u"
    ):
        raise AssertionError("Exact code-owned process cache was not bound.")

    other_process = dataclasses.replace(process, start_microseconds=654321)
    changed_cue = dataclasses.replace(profile.candidate_cue, sha256="a" * 64)
    changed_bin = dataclasses.replace(profile.candidate_bin, sha256="b" * 64)
    alternate_media = automation.RuntimeSessionOpenFileObservation(
        "30r", 8, 90, 4096, 1, "/pinned/alternate.iso"
    )
    invalid_observations = (
        ("process", observation(observed_process=other_process)),
        (
            "argv",
            observation(
                argv=(profile.executable.real_path, "/pinned/other.cue")
            ),
        ),
        ("argv-hash", observation(argv_sha256="f" * 64)),
        (
            "environment",
            observation(environment=(*default_environment, "EXTRA=1")),
        ),
        ("environment-hash", observation(environment_sha256="f" * 64)),
        ("home", observation(external_home="/pinned/other-home")),
        ("current-cue", observation(current_cue=changed_cue)),
        ("current-bin", observation(current_bin=changed_bin)),
        ("missing-bin", observation(open_files=())),
        (
            "duplicate-bin",
            observation(
                open_files=(
                    opened(profile.candidate_bin, "17r"),
                    opened(profile.candidate_bin, "18r"),
                )
            ),
        ),
        (
            "writable-bin",
            observation(open_files=(opened(profile.candidate_bin, "17u"),)),
        ),
        (
            "bin-device",
            observation(
                open_files=(opened(profile.candidate_bin, "17r", device=99),)
            ),
        ),
        (
            "bin-inode",
            observation(
                open_files=(opened(profile.candidate_bin, "17r", inode=99),)
            ),
        ),
        (
            "bin-size",
            observation(
                open_files=(
                    opened(profile.candidate_bin, "17r", byte_length=1),
                )
            ),
        ),
        (
            "bin-link",
            observation(
                open_files=(opened(profile.candidate_bin, "17r", link_count=2),)
            ),
        ),
        (
            "cue-open",
            observation(
                open_files=(
                    opened(profile.candidate_bin, "17r"),
                    opened(profile.candidate_cue, "18r"),
                )
            ),
        ),
        (
            "alternate-media",
            observation(
                open_files=(
                    opened(profile.candidate_bin, "17r"),
                    alternate_media,
                )
            ),
        ),
        (
            "cache-identity-only",
            observation(current_cache=cache),
        ),
        (
            "cache-descriptor-only",
            observation(
                open_files=(
                    opened(profile.candidate_bin, "17r"),
                    opened(cache, "21u"),
                )
            ),
        ),
        (
            "duplicate-cache",
            observation(
                open_files=(
                    opened(profile.candidate_bin, "17r"),
                    opened(cache, "21u"),
                    opened(cache, "22u"),
                ),
                current_cache=cache,
            ),
        ),
        (
            "cache-owner",
            observation(
                open_files=(
                    opened(profile.candidate_bin, "17r"),
                    opened(dataclasses.replace(cache, owner_uid=502), "21u"),
                ),
                current_cache=dataclasses.replace(cache, owner_uid=502),
            ),
        ),
        (
            "cache-mode",
            observation(
                open_files=(
                    opened(profile.candidate_bin, "17r"),
                    opened(dataclasses.replace(cache, mode=0o666), "21u"),
                ),
                current_cache=dataclasses.replace(cache, mode=0o666),
            ),
        ),
        (
            "cache-link",
            observation(
                open_files=(
                    opened(profile.candidate_bin, "17r"),
                    opened(dataclasses.replace(cache, link_count=2), "21u"),
                ),
                current_cache=dataclasses.replace(cache, link_count=2),
            ),
        ),
        (
            "cache-size",
            observation(
                open_files=(
                    opened(profile.candidate_bin, "17r"),
                    opened(
                        dataclasses.replace(
                            cache,
                            byte_length=automation.MAX_PROCESS_CACHE_BIN_BYTES + 1,
                        ),
                        "21u",
                    ),
                ),
                current_cache=dataclasses.replace(
                    cache,
                    byte_length=automation.MAX_PROCESS_CACHE_BIN_BYTES + 1,
                ),
            ),
        ),
        (
            "cache-access",
            observation(
                open_files=(
                    opened(profile.candidate_bin, "17r"),
                    opened(cache, "21r"),
                ),
                current_cache=cache,
            ),
        ),
        (
            "cache-descriptor-identity",
            observation(
                open_files=(
                    opened(profile.candidate_bin, "17r"),
                    opened(cache, "21u", inode=999),
                ),
                current_cache=cache,
            ),
        ),
    )
    for label, invalid in invalid_observations:
        try:
            automation.validate_runtime_session_observation(
                profile, process, invalid
            )
        except automation.AutomationRefusal:
            pass
        else:
            raise AssertionError(f"Runtime observation accepted {label} drift.")
    bundled_media_suffixes = (
        ".bin", ".img", ".iso", ".cue", ".chd", ".cpe", ".ecm",
        ".mds", ".pbp", ".ccd", ".elf", ".exe", ".psexe",
        ".ps-exe", ".psx", ".psf", ".minipsf", ".m3u", ".psxgpu",
        ".psxgpu.zst", ".psxgpu.xz",
    )
    if not set(bundled_media_suffixes).issubset(
        automation.RUNTIME_MEDIA_SOURCE_SUFFIXES
    ):
        raise AssertionError("Bundled DuckStation media suffix coverage regressed.")
    for index, suffix in enumerate((
        ".cpe", ".elf", ".exe", ".psx", ".psxgpu",
        ".psxgpu.zst", ".psxgpu.xz",
    )):
        alternate = automation.RuntimeSessionOpenFileObservation(
            f"{40 + index}r",
            8,
            100 + index,
            4096,
            1,
            f"/pinned/alternate{suffix}",
        )
        expect_any_refusal(
            lambda alternate=alternate: automation.validate_runtime_session_observation(
                profile,
                process,
                observation(
                    open_files=(
                        opened(profile.candidate_bin, "17r"),
                        alternate,
                    )
                ),
            )
        )
    expect_any_refusal(
        lambda: observation(
            open_files=(
                opened(profile.candidate_bin, "17r"),
                opened(cache, "17u"),
            ),
            current_cache=cache,
        )
    )
    expect_any_refusal(
        lambda: dataclasses.replace(
            binding,
            allowed_process_cache=cache,
            allowed_process_cache_descriptor=opened(cache, "17u"),
        )
    )
    expect_any_refusal(
        lambda: automation.RuntimeSessionOpenFileObservation(
            "2147483648r", 8, 91, 1, 1, "/pinned/too-large.bin"
        )
    )
    expect_any_refusal(
        lambda: automation.RuntimeSessionOpenFileObservation(
            ("9" * 5_000) + "r", 8, 92, 1, 1, "/pinned/absurd-fd.bin"
        )
    )
    for numeric_changes in (
        {"device": automation.RUNTIME_ATTESTATION_MAX_SAFE_INTEGER + 1},
        {"inode": automation.RUNTIME_ATTESTATION_MAX_SAFE_INTEGER + 1},
        {"byte_length": automation.RUNTIME_ATTESTATION_MAX_SAFE_INTEGER + 1},
        {"link_count": automation.NATIVE_LINK_COUNT_MAX + 1},
    ):
        expect_any_refusal(
            lambda changes=numeric_changes: dataclasses.replace(
                opened(profile.candidate_bin, "17r"), **changes
            )
        )
    expect_any_refusal(
        lambda: automation.RuntimeSessionOpenFileObservation(
            "93r",
            8,
            93,
            1,
            1,
            "/pinned/" + ("a" * automation.RUNTIME_ATTESTATION_MAX_PATH_BYTES),
        )
    )

    forged_binding = dataclasses.replace(
        binding,
        candidate_bin=dataclasses.replace(
            binding.candidate_bin, sha256="f" * 64
        ),
    )
    expect_any_refusal(
        lambda: dataclasses.replace(first, process_binding=forged_binding)
    )

    class DigestText(str):
        pass

    class ClosureEqual:
        def __eq__(self, other: object) -> bool:
            return other == automation.EXPECTED_HANDOFF_RECEIPT_SHA256

    class ActionEqual:
        def __hash__(self) -> int:
            return hash(automation.KeyAction.PAD_R1_TOGGLE)

        def __eq__(self, other: object) -> bool:
            return other is automation.KeyAction.PAD_R1_TOGGLE

    spoofed_actions = set(profile.enabled_actions)
    spoofed_actions.remove(automation.KeyAction.PAD_R1_TOGGLE)
    spoofed_actions.add(ActionEqual())

    pure_invalid_profiles = (
        dataclasses.replace(profile, profile_id=DigestText(automation.PROFILE_ID)),
        dataclasses.replace(profile, effective_uid=True),
        dataclasses.replace(
            profile,
            executable=dataclasses.replace(profile.executable, sha256="f" * 64),
        ),
        dataclasses.replace(
            profile,
            executable=dataclasses.replace(
                profile.executable,
                sha256=DigestText(automation.EXPECTED_DUCKSTATION_SHA256),
            ),
        ),
        dataclasses.replace(
            profile,
            executable=dataclasses.replace(
                profile.executable,
                byte_length=automation.EXPECTED_EXECUTABLE_SIZE - 1,
            ),
        ),
        dataclasses.replace(
            profile,
            executable=dataclasses.replace(profile.executable, mode=0o777),
        ),
        dataclasses.replace(
            profile,
            settings=dataclasses.replace(profile.settings, sha256="f" * 64),
        ),
        dataclasses.replace(
            profile,
            settings=dataclasses.replace(profile.settings, mode=0o644),
        ),
        dataclasses.replace(
            profile,
            candidate_cue=dataclasses.replace(
                profile.candidate_cue, sha256="f" * 64
            ),
        ),
        dataclasses.replace(
            profile,
            candidate_cue=dataclasses.replace(profile.candidate_cue, mode=0o600),
        ),
        dataclasses.replace(
            profile,
            candidate_bin=dataclasses.replace(
                profile.candidate_bin, sha256="f" * 64
            ),
        ),
        dataclasses.replace(
            profile,
            candidate_bin=dataclasses.replace(profile.candidate_bin, mode=0o600),
        ),
        dataclasses.replace(
            profile,
            external_home_real_path=DigestText(profile.external_home_real_path),
        ),
        dataclasses.replace(
            profile,
            allowed_process_cache_real_path=DigestText(
                profile.allowed_process_cache_real_path
            ),
        ),
        dataclasses.replace(profile, handoff_receipt_sha256="f" * 64),
        dataclasses.replace(profile, handoff_receipt_sha256=ClosureEqual()),
        dataclasses.replace(profile, app_content_manifest_sha256="f" * 64),
        dataclasses.replace(profile, app_output_tree_manifest_sha256="f" * 64),
        dataclasses.replace(profile, package_envelope_sha256="f" * 64),
        dataclasses.replace(profile, external_home_bios_sha256="f" * 64),
        dataclasses.replace(
            profile, host_normal_settings_sentinel_sha256="f" * 64
        ),
        dataclasses.replace(profile, receipt_closure_sha256="f" * 64),
        dataclasses.replace(
            profile, home_output_tree_manifest_sha256="f" * 64
        ),
        dataclasses.replace(profile, candidate_manifest_sha256="f" * 64),
        dataclasses.replace(profile, enabled_actions=frozenset()),
        dataclasses.replace(profile, enabled_actions=frozenset(spoofed_actions)),
    )
    pure_observer = SequenceRuntimeObserver((base, base))
    pure_attestor = automation.ExactRuntimeSessionAttestor(
        pure_observer, session
    )
    pure_topology_before = len(gdb_attestor.connection_calls)
    pure_sent_before = tuple(fake_socket.sent)
    for invalid_profile in pure_invalid_profiles:
        expect_any_refusal(
            lambda invalid_profile=invalid_profile: pure_attestor.attest_exact(
                invalid_profile, process
            )
        )
    expect_any_refusal(
        lambda: pure_attestor.attest_exact(
            profile, dataclasses.replace(process, pid=True)
        )
    )
    expect_any_refusal(
        lambda: pure_attestor.attest_exact(
            profile,
            dataclasses.replace(
                process,
                start_seconds=automation.RUNTIME_ATTESTATION_MAX_SAFE_INTEGER + 1,
            ),
        )
    )
    expect_any_refusal(
        lambda: pure_attestor.attest_exact(
            dataclasses.replace(profile, effective_uid=automation.NATIVE_UID_MAX + 1),
            dataclasses.replace(
                process,
                effective_uid=automation.NATIVE_UID_MAX + 1,
                real_uid=automation.NATIVE_UID_MAX + 1,
                saved_uid=automation.NATIVE_UID_MAX + 1,
            ),
        )
    )
    if (
        pure_observer.calls
        or len(gdb_attestor.connection_calls) != pure_topology_before
        or tuple(fake_socket.sent) != pure_sent_before
        or session.status().state != "RUNNING"
    ):
        raise AssertionError(
            "Malformed frozen profile/process reached observation or GDB."
        )

    bad_before_session, bad_before_socket, bad_before_connector, bad_before_gdb, _ = (
        make_gdb_session(
            [bytes((value,)) for value in connect_transcript], process=process
        )
    )
    bad_before_session.connect_resume_once(process)
    bad_before_observer = SequenceRuntimeObserver((invalid_observations[1][1],))
    bad_before_sent = tuple(bad_before_socket.sent)
    bad_before_topology = len(bad_before_gdb.connection_calls)
    bad_before_exact = automation.ExactRuntimeSessionAttestor(
        bad_before_observer, bad_before_session
    )
    expect_any_refusal(
        lambda: bad_before_exact.attest_exact(profile, process)
    )
    if (
        len(bad_before_observer.calls) != 1
        or len(bad_before_gdb.connection_calls) != bad_before_topology
        or tuple(bad_before_socket.sent) != bad_before_sent
        or len(bad_before_connector.calls) != 1
        or bad_before_socket.close_calls != 1
        or bad_before_session.status().state != "POISONED"
    ):
        raise AssertionError(
            "Invalid bracket-start observation crossed GDB or stayed reusable."
        )
    retry_calls = len(bad_before_observer.calls)
    expect_any_refusal(
        lambda: bad_before_exact.attest_exact(profile, process)
    )
    if len(bad_before_observer.calls) != retry_calls:
        raise AssertionError("POISONED exact attestor retried process observation.")

    binding_drift_session, binding_drift_socket, binding_drift_connector, binding_drift_gdb, _ = (
        make_gdb_session(
            [bytes((value,)) for value in connect_transcript], process=process
        )
    )
    binding_drift_session.connect_resume_once(process)
    descriptor_drift = observation(
        open_files=(opened(profile.candidate_bin, "18r"),)
    )
    binding_drift_observer = SequenceRuntimeObserver((base, descriptor_drift))
    binding_drift_sent = tuple(binding_drift_socket.sent)
    binding_drift_topology = len(binding_drift_gdb.connection_calls)
    binding_drift_results: list[Any] = []
    expect_any_refusal(
        lambda: binding_drift_results.append(
            automation.ExactRuntimeSessionAttestor(
                binding_drift_observer, binding_drift_session
            ).attest_exact(profile, process)
        )
    )
    if (
        binding_drift_results
        or len(binding_drift_observer.calls) != 2
        or len(binding_drift_gdb.connection_calls) - binding_drift_topology != 1
        or tuple(binding_drift_socket.sent) != binding_drift_sent
        or len(binding_drift_connector.calls) != 1
        or binding_drift_socket.close_calls != 1
        or binding_drift_session.status().state != "POISONED"
    ):
        raise AssertionError(
            "Individually valid before/after disc binding drift escaped the bracket."
        )

    topology_gate = {"drift": False}

    def bracket_topology(
        observed_process: Any, expected_client: Any | None, _call: int
    ) -> Any:
        changes: dict[str, object] = {}
        if topology_gate["drift"] and expected_client is not None:
            changes["server_device_token"] = "0xcccc"
        return make_gdb_connection_set(
            make_gdb_listener(pid=observed_process.pid),
            expected_client,
            **changes,
        )

    topology_session, topology_socket, topology_connector, topology_gdb, _ = (
        make_gdb_session(
            [bytes((value,)) for value in connect_transcript],
            process=process,
            connection_observer=bracket_topology,
        )
    )
    topology_session.connect_resume_once(process)

    class TopologyGateObserver(SequenceRuntimeObserver):
        def observe(self, observed_profile: Any, observed_process: Any) -> Any:
            value = super().observe(observed_profile, observed_process)
            if len(self.calls) == 2:
                topology_gate["drift"] = True
            return value

    topology_observer = TopologyGateObserver((base, base))
    topology_sent = tuple(topology_socket.sent)
    topology_calls = len(topology_gdb.connection_calls)
    expect_any_refusal(
        lambda: automation.ExactRuntimeSessionAttestor(
            topology_observer, topology_session
        ).attest_exact(profile, process)
    )
    if (
        len(topology_observer.calls) != 2
        or len(topology_gdb.connection_calls) - topology_calls != 2
        or tuple(topology_socket.sent) != topology_sent
        or len(topology_connector.calls) != 1
        or topology_socket.close_calls != 1
        or topology_session.status().state != "POISONED"
        or topology_session.status().pause_state_certain
    ):
        raise AssertionError(
            "Post-observation competing GDB client escaped the exact bracket."
        )

    race_entered = threading.Event()
    race_continue = threading.Event()

    class BlockingAfterObserver(SequenceRuntimeObserver):
        def observe(self, observed_profile: Any, observed_process: Any) -> Any:
            value = super().observe(observed_profile, observed_process)
            if len(self.calls) == 2:
                race_entered.set()
                if not race_continue.wait(timeout=1):
                    raise AssertionError("Exact bracket race fixture timed out.")
            return value

    race_session, race_socket, race_connector, _, _ = make_gdb_session(
        [bytes((value,)) for value in connect_transcript], process=process
    )
    race_session.connect_resume_once(process)
    race_observer = BlockingAfterObserver((base, base))
    race_exact = automation.ExactRuntimeSessionAttestor(
        race_observer, race_session
    )
    race_results: list[Any] = []
    race_errors: list[BaseException] = []

    def attest_during_pause_race() -> None:
        try:
            race_results.append(race_exact.attest_exact(profile, process))
        except BaseException as exc:
            race_errors.append(exc)

    race_sent = tuple(race_socket.sent)
    race_thread = threading.Thread(target=attest_during_pause_race, daemon=True)
    race_thread.start()
    if not race_entered.wait(timeout=1):
        raise AssertionError("Exact bracket did not reach its second observation.")
    expect_refusal(
        lambda: race_session.arm_planned_pause(process),
        "Concurrent DuckStation GDB transition",
    )
    race_continue.set()
    race_thread.join(timeout=1)
    if (
        race_thread.is_alive()
        or race_results
        or len(race_errors) != 1
        or not isinstance(race_errors[0], automation.AutomationRefusal)
        or race_session.status().state != "POISONED"
        or tuple(race_socket.sent) != race_sent
        or len(race_connector.calls) != 1
        or race_socket.close_calls != 1
    ):
        raise AssertionError(
            "Concurrent pause escaped as a stale RUNNING runtime witness."
        )

    new_session, new_socket, new_connector, new_gdb, _ = make_gdb_session(
        (), process=process
    )
    new_observer = SequenceRuntimeObserver((base,))
    expect_any_refusal(
        lambda: automation.ExactRuntimeSessionAttestor(
            new_observer, new_session
        ).attest_exact(profile, process)
    )
    if (
        new_observer.calls
        or new_session.status().state != "NEW"
        or new_connector.calls
        or new_gdb.connection_calls
        or new_socket.sent
        or new_socket.recv_sizes
        or new_socket.close_calls
    ):
        raise AssertionError("NEW composite attestation crossed a boundary.")

    armed_session, armed_socket, armed_connector, armed_gdb, _ = make_gdb_session(
        [bytes((value,)) for value in connect_transcript], process=process
    )
    armed_session.connect_resume_once(process)
    armed_session.arm_planned_pause(process)
    armed_observer = SequenceRuntimeObserver((base,))
    armed_sent = tuple(armed_socket.sent)
    armed_topology = len(armed_gdb.connection_calls)
    expect_any_refusal(
        lambda: automation.ExactRuntimeSessionAttestor(
            armed_observer, armed_session
        ).attest_exact(profile, process)
    )
    if (
        armed_observer.calls
        or armed_session.status().state != "EXPECTING_PLAN_PAUSE"
        or tuple(armed_socket.sent) != armed_sent
        or len(armed_gdb.connection_calls) != armed_topology
        or len(armed_connector.calls) != 1
        or armed_socket.close_calls
    ):
        raise AssertionError("Armed composite attestation destroyed recovery state.")
    armed_session.close()

    source = TOOL_PATH.read_text(encoding="utf-8")
    main_names = frozenset(automation.main.__code__.co_names)
    foundation_names = frozenset(
        automation.UnattendedFoundation.run.__code__.co_names
    ) | frozenset(
        automation.UnattendedFoundation._run_with_coordinator_lease.__code__.co_names
    ) | frozenset(automation.UnattendedFoundation._execute.__code__.co_names)
    coordinator_names = frozenset(
        automation.GdbInputPulseLifecycleCoordinator.__init__.__code__.co_names
    ) | frozenset(
        automation.GdbInputPulseLifecycleCoordinator.start_execute.__code__.co_names
    ) | frozenset(
        automation.GdbInputPulseLifecycleCoordinator.execute_next.__code__.co_names
    )
    if (
        "ExactRuntimeSessionAttestor" in main_names
        or "ExactRuntimeSessionAttestor" in foundation_names
        or "ExactRuntimeSessionAttestor" in coordinator_names
        or "UnavailableRuntimeSessionAttestor" not in main_names
        or source.count("attest_running_owned(") != 1
        or source.count("attest_running_owned_bracketed(") != 2
    ):
        raise AssertionError("Production crossed the offline exact-attestor gate.")
    session.close()


def test_planned_space_pause_resume_lifecycle() -> None:
    process = make_process()
    transcript = b"".join((
        gdb_u32_reply(100),
        gdb_u32_reply(100),
        gdb_u32_reply(100),
        b"+",
        gdb_u32_reply(100),
        gdb_u32_reply(101),
        b"+" + gdb_rsp_packet(b"S05"),
        gdb_u32_reply(200),
        gdb_u32_reply(200),
        gdb_u32_reply(200),
        b"+",
        gdb_u32_reply(201),
    ))
    session, fake_socket, connector, attestor, _ = make_gdb_session(
        [bytes((value,)) for value in transcript], process=process
    )
    session.connect_resume_once(process)
    sent_before_arm = tuple(fake_socket.sent)
    calls_before_arm = len(attestor.connection_calls)
    session.arm_planned_pause(process)
    if tuple(fake_socket.sent) != sent_before_arm:
        raise AssertionError("Arming planned pause sent RSP bytes before Space key-up.")
    if len(attestor.connection_calls) != calls_before_arm + 1:
        raise AssertionError("Arming planned pause skipped sole-client observation.")
    if session.status() != automation.GdbSessionStatus(
        "EXPECTING_PLAN_PAUSE",
        process,
        make_gdb_listener(),
        1,
        1,
        False,
        True,
    ):
        raise AssertionError("Planned pause did not enter its armed uncertain state.")

    session.confirm_planned_pause(process)
    session.require_paused(process)
    if session.status() != automation.GdbSessionStatus(
        "PAUSED_BY_PLAN",
        process,
        make_gdb_listener(),
        1,
        1,
        True,
        True,
    ):
        raise AssertionError("Exact S05 plus stable VBlank did not prove PAUSED.")

    sent_before_resume_arm = tuple(fake_socket.sent)
    session.arm_planned_resume(process)
    if tuple(fake_socket.sent) != sent_before_resume_arm:
        raise AssertionError("Arming planned resume sent RSP bytes before Space key-up.")
    if session.status().state != "EXPECTING_PLAN_RESUME":
        raise AssertionError("Planned resume did not enter its armed state.")
    session.confirm_planned_resume(process)
    session.require_running(process)
    if session.status() != automation.GdbSessionStatus(
        "RUNNING",
        process,
        make_gdb_listener(),
        1,
        1,
        True,
        True,
    ):
        raise AssertionError("Raw resume plus forward VBlank did not prove RUNNING.")

    if fake_socket.sent.count(automation.GDB_CONTINUE_PACKET) != 1:
        raise AssertionError("Planned Space pair emitted a second GDB continue.")
    # Nine framed memory replies plus the one planned S05 stop frame are each
    # ACKed exactly once.  Neither raw resume '+' is ACKed.
    if fake_socket.sent.count(b"+") != 10 or b"-" in fake_socket.sent:
        raise AssertionError("Planned Space RSP acknowledgement ownership changed.")
    commands = [packet for packet in fake_socket.sent if packet not in {b"+", b"-"}]
    if commands.count(automation.GDB_CONTINUE_PACKET) != 1 or any(
        packet not in {
            automation.GDB_CONTINUE_PACKET,
            automation.GDB_VBLANK_READ_PACKET,
        }
        for packet in commands
    ):
        raise AssertionError("Planned Space lifecycle widened the GDB command surface.")
    if len(connector.calls) != 1:
        raise AssertionError("Planned Space lifecycle did not share one socket.")
    session.close()
    if fake_socket.close_calls != 1:
        raise AssertionError("Planned Space lifecycle did not close once.")


def test_targeted_key_transport_exclusive_capability() -> None:
    process = make_process()
    pristine_backend = FakeBackend()
    pristine = automation.TargetedKeyTransport(
        pristine_backend, sleeper=lambda _: None
    )
    expected_pristine = automation.TargetedKeyTransportStatus(False, 0, 0, ())
    if pristine.status() != expected_pristine:
        raise AssertionError("Fresh key transport was not an atomic pristine snapshot.")
    capability = pristine.claim_exclusive_lifecycle()
    if (
        pristine.status()
        != automation.TargetedKeyTransportStatus(True, 0, 0, ())
        or pristine_backend.preflight_calls
        or pristine_backend.frontmost_checks
        or pristine_backend.posts
    ):
        raise AssertionError("Exclusive claim crossed a fake native boundary or counter.")
    expect_refusal(
        pristine.claim_exclusive_lifecycle,
        "already has an exclusive lifecycle owner",
    )
    if any(
        hasattr(capability, name)
        for name in ("backend", "owner", "token", "transport")
    ):
        raise AssertionError("Exclusive capability exposed a public owner or token alias.")

    callback_calls = 0

    def count_callback() -> None:
        nonlocal callback_calls
        callback_calls += 1

    expect_refusal(
        lambda: pristine.require_target(process.pid),
        "Direct key-transport mutation",
    )
    expect_refusal(
        lambda: pristine.tap(
            process.pid,
            automation.KeyAction.PAD_LEFT_TOGGLE,
            release_identity_validator=count_callback,
            post_release_drift_observer=count_callback,
            pre_key_down_validator=count_callback,
        ),
        "Direct key-transport mutation",
    )
    expect_refusal(
        lambda: pristine.compensating_toggle(
            process.pid,
            automation.KeyAction.PAD_LEFT_TOGGLE,
            identity_validator=count_callback,
            post_release_drift_observer=count_callback,
            pre_key_down_validator=count_callback,
        ),
        "Direct key-transport mutation",
    )
    expect_refusal(pristine.release_all, "Direct key-transport mutation")
    if (
        pristine.status()
        != automation.TargetedKeyTransportStatus(True, 0, 0, ())
        or callback_calls
        or pristine_backend.preflight_calls
        or pristine_backend.frontmost_checks
        or pristine_backend.posts
    ):
        raise AssertionError("Retained raw alias reached callbacks or fake native work.")
    if capability.release_all() != ():
        raise AssertionError("Owned no-op cleanup returned an unexpected error.")

    foreign_backend = FakeBackend()
    foreign = automation.TargetedKeyTransport(
        foreign_backend, sleeper=lambda _: None
    )
    foreign_capability = foreign.claim_exclusive_lifecycle()
    foreign_token = object.__getattribute__(
        foreign_capability,
        "_ExclusiveTargetedKeyTransportCapability__token",
    )
    expect_refusal(
        lambda: pristine._exclusive_release_all(object()),
        "absent or stale",
    )
    expect_refusal(
        lambda: pristine._exclusive_release_all(foreign_token),
        "absent or stale",
    )
    if foreign_capability.release_all() != () or foreign_backend.posts:
        raise AssertionError("Capability identity was not bound to one transport.")

    class DerivedTransport(automation.TargetedKeyTransport):
        pass

    derived = DerivedTransport(FakeBackend(), sleeper=lambda _: None)
    expect_refusal(derived.claim_exclusive_lifecycle, "exact reviewed type")
    if derived.status() != expected_pristine:
        raise AssertionError("Derived-type claim refusal changed transport state.")

    def expect_prior_attempt_blocks_claim(
        label: str,
        mutate: Any,
        transport: Any,
    ) -> None:
        try:
            mutate()
        except automation.AutomationRefusal:
            pass
        if transport.status().raw_mutation_attempts != 1:
            raise AssertionError(f"{label} did not record one raw mutation attempt.")
        expect_refusal(
            transport.claim_exclusive_lifecycle,
            "before any pulse work",
        )
        if transport.exclusive_claimed:
            raise AssertionError(f"{label} transport was claimed after prior use.")

    targeted = automation.TargetedKeyTransport(FakeBackend(), sleeper=lambda _: None)
    expect_prior_attempt_blocks_claim(
        "target preflight",
        lambda: targeted.require_target(process.pid),
        targeted,
    )

    no_op_cleanup = automation.TargetedKeyTransport(
        FakeBackend(), sleeper=lambda _: None
    )
    expect_prior_attempt_blocks_claim(
        "no-op cleanup", no_op_cleanup.release_all, no_op_cleanup
    )

    successful = automation.TargetedKeyTransport(FakeBackend(), sleeper=lambda _: None)
    expect_prior_attempt_blocks_claim(
        "successful pulse",
        lambda: successful.tap(
            process.pid,
            automation.KeyAction.PAD_LEFT_TOGGLE,
            release_identity_validator=lambda: None,
            post_release_drift_observer=lambda: None,
            pre_key_down_validator=lambda: None,
        ),
        successful,
    )
    if successful.status().posted_down_count != 1:
        raise AssertionError("Successful prior pulse lost its monotonic post count.")

    predown = automation.TargetedKeyTransport(FakeBackend(), sleeper=lambda _: None)

    def refuse_predown() -> None:
        raise automation.AutomationRefusal("Injected pre-down refusal.")

    expect_prior_attempt_blocks_claim(
        "pre-down-refused pulse",
        lambda: predown.tap(
            process.pid,
            automation.KeyAction.PAD_LEFT_TOGGLE,
            release_identity_validator=lambda: None,
            post_release_drift_observer=lambda: None,
            pre_key_down_validator=refuse_predown,
        ),
        predown,
    )
    if predown.status().posted_down_count != 0 or predown.possibly_held:
        raise AssertionError("Pre-down refusal fixture did not return to zero post state.")

    held = automation.TargetedKeyTransport(
        FakeBackend(fail_up_count=1), sleeper=lambda _: None
    )
    expect_prior_attempt_blocks_claim(
        "unresolved release",
        lambda: held.tap(
            process.pid,
            automation.KeyAction.PAD_LEFT_TOGGLE,
            release_identity_validator=lambda: None,
            post_release_drift_observer=lambda: None,
            pre_key_down_validator=lambda: None,
        ),
        held,
    )
    if held.possibly_held != ((process.pid, automation.KeyAction.PAD_LEFT_TOGGLE),):
        raise AssertionError("Failed release did not remain visible after claim refusal.")


def test_targeted_key_transport_claim_races_and_coordinator_validation() -> None:
    process = make_process()
    plan = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        nonce="13131313242424243535353546464646",
    )

    simultaneous = automation.TargetedKeyTransport(
        FakeBackend(), sleeper=lambda _: None
    )
    simultaneous_start = threading.Event()
    simultaneous_capabilities: list[Any] = []
    simultaneous_errors: list[BaseException] = []

    def claim_simultaneously() -> None:
        if not simultaneous_start.wait(timeout=1):
            simultaneous_errors.append(
                AssertionError("Simultaneous claim fixture did not start.")
            )
            return
        try:
            simultaneous_capabilities.append(
                simultaneous.claim_exclusive_lifecycle()
            )
        except BaseException as exc:
            simultaneous_errors.append(exc)

    simultaneous_threads = [
        threading.Thread(target=claim_simultaneously, daemon=True) for _ in range(2)
    ]
    for thread in simultaneous_threads:
        thread.start()
    simultaneous_start.set()
    for thread in simultaneous_threads:
        thread.join(timeout=1)
    if (
        any(thread.is_alive() for thread in simultaneous_threads)
        or len(simultaneous_capabilities) != 1
        or len(simultaneous_errors) != 1
        or not isinstance(simultaneous_errors[0], automation.AutomationRefusal)
        or simultaneous.status()
        != automation.TargetedKeyTransportStatus(True, 0, 0, ())
    ):
        raise AssertionError("Simultaneous claims did not produce exactly one owner.")

    cleanup_backend = FakeBackend(fail_up_count=1)
    cleanup_transport = automation.TargetedKeyTransport(
        cleanup_backend, sleeper=lambda _: None
    )
    cleanup_capability = cleanup_transport.claim_exclusive_lifecycle()
    expect_any_refusal(
        lambda: cleanup_capability.tap(
            process.pid,
            automation.KeyAction.PAD_LEFT_TOGGLE,
            release_identity_validator=lambda: None,
            post_release_drift_observer=lambda: None,
            pre_key_down_validator=lambda: None,
        )
    )
    if cleanup_transport.possibly_held != (
        (process.pid, automation.KeyAction.PAD_LEFT_TOGGLE),
    ):
        raise AssertionError("Injected capability failure hid its release obligation.")
    cleanup_results: list[tuple[str, ...]] = []

    def finish_cleanup_on_another_thread() -> None:
        cleanup_results.append(cleanup_capability.release_all())

    cleanup_thread = threading.Thread(
        target=finish_cleanup_on_another_thread, daemon=True
    )
    cleanup_thread.start()
    cleanup_thread.join(timeout=1)
    if (
        cleanup_thread.is_alive()
        or cleanup_results != [()]
        or cleanup_transport.possibly_held
        or cleanup_backend.posts
        != [
            (process.pid, 23, True),
            (process.pid, 23, False),
            (process.pid, 23, False),
        ]
    ):
        raise AssertionError("Capability exception retained its RLock or cleanup debt.")

    claimed_backend = FakeBackend()
    claimed = automation.TargetedKeyTransport(
        claimed_backend, sleeper=lambda _: None
    )
    claimed.claim_exclusive_lifecycle()
    raw_error: list[BaseException] = []

    def raw_after_claim() -> None:
        try:
            claimed.require_target(process.pid)
        except BaseException as exc:
            raw_error.append(exc)

    raw_thread = threading.Thread(target=raw_after_claim, daemon=True)
    raw_thread.start()
    raw_thread.join(timeout=1)
    if (
        raw_thread.is_alive()
        or len(raw_error) != 1
        or not isinstance(raw_error[0], automation.AutomationRefusal)
        or claimed_backend.preflight_calls
        or claimed_backend.frontmost_checks
        or claimed_backend.posts
    ):
        raise AssertionError("Claim-winning race allowed retained raw work.")

    raw_entered = threading.Event()
    raw_continue = threading.Event()

    class BlockingBackend(FakeBackend):
        def preflight_access(self) -> bool:
            raw_entered.set()
            if not raw_continue.wait(timeout=1):
                raise AssertionError("Raw-winner race fixture timed out.")
            return super().preflight_access()

    raw_first_backend = BlockingBackend()
    raw_first = automation.TargetedKeyTransport(
        raw_first_backend, sleeper=lambda _: None
    )
    raw_first_errors: list[BaseException] = []
    claim_errors: list[BaseException] = []

    def run_raw_first() -> None:
        try:
            raw_first.require_target(process.pid)
        except BaseException as exc:
            raw_first_errors.append(exc)

    def run_late_claim() -> None:
        try:
            raw_first.claim_exclusive_lifecycle()
        except BaseException as exc:
            claim_errors.append(exc)

    raw_first_thread = threading.Thread(target=run_raw_first, daemon=True)
    raw_first_thread.start()
    if not raw_entered.wait(timeout=1):
        raise AssertionError("Raw-winner race did not enter under the transport lock.")
    claim_thread = threading.Thread(target=run_late_claim, daemon=True)
    claim_thread.start()
    raw_continue.set()
    raw_first_thread.join(timeout=1)
    claim_thread.join(timeout=1)
    if (
        raw_first_thread.is_alive()
        or claim_thread.is_alive()
        or raw_first_errors
        or len(claim_errors) != 1
        or not isinstance(claim_errors[0], automation.AutomationRefusal)
        or raw_first.status()
        != automation.TargetedKeyTransportStatus(False, 1, 0, ())
        or raw_first_backend.preflight_calls != 1
        or raw_first_backend.frontmost_checks != [(process.pid, process.pid)]
        or raw_first_backend.posts
    ):
        raise AssertionError("Raw-winning race did not permanently block the late claim.")

    def fresh_session() -> Any:
        session, _, _, _, _ = make_gdb_session((), process=process)
        return session

    invalid_cases: tuple[tuple[str, Any], ...] = (
        (
            "bad-session-type",
            lambda transport: automation.GdbInputPulseLifecycleCoordinator(
                plan, object(), transport
            ),
        ),
        (
            "bad-clock",
            lambda transport: automation.GdbInputPulseLifecycleCoordinator(
                plan, fresh_session(), transport, clock=None
            ),
        ),
        (
            "bad-timeout",
            lambda transport: automation.GdbInputPulseLifecycleCoordinator(
                plan, fresh_session(), transport, pulse_timeout_seconds=0
            ),
        ),
    )
    for label, construct in invalid_cases:
        transport = automation.TargetedKeyTransport(
            FakeBackend(), sleeper=lambda _: None
        )
        expect_any_refusal(lambda construct=construct, transport=transport: construct(transport))
        if transport.status() != automation.TargetedKeyTransportStatus(False, 0, 0, ()):
            raise AssertionError(f"Invalid {label} constructor consumed the transport.")

    class DerivedTransport(automation.TargetedKeyTransport):
        pass

    derived = DerivedTransport(FakeBackend(), sleeper=lambda _: None)
    expect_any_refusal(
        lambda: automation.GdbInputPulseLifecycleCoordinator(
            plan, fresh_session(), derived
        )
    )
    if derived.status() != automation.TargetedKeyTransportStatus(False, 0, 0, ()):
        raise AssertionError("Derived transport constructor refusal consumed its state.")

    previously_targeted = automation.TargetedKeyTransport(
        FakeBackend(), sleeper=lambda _: None
    )
    previously_targeted.require_target(process.pid)
    expect_refusal(
        lambda: automation.GdbInputPulseLifecycleCoordinator(
            plan, fresh_session(), previously_targeted
        ),
        "pristine key transport",
    )
    if previously_targeted.status() != automation.TargetedKeyTransportStatus(
        False, 1, 0, ()
    ):
        raise AssertionError("Non-pristine constructor refusal changed history.")

    shared = automation.TargetedKeyTransport(FakeBackend(), sleeper=lambda _: None)
    first = automation.GdbInputPulseLifecycleCoordinator(
        plan, fresh_session(), shared
    )
    if not shared.exclusive_claimed or first.status().state != "NEW":
        raise AssertionError("Valid coordinator did not consume the pristine transport.")
    expect_refusal(
        lambda: automation.GdbInputPulseLifecycleCoordinator(
            plan, fresh_session(), shared
        ),
        "pristine key transport",
    )
    if shared.status().raw_mutation_attempts != 0:
        raise AssertionError("Second coordinator attempted raw transport mutation.")


def test_gdb_input_pulse_coordinator_exact_order() -> None:
    process = make_process()
    plan = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        ("PAD_LEFT_TOGGLE", "HELD"),
        ("PAD_LEFT_TOGGLE", "RELEASED"),
        nonce="10101010202020203030303040404040",
    )
    transcript = b"".join((
        gdb_u32_reply(100),
        gdb_u32_reply(100),
        gdb_u32_reply(100),
        b"+",
        gdb_u32_reply(100),
        gdb_u32_reply(101),
        b"+" + gdb_rsp_packet(b"S05"),
        gdb_u32_reply(200),
        gdb_u32_reply(200),
        gdb_u32_reply(200),
        b"+",
        gdb_u32_reply(201),
    ))
    session, fake_socket, connector, _, session_clock = make_gdb_session(
        [bytes((value,)) for value in transcript], process=process
    )
    trace: list[str] = []

    class TracingBackend(FakeBackend):
        def __init__(self) -> None:
            super().__init__()
            self.pulse = 0

        def post_key_down(self, pid: int, key_code: int) -> None:
            self.pulse += 1
            trace.append(f"down-{self.pulse}")
            super().post_key_down(pid, key_code)

        def force_key_up(
            self,
            pid: int,
            key_code: int,
            *,
            identity_validator: Any,
        ) -> Any:
            trace.append(f"up-enter-{self.pulse}")
            result = super().force_key_up(
                pid, key_code, identity_validator=identity_validator
            )
            trace.append(f"up-return-{self.pulse}")
            return result

    backend = TracingBackend()
    transport = automation.TargetedKeyTransport(backend, sleeper=lambda _: None)

    def trace_session_method(name: str, label: str) -> None:
        original = getattr(session, name)

        def traced(bound_process: Any) -> None:
            trace.append(label)
            original(bound_process)

        setattr(session, name, traced)

    trace_session_method("arm_planned_pause", "arm-pause")
    trace_session_method("confirm_planned_pause", "confirm-pause")
    trace_session_method("arm_planned_resume", "arm-resume")
    trace_session_method("confirm_planned_resume", "confirm-resume")

    coordinator = automation.GdbInputPulseLifecycleCoordinator(
        plan,
        session,
        transport,
        clock=session_clock.clock,
        pulse_timeout_seconds=1.0,
    )
    if coordinator.status() != automation.GdbInputPulseCoordinatorStatus(
        "NEW", None, 0, 4, 0, None, (), "NEW", False
    ):
        raise AssertionError("Coordinator construction was not pure NEW state.")
    if transport.status() != automation.TargetedKeyTransportStatus(True, 0, 0, ()):
        raise AssertionError("Coordinator did not consume one pristine transport claim.")
    if connector.calls or fake_socket.sent or backend.posts:
        raise AssertionError("Coordinator construction crossed a native boundary.")

    coordinator.start_execute(process)
    reentrant_mutation_checked = False
    raw_alias_checked = False

    def observe_reentrant_status(label: str) -> None:
        nonlocal raw_alias_checked, reentrant_mutation_checked
        if coordinator.status().state != "PULSE_IN_FLIGHT":
            raise AssertionError("Callback did not observe its in-flight pulse.")
        if not reentrant_mutation_checked:
            expect_refusal(
                lambda: coordinator.require_complete(process),
                "Reentrant GDB input-pulse completion",
            )
            reentrant_mutation_checked = True
        if not raw_alias_checked:
            expect_refusal(
                transport.release_all,
                "Direct key-transport mutation",
            )
            raw_alias_checked = True
        trace.append(label)

    for event in plan.events:
        coordinator.execute_next(
            event,
            process,
            release_identity_validator=lambda event=event: observe_reentrant_status(
                f"identity-{event.sequence}"
            ),
            pulse_ownership_validator=lambda event=event: observe_reentrant_status(
                f"ownership-{event.sequence}"
            ),
            post_release_drift_observer=lambda event=event: observe_reentrant_status(
                f"external-{event.sequence}"
            ),
        )

    if not (
        trace.index("arm-pause") < trace.index("down-1")
        < trace.index("up-return-1") < trace.index("confirm-pause")
        < trace.index("external-0")
        and trace.index("arm-resume") < trace.index("down-2")
        < trace.index("up-return-2") < trace.index("confirm-resume")
        < trace.index("external-1")
    ):
        raise AssertionError(f"Coordinator pulse ordering changed: {trace!r}")
    if not reentrant_mutation_checked or not raw_alias_checked:
        raise AssertionError("Callback reentrant mutation refusals were not exercised.")
    if any(
        trace.index(f"external-{index}") < trace.index(f"up-return-{index + 1}")
        for index in range(4)
    ):
        raise AssertionError("Coordinator confirmed an event before key-up returned.")
    if coordinator.status() != automation.GdbInputPulseCoordinatorStatus(
        "COMPLETE",
        process,
        4,
        4,
        4,
        automation.PlannedEmulationState.RUNNING,
        (),
        "RUNNING",
        False,
    ):
        raise AssertionError("Coordinator did not reach exact terminal RUNNING.")
    coordinator.require_complete(process)
    coordinator.close(process)
    coordinator.close(process)
    if (
        coordinator.status().state != "CLOSED"
        or fake_socket.close_calls != 1
        or not transport.exclusive_claimed
        or transport.status().raw_mutation_attempts != 0
    ):
        raise AssertionError("Coordinator did not close exactly once from RUNNING.")
    if len(connector.calls) != 1:
        raise AssertionError("Coordinator opened more than one GDB connection.")
    if fake_socket.sent.count(automation.GDB_CONTINUE_PACKET) != 1:
        raise AssertionError("Coordinator emitted more than the initial continue.")
    if transport.possibly_held:
        raise AssertionError("Coordinator left a physical key obligation.")
    if backend.posts != [
        (process.pid, automation.ACTION_SPECS[event.action].key_code, is_down)
        for event in plan.events
        for is_down in (True, False)
    ]:
        raise AssertionError(f"Coordinator host pulse transcript changed: {backend.posts!r}")

    main_names = frozenset(automation.main.__code__.co_names)
    foundation_names = frozenset(
        automation.UnattendedFoundation.run.__code__.co_names
    ) | frozenset(
        automation.UnattendedFoundation._run_with_coordinator_lease.__code__.co_names
    ) | frozenset(automation.UnattendedFoundation._execute.__code__.co_names)
    if (
        "GdbInputPulseLifecycleCoordinator" in main_names
        or "PersistentDuckStationGdbSession" in main_names
        or "claim_exclusive_lifecycle" in main_names
        or "claim_exclusive_lifecycle" in foundation_names
        or "UnavailableNativePadConfirmer" not in main_names
        or "UnavailableRuntimeSessionAttestor" not in main_names
    ):
        raise AssertionError("Production main crossed the offline coordinator gate.")
    if TOOL_PATH.read_bytes().count(b"GdbInputPulseLifecycleCoordinator") != 1:
        raise AssertionError(
            "Offline coordinator was referenced outside its class definition."
        )


def test_gdb_input_pulse_coordinator_failure_matrix() -> None:
    process = make_process()
    plan = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        nonce="50505050606060607070707080808080",
    )
    connect_transcript = b"".join((
        gdb_u32_reply(100),
        gdb_u32_reply(100),
        gdb_u32_reply(100),
        b"+",
        gdb_u32_reply(100),
        gdb_u32_reply(101),
    ))
    pause_transcript = b"".join((
        connect_transcript,
        b"+" + gdb_rsp_packet(b"S05"),
        gdb_u32_reply(200),
        gdb_u32_reply(200),
        gdb_u32_reply(200),
    ))

    odd = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        nonce="90909090a0a0a0a0b0b0b0b0c0c0c0c0",
    )
    untouched_session, untouched_socket, untouched_connector, _, _ = make_gdb_session(
        (connect_transcript,), process=process
    )
    untouched_transport = automation.TargetedKeyTransport(
        FakeBackend(), sleeper=lambda _: None
    )
    expect_refusal(
        lambda: automation.GdbInputPulseLifecycleCoordinator(
            odd, untouched_session, untouched_transport
        ),
        "pairs and terminate RUNNING",
    )
    if (
        untouched_connector.calls
        or untouched_socket.sent
        or untouched_transport.status()
        != automation.TargetedKeyTransportStatus(False, 0, 0, ())
    ):
        raise AssertionError("Invalid plan reached a connector or event boundary.")

    preused_session, preused_socket, preused_connector, _, _ = make_gdb_session(
        (connect_transcript,), process=process
    )
    preused_session.connect_resume_once(process)
    preused_sent = tuple(preused_socket.sent)
    preused_connector_calls = len(preused_connector.calls)
    preused_backend = FakeBackend()
    preused_transport = automation.TargetedKeyTransport(
        preused_backend, sleeper=lambda _: None
    )
    expect_refusal(
        lambda: automation.GdbInputPulseLifecycleCoordinator(
            plan, preused_session, preused_transport
        ),
        "fresh NEW session",
    )
    if (
        preused_session.status().state != "RUNNING"
        or tuple(preused_socket.sent) != preused_sent
        or len(preused_connector.calls) != preused_connector_calls
        or preused_socket.close_calls != 0
        or preused_backend.posts
        or preused_transport.status()
        != automation.TargetedKeyTransportStatus(False, 0, 0, ())
    ):
        raise AssertionError(
            "Pre-used session constructor refusal mutated its prior owner."
        )

    inaccessible_session, inaccessible_socket, inaccessible_connector, _, _ = (
        make_gdb_session((connect_transcript,), process=process)
    )
    inaccessible_backend = FakeBackend(permission=False)
    inaccessible_transport = automation.TargetedKeyTransport(
        inaccessible_backend, sleeper=lambda _: None
    )
    inaccessible = automation.GdbInputPulseLifecycleCoordinator(
        plan, inaccessible_session, inaccessible_transport
    )
    expect_refusal(
        lambda: inaccessible.start_execute(process),
        "Accessibility post-event permission",
    )
    inaccessible_status = inaccessible.status()
    if (
        inaccessible_status.state != "POISONED"
        or inaccessible_status.session_state != "NEW"
        or not inaccessible_status.recovery_required
        or inaccessible_connector.calls
        or inaccessible_socket.sent
        or inaccessible_backend.posts
    ):
        raise AssertionError(
            "Accessibility/focus preflight did not precede the GDB connector."
        )

    wrong_session, _, wrong_connector, _, _ = make_gdb_session(
        (connect_transcript,), process=process
    )
    wrong_backend = FakeBackend()
    wrong_transport = automation.TargetedKeyTransport(
        wrong_backend, sleeper=lambda _: None
    )
    wrong = automation.GdbInputPulseLifecycleCoordinator(
        plan, wrong_session, wrong_transport
    )
    wrong.start_execute(process)
    expect_refusal(
        lambda: wrong.execute_next(
            plan.events[1],
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=lambda: None,
            post_release_drift_observer=lambda: None,
        ),
        "event order",
    )
    if wrong.status().state != "POISONED" or wrong_backend.posts:
        raise AssertionError("Wrong event order did not refuse before host input.")
    calls_before_retry = len(wrong_connector.calls)
    expect_any_refusal(
        lambda: wrong.execute_next(
            plan.events[0],
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=lambda: None,
            post_release_drift_observer=lambda: None,
        )
    )
    if len(wrong_connector.calls) != calls_before_retry or wrong_backend.posts:
        raise AssertionError("Poisoned coordinator retried connector or input work.")

    predown_session, _, _, _, _ = make_gdb_session(
        (connect_transcript,), process=process
    )
    predown_backend = FakeBackend()
    predown_transport = automation.TargetedKeyTransport(
        predown_backend, sleeper=lambda _: None
    )
    predown = automation.GdbInputPulseLifecycleCoordinator(
        plan, predown_session, predown_transport
    )
    predown.start_execute(process)

    def refuse_ownership() -> None:
        raise automation.AutomationRefusal("Injected pulse ownership drift.")

    expect_refusal(
        lambda: predown.execute_next(
            plan.events[0],
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=refuse_ownership,
            post_release_drift_observer=lambda: None,
        ),
        "key-down ownership",
    )
    if (
        predown.status().state != "POISONED"
        or predown.status().next_event_index != 0
        or predown.status().emulation_state is not None
        or predown.status().session_state != "EXPECTING_PLAN_PAUSE"
        or not predown.status().recovery_required
        or predown_backend.posts
        or predown_transport.possibly_held
    ):
        raise AssertionError(
            "Pre-down refusal did not expose its armed recovery requirement."
        )

    possible_session, _, _, _, _ = make_gdb_session(
        [bytes((value,)) for value in pause_transcript], process=process
    )
    possible_backend = FakeBackend(fail_down=True)
    possible_transport = automation.TargetedKeyTransport(
        possible_backend, sleeper=lambda _: None
    )
    possible = automation.GdbInputPulseLifecycleCoordinator(
        plan, possible_session, possible_transport
    )
    possible.start_execute(process)
    expect_refusal(
        lambda: possible.execute_next(
            plan.events[0],
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=lambda: None,
            post_release_drift_observer=lambda: None,
        ),
        "failed before release",
    )
    possible_status = possible.status()
    if (
        possible_status.state != "POISONED"
        or possible_status.next_event_index != 1
        or possible_status.emulation_state is not automation.PlannedEmulationState.PAUSED
        or possible_status.session_state != "PAUSED_BY_PLAN"
        or possible_transport.possibly_held
        or possible_backend.posts != [
            (process.pid, 49, True),
            (process.pid, 49, False),
        ]
    ):
        raise AssertionError(
            "Possible-post key-down failure did not release and classify once."
        )

    drift_session, _, _, _, _ = make_gdb_session(
        [bytes((value,)) for value in pause_transcript], process=process
    )
    drift_backend = FakeBackend()
    drift_transport = automation.TargetedKeyTransport(
        drift_backend, sleeper=lambda _: None
    )
    drift = automation.GdbInputPulseLifecycleCoordinator(
        plan, drift_session, drift_transport
    )
    drift.start_execute(process)
    drift_calls = 0

    def refuse_post_release() -> None:
        nonlocal drift_calls
        drift_calls += 1
        raise automation.AutomationRefusal("Injected post-release process drift.")

    expect_refusal(
        lambda: drift.execute_next(
            plan.events[0],
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=lambda: None,
            post_release_drift_observer=refuse_post_release,
        ),
        "environment drifted",
    )
    drift_status = drift.status()
    if (
        drift_status.state != "POISONED"
        or drift_status.next_event_index != 1
        or drift_status.emulation_state is not automation.PlannedEmulationState.PAUSED
        or drift_status.session_state != "PAUSED_BY_PLAN"
        or drift_calls != 1
        or drift_transport.possibly_held
        or drift_backend.posts != [
            (process.pid, 49, True),
            (process.pid, 49, False),
        ]
    ):
        raise AssertionError(
            "Post-release drift lost exactly-once transition classification."
        )

    confirmation_transcript = connect_transcript + b"+" + gdb_rsp_packet(b"S04")
    confirmation_session, confirmation_socket, _, _, _ = make_gdb_session(
        [bytes((value,)) for value in confirmation_transcript], process=process
    )
    confirmation_backend = FakeBackend()
    confirmation_transport = automation.TargetedKeyTransport(
        confirmation_backend, sleeper=lambda _: None
    )
    confirmation = automation.GdbInputPulseLifecycleCoordinator(
        plan, confirmation_session, confirmation_transport
    )
    confirmation_external_calls = 0

    def count_confirmation_external() -> None:
        nonlocal confirmation_external_calls
        confirmation_external_calls += 1

    confirmation.start_execute(process)
    expect_refusal(
        lambda: confirmation.execute_next(
            plan.events[0],
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=lambda: None,
            post_release_drift_observer=count_confirmation_external,
        ),
        "not exact S05#b8",
    )
    confirmation_status = confirmation.status()
    if (
        confirmation_status.state != "POISONED"
        or confirmation_status.next_event_index != 0
        or confirmation_status.emulation_state is not None
        or confirmation_status.session_state != "POISONED"
        or confirmation_external_calls != 0
        or confirmation_transport.possibly_held
        or confirmation_socket.close_calls != 1
        or confirmation_backend.posts != [
            (process.pid, 49, True),
            (process.pid, 49, False),
        ]
    ):
        raise AssertionError(
            "Failed GDB confirmation forged settlement or skipped physical release."
        )

    topology_ownership_calls = 0

    def pre_up_topology(
        observed_process: Any, expected_client: Any | None, _call: int
    ) -> Any:
        changes: dict[str, object] = {}
        if topology_ownership_calls >= 2 and expected_client is not None:
            changes["server_device_token"] = "0xdead"
        return make_gdb_connection_set(
            make_gdb_listener(pid=observed_process.pid),
            expected_client,
            **changes,
        )

    topology_session, topology_socket, _, _, _ = make_gdb_session(
        (connect_transcript,),
        process=process,
        connection_observer=pre_up_topology,
    )
    topology_backend = FakeBackend()
    topology_transport = automation.TargetedKeyTransport(
        topology_backend, sleeper=lambda _: None
    )
    topology = automation.GdbInputPulseLifecycleCoordinator(
        plan, topology_session, topology_transport
    )
    topology_external_calls = 0

    def count_topology_ownership() -> None:
        nonlocal topology_ownership_calls
        topology_ownership_calls += 1

    def count_topology_external() -> None:
        nonlocal topology_external_calls
        topology_external_calls += 1

    topology.start_execute(process)
    expect_refusal(
        lambda: topology.execute_next(
            plan.events[0],
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=count_topology_ownership,
            post_release_drift_observer=count_topology_external,
        ),
        "environment drifted",
    )
    topology_status = topology.status()
    if (
        topology_status.state != "POISONED"
        or topology_status.next_event_index != 0
        or topology_status.emulation_state is not None
        or topology_status.session_state != "POISONED"
        or topology_external_calls != 0
        or topology_transport.possibly_held
        or topology_socket.close_calls != 1
        or topology_backend.posts != [
            (process.pid, 49, True),
            (process.pid, 49, False),
        ]
    ):
        raise AssertionError(
            "Pre-up topology poison vetoed the owed key-up or forged certainty."
        )

    retry_session, _, _, _, _ = make_gdb_session(
        [bytes((value,)) for value in pause_transcript], process=process
    )
    retry_backend = FakeBackend(fail_up_count=1)
    retry_transport = automation.TargetedKeyTransport(
        retry_backend, sleeper=lambda _: None
    )
    retry = automation.GdbInputPulseLifecycleCoordinator(
        plan, retry_session, retry_transport
    )
    retry_confirmations = 0
    original_retry_confirm = retry_session.confirm_planned_pause

    def count_retry_confirmation(bound_process: Any) -> None:
        nonlocal retry_confirmations
        retry_confirmations += 1
        original_retry_confirm(bound_process)

    retry_session.confirm_planned_pause = count_retry_confirmation
    retry.start_execute(process)
    expect_refusal(
        lambda: retry.execute_next(
            plan.events[0],
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=lambda: None,
            post_release_drift_observer=lambda: None,
        ),
        "mandatory key-up failed",
    )
    retry_status = retry.status()
    if (
        retry_status.state != "POISONED"
        or retry_status.next_event_index != 1
        or retry_status.emulation_state is not automation.PlannedEmulationState.PAUSED
        or retry_confirmations != 1
        or retry_transport.possibly_held
        or retry_session.status().state != "PAUSED_BY_PLAN"
        or retry_backend.posts != [
            (process.pid, 49, True),
            (process.pid, 49, False),
            (process.pid, 49, False),
        ]
    ):
        raise AssertionError("Cleanup key-up did not confirm exactly once then refuse.")

    final_retry_session, _, _, _, _ = make_gdb_session(
        [bytes((value,)) for value in pause_transcript], process=process
    )
    final_retry_backend = FakeBackend(fail_up_count=2)
    final_retry_transport = automation.TargetedKeyTransport(
        final_retry_backend, sleeper=lambda _: None
    )
    final_retry = automation.GdbInputPulseLifecycleCoordinator(
        plan, final_retry_session, final_retry_transport
    )
    final_retry_confirmations = 0
    original_final_retry_confirm = final_retry_session.confirm_planned_pause

    def count_final_retry_confirmation(bound_process: Any) -> None:
        nonlocal final_retry_confirmations
        final_retry_confirmations += 1
        original_final_retry_confirm(bound_process)

    final_retry_session.confirm_planned_pause = count_final_retry_confirmation
    final_retry.start_execute(process)
    observed_final_retry_error = ""
    try:
        final_retry.execute_next(
            plan.events[0],
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=lambda: None,
            post_release_drift_observer=lambda: None,
        )
    except automation.AutomationRefusal as exc:
        observed_final_retry_error = str(exc)
    else:
        raise AssertionError("Final cleanup retry unexpectedly accepted the pulse.")
    final_retry_status = final_retry.status()
    if (
        "mandatory key-up failed" not in observed_final_retry_error.lower()
        or "cleanup remains incomplete" in observed_final_retry_error.lower()
        or final_retry_status.state != "POISONED"
        or final_retry_status.next_event_index != 1
        or final_retry_status.emulation_state
        is not automation.PlannedEmulationState.PAUSED
        or final_retry_confirmations != 1
        or final_retry_transport.possibly_held
        or final_retry_backend.posts != [
            (process.pid, 49, True),
            (process.pid, 49, False),
            (process.pid, 49, False),
            (process.pid, 49, False),
        ]
    ):
        raise AssertionError(
            "Successful final key-up retry was reported as still incomplete."
        )

    stuck_session, _, _, _, _ = make_gdb_session(
        (connect_transcript,), process=process
    )
    stuck_backend = FakeBackend(fail_up_count=3)
    stuck_transport = automation.TargetedKeyTransport(
        stuck_backend, sleeper=lambda _: None
    )
    stuck = automation.GdbInputPulseLifecycleCoordinator(
        plan, stuck_session, stuck_transport
    )
    stuck.start_execute(process)
    expect_refusal(
        lambda: stuck.execute_next(
            plan.events[0],
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=lambda: None,
            post_release_drift_observer=lambda: None,
        ),
        "cleanup remains incomplete",
    )
    if (
        stuck.status().state != "POISONED"
        or stuck.status().next_event_index != 0
        or stuck_transport.possibly_held != ((process.pid, automation.KeyAction.TOGGLE_PAUSE),)
        or stuck_backend.posts != [
            (process.pid, 49, True),
            (process.pid, 49, False),
            (process.pid, 49, False),
            (process.pid, 49, False),
        ]
    ):
        raise AssertionError("Exhausted cleanup hid the held key or posted another Space down.")

    arm_clock = FakeClock()
    arm_gate = {"active": False, "advanced": False}

    def slow_arm_set(
        observed_process: Any, expected_client: Any | None, _call: int
    ) -> Any:
        if arm_gate["active"] and not arm_gate["advanced"]:
            arm_clock.value += 3.0
            arm_gate["advanced"] = True
        return make_gdb_connection_set(
            make_gdb_listener(pid=observed_process.pid), expected_client
        )

    arm_session, _, _, _, _ = make_gdb_session(
        (connect_transcript,),
        process=process,
        connection_observer=slow_arm_set,
    )
    arm_backend = FakeBackend()
    arm_transport = automation.TargetedKeyTransport(
        arm_backend, sleeper=lambda _: None
    )
    arm = automation.GdbInputPulseLifecycleCoordinator(
        plan,
        arm_session,
        arm_transport,
        clock=arm_clock.clock,
        pulse_timeout_seconds=2.0,
    )
    arm.start_execute(process)
    arm_gate["active"] = True
    expect_refusal(
        lambda: arm.execute_next(
            plan.events[0],
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=lambda: None,
            post_release_drift_observer=lambda: None,
        ),
        "key-down ownership",
    )
    arm_status = arm.status()
    if (
        not arm_gate["advanced"]
        or arm_status.state != "POISONED"
        or arm_status.next_event_index != 0
        or arm_status.emulation_state is not None
        or arm_status.session_state != "EXPECTING_PLAN_PAUSE"
        or arm_backend.posts
        or arm_transport.possibly_held
    ):
        raise AssertionError("Time consumed while arming reset the pulse deadline.")

    late_session, _, _, _, _ = make_gdb_session(
        [bytes((value,)) for value in pause_transcript], process=process
    )
    late_clock = FakeClock()
    late_backend = FakeBackend()
    late_transport = automation.TargetedKeyTransport(
        late_backend, sleeper=lambda _seconds: late_clock.sleep(3.0)
    )
    late = automation.GdbInputPulseLifecycleCoordinator(
        plan,
        late_session,
        late_transport,
        clock=late_clock.clock,
        pulse_timeout_seconds=2.0,
    )
    late.start_execute(process)
    expect_refusal(
        lambda: late.execute_next(
            plan.events[0],
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=lambda: None,
            post_release_drift_observer=lambda: None,
        ),
        "absolute acceptance deadline",
    )
    if (
        late.status().state != "POISONED"
        or late.status().next_event_index != 1
        or late.status().emulation_state is not automation.PlannedEmulationState.PAUSED
        or late_transport.possibly_held
        or late_session.status().state != "PAUSED_BY_PLAN"
        or late_backend.posts != [
            (process.pid, 49, True),
            (process.pid, 49, False),
        ]
    ):
        raise AssertionError("Late pulse did not release, classify, and then refuse.")

    mixed_plan = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        ("PAD_LEFT_TOGGLE", "HELD"),
        ("PAD_LEFT_TOGGLE", "RELEASED"),
        nonce="d0d0d0d0e0e0e0e0f0f0f0f001010101",
    )
    resume_transcript = pause_transcript + b"+" + gdb_u32_reply(201)
    non_toggle_gate = {"active": False}

    def non_toggle_set(
        observed_process: Any, expected_client: Any | None, _call: int
    ) -> Any:
        changes: dict[str, object] = {}
        if non_toggle_gate["active"] and expected_client is not None:
            changes["server_device_token"] = "0xbeef"
        return make_gdb_connection_set(
            make_gdb_listener(pid=observed_process.pid),
            expected_client,
            **changes,
        )

    non_toggle_session, non_toggle_socket, _, _, _ = make_gdb_session(
        [bytes((value,)) for value in resume_transcript],
        process=process,
        connection_observer=non_toggle_set,
    )
    non_toggle_backend = FakeBackend()
    non_toggle_transport = automation.TargetedKeyTransport(
        non_toggle_backend, sleeper=lambda _: None
    )
    non_toggle = automation.GdbInputPulseLifecycleCoordinator(
        mixed_plan, non_toggle_session, non_toggle_transport
    )
    non_toggle.start_execute(process)
    for event in mixed_plan.events[:2]:
        non_toggle.execute_next(
            event,
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=lambda: None,
            post_release_drift_observer=lambda: None,
        )
    non_toggle_gate["active"] = True
    expect_any_refusal(
        lambda: non_toggle.execute_next(
            mixed_plan.events[2],
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=lambda: None,
            post_release_drift_observer=lambda: None,
        )
    )
    non_toggle_status = non_toggle.status()
    if (
        non_toggle_status.state != "POISONED"
        or non_toggle_status.next_event_index != 2
        or non_toggle_status.emulation_state is not None
        or non_toggle_status.session_state != "POISONED"
        or non_toggle_socket.close_calls != 1
        or non_toggle_transport.possibly_held
        or non_toggle_backend.posts != [
            (process.pid, 49, True),
            (process.pid, 49, False),
            (process.pid, 49, True),
            (process.pid, 49, False),
        ]
    ):
        raise AssertionError(
            "Non-toggle ownership poison preserved false RUNNING certainty."
        )

    completion_gate = {"active": False}

    def completion_set(
        observed_process: Any, expected_client: Any | None, _call: int
    ) -> Any:
        changes: dict[str, object] = {}
        if completion_gate["active"] and expected_client is not None:
            changes["server_device_token"] = "0xcafe"
        return make_gdb_connection_set(
            make_gdb_listener(pid=observed_process.pid),
            expected_client,
            **changes,
        )

    completion_session, completion_socket, _, _, _ = make_gdb_session(
        [bytes((value,)) for value in resume_transcript],
        process=process,
        connection_observer=completion_set,
    )
    completion_backend = FakeBackend()
    completion_transport = automation.TargetedKeyTransport(
        completion_backend, sleeper=lambda _: None
    )
    completion = automation.GdbInputPulseLifecycleCoordinator(
        plan, completion_session, completion_transport
    )
    completion.start_execute(process)
    for event in plan.events:
        completion.execute_next(
            event,
            process,
            release_identity_validator=lambda: None,
            pulse_ownership_validator=lambda: None,
            post_release_drift_observer=lambda: None,
        )
    if completion.status().state != "COMPLETE":
        raise AssertionError("Completion-drift fixture did not reach COMPLETE.")
    completion_gate["active"] = True
    expect_any_refusal(lambda: completion.require_complete(process))
    completion_status = completion.status()
    if (
        completion_status.state != "POISONED"
        or completion_status.next_event_index != len(plan.events)
        or completion_status.emulation_state is not None
        or completion_status.session_state != "POISONED"
        or completion_socket.close_calls != 1
        or completion_transport.possibly_held
    ):
        raise AssertionError(
            "Terminal reattestation poison preserved false RUNNING certainty."
        )


def test_planned_space_transition_failure_matrix() -> None:
    process = make_process()
    connect_transcript = b"".join((
        gdb_u32_reply(100),
        gdb_u32_reply(100),
        gdb_u32_reply(100),
        b"+",
        gdb_u32_reply(100),
        gdb_u32_reply(101),
    ))

    wrong_stop, wrong_stop_socket, _, _, _ = make_gdb_session(
        (connect_transcript + b"+" + gdb_rsp_packet(b"S04"),), process=process
    )
    wrong_stop.connect_resume_once(process)
    wrong_stop.arm_planned_pause(process)
    expect_refusal(
        lambda: wrong_stop.confirm_planned_pause(process), "not exact S05#b8"
    )
    if (
        wrong_stop.status().state != "POISONED"
        or not wrong_stop.status().pause_state_certain
        or wrong_stop_socket.sent[-1:] != [b"+"]
    ):
        raise AssertionError("Valid unexpected stop was not ACKed and classified paused.")

    uppercase_stop, uppercase_socket, _, _, _ = make_gdb_session(
        (connect_transcript + b"+$S05#B8",), process=process
    )
    uppercase_stop.connect_resume_once(process)
    uppercase_stop.arm_planned_pause(process)
    expect_refusal(
        lambda: uppercase_stop.confirm_planned_pause(process),
        "not exact S05#b8",
    )
    if (
        not uppercase_stop.status().pause_state_certain
        or uppercase_socket.sent[-1:] != [b"+"]
    ):
        raise AssertionError("Uppercase checksum was accepted as pinned source framing.")

    malformed_stop, malformed_socket, _, _, _ = make_gdb_session(
        (connect_transcript + b"+$S05#00",), process=process
    )
    malformed_stop.connect_resume_once(process)
    malformed_stop.arm_planned_pause(process)
    expect_refusal(
        lambda: malformed_stop.confirm_planned_pause(process),
        "checksum does not match",
    )
    if (
        malformed_stop.status().pause_state_certain
        or malformed_socket.sent[-1:] != [b"-"]
    ):
        raise AssertionError("Malformed planned stop did not NACK with uncertainty.")

    notification_clock = FakeClock()
    notification_gate = {"armed": False, "advanced": False}

    def late_notification_set(
        observed_process: Any, expected_client: Any | None, _call: int
    ) -> Any:
        if notification_gate["armed"] and not notification_gate["advanced"]:
            notification_clock.value += 1.0
            notification_gate["advanced"] = True
        return make_gdb_connection_set(
            make_gdb_listener(pid=observed_process.pid), expected_client
        )

    late_notification = b"".join((
        connect_transcript,
        b"+" + gdb_rsp_packet(b"S05"),
        gdb_u32_reply(200),
        gdb_u32_reply(200),
        gdb_u32_reply(200),
    ))
    notification, notification_socket, _, _, _ = make_gdb_session(
        (late_notification,),
        process=process,
        clock=notification_clock.clock,
        sleeper=notification_clock.sleep,
        connection_observer=late_notification_set,
    )
    notification.connect_resume_once(process)
    notification.arm_planned_pause(process)
    notification_sent_before = len(notification_socket.sent)
    notification_gate["armed"] = True
    expect_refusal(
        lambda: notification.confirm_planned_pause(process),
        "planned pause confirmation timed out",
    )
    if (
        notification.status().state != "POISONED"
        or not notification.status().pause_state_certain
        or not notification_gate["advanced"]
        or notification_socket.sent[notification_sent_before:] != [b"+"]
    ):
        raise AssertionError(
            "Late exact S05 reset the deadline or triggered another memory read."
        )

    frozen_pause = b"".join((
        connect_transcript,
        b"+" + gdb_rsp_packet(b"S05"),
        *(gdb_u32_reply(200) for _ in range(5)),
    ))
    frozen, frozen_socket, _, _, _ = make_gdb_session(
        (frozen_pause,),
        process=process,
        maximum_samples=4,
    )
    frozen.connect_resume_once(process)
    frozen._clock = lambda: 0.0
    frozen._sleeper = lambda _seconds: None
    frozen.arm_planned_pause(process)
    expect_refusal(
        lambda: frozen.confirm_planned_pause(process),
        "planned pause confirmation timed out",
    )
    if (
        frozen.status().state != "POISONED"
        or not frozen.status().pause_state_certain
        or frozen_socket.sent.count(automation.GDB_CONTINUE_PACKET) != 1
    ):
        raise AssertionError("Exact planned S05 lost known-paused failure evidence.")

    advancing_pause = b"".join((
        connect_transcript,
        b"+" + gdb_rsp_packet(b"S05"),
        gdb_u32_reply(200),
        gdb_u32_reply(201),
    ))
    advancing, advancing_socket, advancing_connector, _, _ = make_gdb_session(
        (advancing_pause,), process=process
    )
    advancing.connect_resume_once(process)
    advancing.arm_planned_pause(process)
    expect_refusal(
        lambda: advancing.confirm_planned_pause(process),
        "did not remain at the planned pause",
    )
    advancing_status = advancing.status()
    sent_before_retry = tuple(advancing_socket.sent)
    expect_any_refusal(lambda: advancing.confirm_planned_pause(process))
    if (
        advancing_status.state != "POISONED"
        or advancing_status.pause_state_certain
        or tuple(advancing_socket.sent) != sent_before_retry
        or len(advancing_connector.calls) != 1
    ):
        raise AssertionError("Guest progress contradiction preserved PAUSED certainty.")

    late_advancing_clock = FakeClock()

    def late_advancing_set(
        observed_process: Any, expected_client: Any | None, call: int
    ) -> Any:
        if call == 21:
            late_advancing_clock.value += 0.2
        return make_gdb_connection_set(
            make_gdb_listener(pid=observed_process.pid), expected_client
        )

    late_advancing, _, _, _, _ = make_gdb_session(
        (advancing_pause,),
        process=process,
        clock=late_advancing_clock.clock,
        sleeper=late_advancing_clock.sleep,
        connection_observer=late_advancing_set,
    )
    late_advancing.connect_resume_once(process)
    late_advancing.arm_planned_pause(process)
    expect_refusal(
        lambda: late_advancing.confirm_planned_pause(process),
        "did not remain at the planned pause",
    )
    if late_advancing.status().pause_state_certain:
        raise AssertionError("Late advancing tick was misclassified as equal timeout.")

    restop_transcript = b"".join((
        connect_transcript,
        b"+" + gdb_rsp_packet(b"S05"),
        gdb_u32_reply(200),
        gdb_u32_reply(200),
        gdb_u32_reply(200),
        b"++" + gdb_rsp_packet(b"S05"),
    ))
    restop, restop_socket, restop_connector, _, _ = make_gdb_session(
        (restop_transcript,), process=process
    )
    restop.connect_resume_once(process)
    restop.arm_planned_pause(process)
    restop.confirm_planned_pause(process)
    restop.arm_planned_resume(process)
    expect_refusal(
        lambda: restop.confirm_planned_resume(process), "stop notification"
    )
    restop_status = restop.status()
    if (
        restop_status.state != "POISONED"
        or not restop_status.pause_state_certain
        or restop_socket.sent.count(automation.GDB_CONTINUE_PACKET) != 1
        or len(restop_connector.calls) != 1
        or restop_socket.sent[-1:] != [b"+"]
    ):
        raise AssertionError("Immediate re-stop was skipped or misclassified.")

    malformed_t_transcript = b"".join((
        connect_transcript,
        b"+" + gdb_rsp_packet(b"S05"),
        gdb_u32_reply(200),
        gdb_u32_reply(200),
        gdb_u32_reply(200),
        b"++" + gdb_rsp_packet(b"T05garbage"),
    ))
    malformed_t, malformed_t_socket, _, _, _ = make_gdb_session(
        (malformed_t_transcript,), process=process
    )
    malformed_t.connect_resume_once(process)
    malformed_t.arm_planned_pause(process)
    malformed_t.confirm_planned_pause(process)
    malformed_t.arm_planned_resume(process)
    expect_refusal(
        lambda: malformed_t.confirm_planned_resume(process),
        "memory reply is not exactly four hex bytes",
    )
    if (
        malformed_t.status().pause_state_certain
        or malformed_t_socket.sent[-1:] != [b"+"]
    ):
        raise AssertionError("Malformed T tail forged known-paused certainty.")


def test_one_shot_gdb_session_rollover_and_failure_matrix() -> None:
    process = make_process()

    rollover_transcript = b"".join((
        gdb_u32_reply(0xFFFFFFFF),
        gdb_u32_reply(0xFFFFFFFF),
        gdb_u32_reply(0xFFFFFFFF),
        b"+",
        gdb_u32_reply(0),
    ))
    rollover, rollover_socket, rollover_connector, _, _ = make_gdb_session(
        (rollover_transcript,), process=process
    )
    rollover.connect_resume_once(process)
    if rollover.status().state != "RUNNING":
        raise AssertionError("Unsigned VBlank rollover was not accepted as forward.")
    rollover.close()
    if len(rollover_connector.calls) != 1 or rollover_socket.close_calls != 1:
        raise AssertionError("Rollover session did not retain one connection.")

    cases: tuple[tuple[str, bytes, int | None, str, bool], ...] = (
        (
            "bad-continue-ack",
            b"".join((
                gdb_u32_reply(100), gdb_u32_reply(100), gdb_u32_reply(100), b"-"
            )),
            None,
            "acknowledgement",
            False,
        ),
        (
            "immediate-stop",
            b"".join((
                gdb_u32_reply(100),
                gdb_u32_reply(100),
                gdb_u32_reply(100),
                b"+",
                b"+" + gdb_rsp_packet(b"S05"),
            )),
            None,
            "stop notification",
            True,
        ),
        (
            "duplicate-resume-plus",
            b"".join((
                gdb_u32_reply(100),
                gdb_u32_reply(100),
                gdb_u32_reply(100),
                b"++",
                gdb_u32_reply(101),
            )),
            None,
            "duplicate request acknowledgement",
            False,
        ),
        (
            "unchanged-running",
            b"".join((
                gdb_u32_reply(100),
                gdb_u32_reply(100),
                gdb_u32_reply(100),
                b"+",
                *(gdb_u32_reply(100) for _ in range(8)),
            )),
            None,
            "prove RUNNING",
            False,
        ),
        (
            "backward-reset",
            b"".join((
                gdb_u32_reply(1000),
                gdb_u32_reply(1000),
                gdb_u32_reply(1000),
                b"+",
                gdb_u32_reply(10),
            )),
            None,
            "backward or reset",
            False,
        ),
        (
            "partial-continue-send",
            b"".join((
                gdb_u32_reply(100), gdb_u32_reply(100), gdb_u32_reply(100)
            )),
            7,
            "failed closed",
            False,
        ),
    )
    for name, transcript, fail_send_number, message, certain in cases:
        session, fake_socket, connector, _, _ = make_gdb_session(
            (transcript,),
            process=process,
            fail_send_number=fail_send_number,
        )
        refusal = expect_any_refusal(lambda: session.connect_resume_once(process))
        if message not in refusal:
            raise AssertionError(f"GDB session case {name} had wrong refusal: {refusal}")
        status = session.status()
        if (
            status.state != "POISONED"
            or status.connection_attempts != 1
            or status.continue_attempts != 1
            or status.pause_state_certain is not certain
            or fake_socket.close_calls != 1
            or len(connector.calls) != 1
            or fake_socket.sent.count(automation.GDB_CONTINUE_PACKET) != 1
        ):
            raise AssertionError(f"GDB session case {name} did not poison exactly: {status}")
        sent_before = tuple(fake_socket.sent)
        expect_any_refusal(lambda: session.connect_resume_once(process))
        if tuple(fake_socket.sent) != sent_before or len(connector.calls) != 1:
            raise AssertionError(f"GDB session case {name} retried after poison.")

    # A frozen clock is still bounded by the exact sample cap and never reaches
    # the continue boundary.  The accepted connection is positively paused.
    frozen_transcript = b"".join(gdb_u32_reply(77) for _ in range(5))
    frozen, frozen_socket, frozen_connector, _, _ = make_gdb_session(
        (frozen_transcript,),
        process=process,
        clock=lambda: 0.0,
        sleeper=lambda _seconds: None,
        maximum_samples=4,
    )
    expect_refusal(
        lambda: frozen.connect_resume_once(process),
        "pause confirmation timed out",
    )
    frozen_status = frozen.status()
    if (
        frozen_status.state != "POISONED"
        or frozen_status.connection_attempts != 1
        or frozen_status.continue_attempts != 0
        or frozen_status.pause_state_certain
        or automation.GDB_CONTINUE_PACKET in frozen_socket.sent
        or len(frozen_connector.calls) != 1
    ):
        raise AssertionError("Frozen-clock pause probe crossed the continue boundary.")

    # Public reads can never lazily create the pause-producing connection.
    unopened, unopened_socket, unopened_connector, unopened_attestor, _ = make_gdb_session(
        (), process=process
    )
    expect_refusal(lambda: unopened.read_pad_word(process), "not exact confirmed RUNNING")
    if (
        unopened_connector.calls
        or unopened_socket.sent
        or unopened_attestor.processes
        or unopened.status().state != "POISONED"
    ):
        raise AssertionError("A public pad read lazily opened a GDB connection.")


def test_gdb_session_ownership_deadline_and_status_boundaries() -> None:
    process = make_process()

    def exact_set(
        observed_process: Any, expected_client: Any | None, _call: int
    ) -> Any:
        return make_gdb_connection_set(
            make_gdb_listener(pid=observed_process.pid), expected_client
        )

    def preexisting(
        _process: Any, _expected_client: Any | None, _call: int
    ) -> Any:
        raise automation.AutomationRefusal(
            "A GDB client was already established before ownership."
        )

    pre, pre_socket, pre_connector, _, _ = make_gdb_session(
        (), process=process, connection_observer=preexisting
    )
    expect_refusal(
        lambda: pre.connect_resume_once(process), "already established"
    )
    pre_status = pre.status()
    if (
        pre_connector.calls
        or pre_socket.sent
        or pre_status.state != "POISONED"
        or pre_status.connection_attempts != 0
        or pre_status.continue_attempts != 0
        or pre_status.single_client_observed
    ):
        raise AssertionError("Pre-existing client refusal crossed the connector boundary.")

    def missing_post_pair(
        observed_process: Any, expected_client: Any | None, call: int
    ) -> Any:
        if call == 2:
            raise automation.AutomationRefusal(
                "GDB connection set is missing the accepted socket pair."
            )
        return exact_set(observed_process, expected_client, call)

    missing, missing_socket, missing_connector, _, _ = make_gdb_session(
        (), process=process, connection_observer=missing_post_pair
    )
    expect_refusal(
        lambda: missing.connect_resume_once(process), "missing the accepted"
    )
    missing_status = missing.status()
    if (
        len(missing_connector.calls) != 1
        or missing_socket.sent
        or missing_status.connection_attempts != 1
        or missing_status.continue_attempts != 0
        or missing_status.single_client_observed
    ):
        raise AssertionError("Missing post-connect socket pair reached an RSP request.")

    paused_transcript = b"".join((
        gdb_u32_reply(100), gdb_u32_reply(100), gdb_u32_reply(100)
    ))

    def drift_before_continue(
        observed_process: Any, expected_client: Any | None, call: int
    ) -> Any:
        normal = exact_set(observed_process, expected_client, call)
        if call == 9:
            return dataclasses.replace(normal, server_device_token="0xccc3")
        return normal

    before_c, before_c_socket, before_c_connector, _, _ = make_gdb_session(
        (paused_transcript,),
        process=process,
        connection_observer=drift_before_continue,
    )
    expect_refusal(
        lambda: before_c.connect_resume_once(process), "client set changed"
    )
    before_c_status = before_c.status()
    if (
        before_c_socket.sent.count(automation.GDB_CONTINUE_PACKET) != 0
        or before_c_status.continue_attempts != 0
        or before_c_status.pause_state_certain
        or len(before_c_connector.calls) != 1
    ):
        raise AssertionError("Pre-continue topology drift crossed the exact send boundary.")

    def drift_after_continue(
        observed_process: Any, expected_client: Any | None, call: int
    ) -> Any:
        normal = exact_set(observed_process, expected_client, call)
        if call == 10:
            return dataclasses.replace(normal, server_device_token="0xccc3")
        return normal

    after_c_transcript = paused_transcript + b"+"
    after_c, after_c_socket, after_c_connector, _, _ = make_gdb_session(
        (after_c_transcript,),
        process=process,
        connection_observer=drift_after_continue,
    )
    expect_refusal(
        lambda: after_c.connect_resume_once(process), "client set changed"
    )
    after_c_status = after_c.status()
    if (
        after_c_socket.sent.count(automation.GDB_CONTINUE_PACKET) != 1
        or after_c_status.continue_attempts != 1
        or after_c_status.pause_state_certain
        or len(after_c_connector.calls) != 1
    ):
        raise AssertionError("Post-continue topology drift retained false certainty.")

    def drift_while_arming(
        observed_process: Any, expected_client: Any | None, call: int
    ) -> Any:
        normal = exact_set(observed_process, expected_client, call)
        if call == 15:
            return dataclasses.replace(normal, client_device_token="0xccc3")
        return normal

    arm_transcript = paused_transcript + b"+" + gdb_u32_reply(100) + gdb_u32_reply(101)
    arm, arm_socket, arm_connector, _, _ = make_gdb_session(
        (arm_transcript,),
        process=process,
        connection_observer=drift_while_arming,
    )
    arm.connect_resume_once(process)
    sent_before_arm = tuple(arm_socket.sent)
    expect_refusal(lambda: arm.arm_planned_pause(process), "client set changed")
    if (
        arm.status().state != "POISONED"
        or arm.status().pause_state_certain
        or tuple(arm_socket.sent) != sent_before_arm
        or len(arm_connector.calls) != 1
    ):
        raise AssertionError("Topology drift while arming preserved claimed certainty.")

    changed_tick, changed_socket, _, _, _ = make_gdb_session(
        (gdb_u32_reply(100) + gdb_u32_reply(101),),
        process=process,
        connection_observer=exact_set,
    )
    expect_refusal(
        lambda: changed_tick.connect_resume_once(process),
        "did not remain at the connection-induced pause",
    )
    changed_status = changed_tick.status()
    if (
        changed_status.continue_attempts != 0
        or changed_status.pause_state_certain
        or automation.GDB_CONTINUE_PACKET in changed_socket.sent
    ):
        raise AssertionError("Early changing tick crossed the continue boundary.")

    late_pause_clock = FakeClock()

    def late_pause_set(
        observed_process: Any, expected_client: Any | None, call: int
    ) -> Any:
        if call == 4:
            late_pause_clock.value += 0.2
        return exact_set(observed_process, expected_client, call)

    late_pause, late_pause_socket, _, _, _ = make_gdb_session(
        (gdb_u32_reply(100) + gdb_u32_reply(100),),
        process=process,
        clock=late_pause_clock.clock,
        sleeper=late_pause_clock.sleep,
        connection_observer=late_pause_set,
    )
    expect_refusal(
        lambda: late_pause.connect_resume_once(process),
        "pause confirmation timed out",
    )
    if (
        late_pause.status().continue_attempts != 0
        or automation.GDB_CONTINUE_PACKET in late_pause_socket.sent
    ):
        raise AssertionError("Late equal pause evidence was accepted after deadline.")

    late_run_clock = FakeClock()

    def late_run_set(
        observed_process: Any, expected_client: Any | None, call: int
    ) -> Any:
        if call == 12:
            late_run_clock.value += 0.2
        return exact_set(observed_process, expected_client, call)

    late_run_transcript = paused_transcript + b"+" + gdb_u32_reply(101)
    late_run, late_run_socket, _, _, _ = make_gdb_session(
        (late_run_transcript,),
        process=process,
        clock=late_run_clock.clock,
        sleeper=late_run_clock.sleep,
        connection_observer=late_run_set,
    )
    expect_refusal(
        lambda: late_run.connect_resume_once(process), "did not prove RUNNING"
    )
    if (
        late_run.status().continue_attempts != 1
        or late_run.status().pause_state_certain
        or late_run_socket.sent.count(automation.GDB_CONTINUE_PACKET) != 1
    ):
        raise AssertionError("Late forward VBlank evidence entered RUNNING.")

    # status() serializes with the mutation lock.  It may wait for an operation,
    # but it cannot combine a fresh session state with a connected transport.
    status_session, _, _, _, _ = make_gdb_session(
        (), process=process, connection_observer=exact_set
    )
    started = threading.Event()
    finished = threading.Event()
    snapshots: list[Any] = []

    def read_status() -> None:
        started.set()
        snapshots.append(status_session.status())
        finished.set()

    status_session._operation_lock.acquire()
    thread = threading.Thread(target=read_status, daemon=True)
    thread.start()
    if not started.wait(timeout=1) or finished.wait(timeout=0.05):
        status_session._operation_lock.release()
        thread.join(timeout=1)
        raise AssertionError("GDB status did not serialize with an active mutation.")
    status_session._operation_lock.release()
    thread.join(timeout=1)
    if thread.is_alive() or snapshots != [
        automation.GdbSessionStatus("NEW", None, None, 0, 0, False, False)
    ]:
        raise AssertionError("Serialized GDB status returned an impossible snapshot.")
    expect_any_refusal(
        lambda: automation.GdbSessionStatus(
            "RUNNING", process, make_gdb_listener(), 0, 1, True, True
        )
    )


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
    refusal_reason = automation.UnavailableNativePadConfirmer.REFUSAL_REASON
    required_boundaries = (
        "static pad-frame address",
        "active-low bit mapping are pinned",
        "live-process use",
        "four-byte read atomicity",
        "exclusive persistent shared-session reader",
        "entire input plan",
    )
    if any(boundary not in refusal_reason for boundary in required_boundaries):
        raise AssertionError("Production pad refusal overclaims the static provenance.")
    if "PollingReadOnlyPadConfirmer" in automation.main.__code__.co_names:
        raise AssertionError("Production main wired the offline polling pad confirmer.")
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

    # Real native regression: a temp owned, ad-hoc-signed sleep probe survives
    # atomic pathname replacement between the fake key-down and owed fake
    # key-up.  Current macOS kills a copied Apple /bin/sleep before this gate,
    # so the purpose-built probe avoids an unrelated platform-signing race.  No
    # CGEvent is created or posted; only libproc/lsof read-only observation runs.
    with tempfile.TemporaryDirectory(prefix="id65-release-vnode-") as temp:
        root = pathlib.Path(temp).resolve()
        executable = root / "owned-sleep"
        replacement = root / "replacement-sleep"
        probe_source = (
            b"#include <unistd.h>\n"
            b"int main(void) { sleep(10); return 0; }\n"
        )
        compiled = subprocess.run(
            ["/usr/bin/clang", "-x", "c", "-", "-o", str(executable)],
            input=probe_source,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
            close_fds=True,
            timeout=30,
            env={"LC_ALL": "C", "PATH": "/usr/bin:/bin:/usr/sbin:/sbin"},
        )
        if compiled.returncode != 0 or compiled.stdout or compiled.stderr:
            raise AssertionError("Could not build the native release-vnode probe.")
        os.chmod(executable, 0o755)

        def ad_hoc_sign_and_verify(path: pathlib.Path) -> None:
            signed = subprocess.run(
                [
                    "/usr/bin/codesign",
                    "--force",
                    "--sign",
                    "-",
                    "--timestamp=none",
                    str(path),
                ],
                stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                check=False,
                close_fds=True,
                timeout=30,
                env={"LC_ALL": "C", "PATH": "/usr/bin:/bin:/usr/sbin:/sbin"},
            )
            if signed.returncode != 0:
                raise AssertionError("Could not ad-hoc-sign the native vnode probe.")
            verified = subprocess.run(
                ["/usr/bin/codesign", "--verify", "--strict", str(path)],
                stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                check=False,
                close_fds=True,
                timeout=30,
                env={"LC_ALL": "C", "PATH": "/usr/bin:/bin:/usr/sbin:/sbin"},
            )
            if verified.returncode != 0 or verified.stdout or verified.stderr:
                raise AssertionError("Ad-hoc vnode probe signature did not verify.")

        ad_hoc_sign_and_verify(executable)
        shutil.copyfile(executable, replacement)
        os.chmod(replacement, 0o755)
        ad_hoc_sign_and_verify(replacement)
        child = subprocess.Popen(
            [str(executable), "10"],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            close_fds=True,
        )
        try:
            child_baseline = None
            for _ in range(50):
                if child.poll() is not None:
                    raise AssertionError("Native vnode probe exited before readiness.")
                try:
                    child_baseline = observer.observe(child.pid)
                    break
                except automation.AutomationRefusal:
                    select.select([], [], [], 0.02)
            if child_baseline is None:
                raise AssertionError("Native vnode probe did not become observable.")
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
        test_input_timing_static_provenance_cross_check,
        test_canonical_plan_and_allowlist,
        test_pure_gdb_session_input_lifecycle_policy,
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
        test_strict_gdb_connection_set_parser_and_attestor,
        test_persistent_gdb_reader_fixed_packets_and_fragmentation,
        test_persistent_gdb_reader_rsp_refusal_matrix,
        test_persistent_gdb_reader_semantic_and_binding_refusals,
        test_one_shot_gdb_session_resume_and_shared_reads,
        test_gdb_runtime_session_sentinel_exact_and_refusals,
        test_exact_runtime_session_attestor_and_disc_binding,
        test_planned_space_pause_resume_lifecycle,
        test_targeted_key_transport_exclusive_capability,
        test_targeted_key_transport_claim_races_and_coordinator_validation,
        test_gdb_input_pulse_coordinator_exact_order,
        test_gdb_input_pulse_coordinator_failure_matrix,
        test_planned_space_transition_failure_matrix,
        test_one_shot_gdb_session_rollover_and_failure_matrix,
        test_gdb_session_ownership_deadline_and_status_boundaries,
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
