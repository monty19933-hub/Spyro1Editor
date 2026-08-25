#!/usr/bin/env python3
"""Deterministic offline smoke for the capture-only ID65 V3 coordinator."""

from __future__ import annotations

import copy
import contextlib
import dataclasses
import datetime as dt
import builtins
import hashlib
import importlib.util
import io
import json
import os
import pathlib
import pty
import re
import select
import shutil
import signal
import stat
import struct
import subprocess
import sys
import tempfile
import zlib
from typing import Any


TOOL_PATH = pathlib.Path(__file__).with_name(
    "Capture-SpyroId65V3RuntimeSession.py"
)
SPEC = importlib.util.spec_from_file_location("id65_v3_capture", TOOL_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("Cannot load the capture coordinator for offline smoke.")
capture = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = capture
SPEC.loader.exec_module(capture)

ACTUAL_DUCKSTATION_CALLS: list[tuple[Any, ...]] = []
_PRODUCTION_DUCKSTATION = os.path.realpath(
    TOOL_PATH.parent.parent / capture.DUCKSTATION_EXECUTABLE_RELATIVE_PATH
)


def _audit_no_production_duckstation(event: str, args: tuple[Any, ...]) -> None:
    if event != "subprocess.Popen" or not args:
        return
    executable = args[0]
    if isinstance(executable, (str, bytes, os.PathLike)):
        try:
            observed = os.path.realpath(os.fsdecode(executable))
        except (TypeError, ValueError):
            return
        if observed == _PRODUCTION_DUCKSTATION:
            ACTUAL_DUCKSTATION_CALLS.append(args)


sys.addaudithook(_audit_no_production_duckstation)


class FakeChild:
    def __init__(self, pid: int) -> None:
        self.pid = pid
        self.code: int | None = None
        self.signals: list[int] = []

    def poll(self) -> int | None:
        return self.code

    def wait(self, timeout: float | None = None) -> int:
        del timeout
        if self.code is None:
            raise AssertionError("Owned fake child was waited without a signal.")
        return self.code

    def send_signal(self, value: int) -> None:
        self.signals.append(value)
        self.code = 0


class ResistantFakeChild(FakeChild):
    def wait(self, timeout: float | None = None) -> int:
        if self.code is None:
            raise subprocess.TimeoutExpired("resistant-owned-child", timeout)
        return self.code

    def send_signal(self, value: int) -> None:
        self.signals.append(value)
        if value == signal.SIGKILL:
            self.code = -signal.SIGKILL


class FaultingReapChild(ResistantFakeChild):
    def __init__(
        self,
        pid: int,
        *,
        poll_faults: int = 0,
        send_faults: set[int] | None = None,
        wait_faults: int = 0,
        never_reap: bool = False,
    ) -> None:
        super().__init__(pid)
        self.poll_faults = poll_faults
        self.send_faults = set(send_faults or ())
        self.wait_faults = wait_faults
        self.never_reap = never_reap

    def poll(self) -> int | None:
        if self.poll_faults > 0:
            self.poll_faults -= 1
            raise OSError("injected owned-child poll failure")
        return super().poll()

    def wait(self, timeout: float | None = None) -> int:
        if self.wait_faults > 0:
            self.wait_faults -= 1
            raise KeyboardInterrupt("injected owned-child wait interruption")
        return super().wait(timeout)

    def send_signal(self, value: int) -> None:
        self.signals.append(value)
        if value in self.send_faults:
            raise OSError("injected owned-child signal failure")
        if value == signal.SIGKILL and not self.never_reap:
            self.code = -signal.SIGKILL


class FakeSource:
    def __init__(self, root: pathlib.Path) -> None:
        self.repository_root = root
        self.package_root = root / "fake-profile-package"
        self.external_home_root = self.package_root / "home"
        self.profile_root = (
            self.external_home_root / "Library/Application Support/DuckStation"
        )
        self.profile_root.mkdir(parents=True)
        self.executable_path = (
            self.package_root
            / "DuckStation-ID65-Unattended-v1.app/Contents/MacOS/DuckStation"
        )
        self.executable_path.parent.mkdir(parents=True)
        self.executable_path.write_bytes(b"offline fake executable")
        self.cache_path = (
            self.profile_root / capture.ALLOWED_PROCESS_CACHE_BIN_RELATIVE_PATH
        )
        self.cache_path.parent.mkdir()
        self.cache_path.write_bytes(b"offline fake Vulkan shader cache")
        self.cache_path.chmod(0o644)
        self.cue_path = root / "candidate.cue"
        self.bin_path = root / "candidate.bin"
        self.cue_path.write_bytes(b"offline fake cue")
        self.bin_path.write_bytes(b"offline fake bin")
        self.refuse = False
        self.validate_calls = 0
        self.snapshot_calls = 0
        self.prelaunch_calls = 0
        self.prelaunch_probe: Any | None = None
        self.cue_identity_path_override: str | None = None
        self.cue_hash_override: str | None = None
        self.pins = capture._offline_capture_pins("a" * 64)

    def validate_all(self) -> dict[str, str]:
        self.validate_calls += 1
        if self.refuse:
            raise capture.CaptureRefusal("injected source drift")
        return dict(self.pins)

    def candidate_identities(self) -> tuple[Any, Any]:
        cue = capture.FileIdentity(
            self.cue_identity_path_override or os.path.realpath(self.cue_path),
            10,
            11,
            123,
            self.cue_hash_override or capture.EXPECTED_CANDIDATE_CUE_SHA256,
        )
        bin_file = capture.FileIdentity(
            os.path.realpath(self.bin_path),
            10,
            12,
            456,
            capture.EXPECTED_CANDIDATE_BIN_SHA256,
        )
        return cue, bin_file

    def executable_identity(self) -> Any:
        return capture.FileIdentity(
            os.path.realpath(self.executable_path),
            20,
            21,
            20,
            capture.EXPECTED_DUCKSTATION_SHA256,
        )

    def capture_profile(
        self,
        *,
        phase: str,
        session_id: str,
        process_instance_id: str,
        captured_at_utc: str | None = None,
    ) -> dict[str, Any]:
        if self.refuse:
            raise capture.CaptureRefusal("injected profile drift")
        self.snapshot_calls += 1
        entries = [
            {
                "byteLength": capture.EXPECTED_BIOS_SIZE,
                "relativePath": capture.BIOS_MANIFEST_PATH,
                "sha256": capture.EXPECTED_BIOS_SHA256,
            },
            {
                "byteLength": 4,
                "relativePath": "playtime.dat",
                "sha256": "c" * 64,
            },
            {
                "byteLength": capture.EXPECTED_SETTINGS_SIZE,
                "relativePath": "settings.ini",
                "sha256": capture.EXPECTED_SETTINGS_SHA256,
            },
        ]
        entries.sort(key=lambda item: item["relativePath"])
        return {
            "capturedAtUtc": captured_at_utc or "1970-01-01T00:00:00.000Z",
            "duckStationExecutableSha256": capture.EXPECTED_DUCKSTATION_SHA256,
            "handoffReceiptSha256": capture.EXPECTED_HANDOFF_RECEIPT_SHA256,
            "appContentManifestSha256": capture.EXPECTED_APP_CONTENT_MANIFEST_SHA256,
            "appOutputTreeManifestSha256": capture.EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
            "packageEnvelopeSha256": capture.EXPECTED_PACKAGE_ENVELOPE_SHA256,
            "externalHomeSettingsSha256": capture.EXPECTED_SETTINGS_SHA256,
            "externalHomeBiosSha256": capture.EXPECTED_BIOS_SHA256,
            "hostNormalSettingsSentinelId": capture.NORMAL_SETTINGS_RESOURCE_ID,
            "hostNormalSettingsSentinelSha256": capture.EXPECTED_NORMAL_SETTINGS_SHA256,
            "dataFiles": entries,
            "format": capture.PROFILE_FORMAT,
            "formatVersion": capture.PROFILE_FORMAT_VERSION,
            "phase": phase,
            "processInstanceId": process_instance_id,
            "profileId": capture.PROFILE_ID,
            "sessionId": session_id,
            "snapshotId": f"{session_id}.profile-{phase}",
        }

    def verify_prelaunch_once(self) -> dict[str, Any]:
        if self.prelaunch_probe is not None:
            self.prelaunch_probe()
        self.prelaunch_calls += 1
        if self.prelaunch_calls != 1:
            raise capture.CaptureRefusal("injected repeated prelaunch proof")
        return capture._offline_prelaunch_proof()


class FakeBoundary:
    def __init__(self, root: pathlib.Path, source: FakeSource) -> None:
        self.repository_root = root
        self.source = source
        self.base = dt.datetime(2026, 8, 10, 20, 0, tzinfo=dt.timezone.utc)
        self.monotonic = 1_000_000
        self.environment_ok = True
        self.permission_ok = True
        self.locked = False
        self.running = False
        self.exited = False
        self.listener_before = False
        self.child_pid = 9000
        self.launch_count = 0
        self.frame_count = 0
        self.prompts: list[str] = []
        self.announcements: list[str] = []
        self.actions: list[tuple[str, str]] = []
        self.readiness_failures_remaining = 0
        self.readiness_unsafe_windows: list[
            tuple[dt.datetime, dt.datetime]
        ] = []
        self.children: list[FakeChild] = []
        self.watcher_stream: Any | None = None
        self.watcher_started_at: dt.datetime | None = None
        self.watcher_next_elapsed_ms = 0
        self.watcher_trailer_written = False
        self.fall_at: dt.datetime | None = None
        self.respawn_at: dt.datetime | None = None
        self.current_lives = 4
        self.deadline_calls: list[int] = []
        self.frame_deadline_calls: list[int] = []
        self.expire_on_deadline_call: int | None = None
        self.expire_during_frame = False
        self.expire_during_watcher_wait = False
        self._expire_on_next_clock = False
        self._active_recording_deadline: int | None = None
        self.fail_after_recording_start = False
        self.fail_watcher_handoff = False
        self.fail_recording_handoff = False
        self._fail_next_clock = False
        self.late_recovery_after_respawn_marker = False
        self.candidate_load_announced_at: dt.datetime | None = None
        self.process = self._make_process(4242, self.base)

    def _make_process(
        self, pid: int, started: dt.datetime
    ) -> Any:
        executable = self.source.executable_identity()
        cue = os.path.realpath(self.source.cue_path)
        bin_path = os.path.realpath(self.source.bin_path)
        start_seconds = int(started.timestamp())
        start_microseconds = started.microsecond
        return capture.ProcessObservation(
            pid=pid,
            parent_pid=1,
            start_seconds=start_seconds,
            start_microseconds=start_microseconds,
            process_started_at_utc=capture.process_start_utc(
                start_seconds, start_microseconds
            ),
            executable=executable,
            argv=(os.path.realpath(self.source.executable_path), cue),
            argv_nul_sha256=hashlib.sha256(
                (
                    os.path.realpath(self.source.executable_path)
                    + "\0"
                    + cue
                    + "\0"
                ).encode()
            ).hexdigest(),
            environment_nul_sha256=hashlib.sha256(
                (
                    "HOME=" + os.path.realpath(self.source.external_home_root) + "\0"
                ).encode()
            ).hexdigest(),
            external_home_real_path=os.path.realpath(self.source.external_home_root),
            open_files=(
                capture.OpenFileObservation("21r", 10, 12, 456, 1, bin_path),
            ),
        )

    def clock(self) -> Any:
        if self._fail_next_clock:
            self._fail_next_clock = False
            raise capture.CaptureRefusal(
                "injected failure immediately after recording child creation"
            )
        if self._expire_on_next_clock:
            self._expire_on_next_clock = False
            if self._active_recording_deadline is None:
                raise AssertionError("Watcher expiry lacks the shared recording deadline.")
            delta = self._active_recording_deadline - self.monotonic
            if delta > 0:
                self.base += dt.timedelta(microseconds=delta)
                self.monotonic = self._active_recording_deadline
            return capture.ClockSample(capture.format_utc(self.base), self.monotonic)
        self.monotonic += 1_000
        self.base += dt.timedelta(milliseconds=1)
        return capture.ClockSample(capture.format_utc(self.base), self.monotonic)

    def advance(self, microseconds: int) -> Any:
        target = self.base + dt.timedelta(microseconds=microseconds)
        self._append_live_watcher_samples(target)
        self.monotonic += microseconds
        self.base = target
        return capture.ClockSample(capture.format_utc(self.base), self.monotonic)

    def boot_identity(self) -> str:
        return "d" * 64

    def require_capture_environment(self) -> None:
        if not self.environment_ok or not self.permission_ok or self.locked:
            raise capture.CaptureRefusal("injected unavailable capture environment")

    def duckstation_processes(self) -> list[Any]:
        return [self.process] if self.running and not self.exited else []

    def listener_pids(self) -> set[int]:
        if self.listener_before:
            return {999}
        return {self.process.pid} if self.running and not self.exited else set()

    def wait_for_duckstation(self, cue_real_path: str) -> Any:
        if cue_real_path != os.path.realpath(self.source.cue_path):
            raise AssertionError("Coordinator requested the wrong fake CUE.")
        self.launch_count += 1
        self.process = self._make_process(4241 + self.launch_count, self.base)
        self.running = True
        self.exited = False
        self.fall_at = None
        self.respawn_at = None
        self.candidate_load_announced_at = None
        self.current_lives = 4
        return self.process

    def wait_for_process_exit(self, process: Any) -> Any:
        if process != self.process:
            raise AssertionError("Coordinator waited for the wrong fake process.")
        self.exited = True
        return self.advance(1_000_000)

    def observe_window(self, pid: int) -> Any:
        if pid != self.process.pid:
            raise AssertionError("Coordinator observed the wrong fake PID.")
        return capture.WindowObservation(808, pid, 10, 20, 1280, 720)

    def confirm(self, prompt: str) -> Any:
        if not isinstance(prompt, str) or not prompt:
            raise AssertionError("Coordinator emitted an empty prompt.")
        self.prompts.append(prompt)
        self.actions.append(("prompt", prompt))
        target = self.base + dt.timedelta(seconds=30)
        if (
            prompt.startswith("GAMEPLAY READINESS")
            and self.readiness_failures_remaining > 0
        ):
            self.readiness_failures_remaining -= 1
            self.readiness_unsafe_windows.append(
                (target, target + dt.timedelta(milliseconds=250))
            )
        if "Two-operator fall authorization" in prompt:
            self.fall_at = target
        if "same-pad respawn" in prompt:
            if self.fall_at is None:
                raise AssertionError("Respawn confirmation preceded its fall cue.")
            self.respawn_at = (
                target + dt.timedelta(milliseconds=100)
                if self.late_recovery_after_respawn_marker
                else self.fall_at + dt.timedelta(seconds=1)
            )
        if (
            prompt.startswith("MINIMUM MET")
            and "Deliberately die once in Town Square" in prompt
        ):
            self.current_lives = 3
        sample = self.advance(30_000_000)
        self.advance(100_000)
        return sample

    def confirm_before_deadline(
        self, prompt: str, deadline_monotonic_microseconds: int
    ) -> Any:
        self._active_recording_deadline = deadline_monotonic_microseconds
        self.deadline_calls.append(deadline_monotonic_microseconds)
        if (
            self.expire_on_deadline_call is not None
            and len(self.deadline_calls) == self.expire_on_deadline_call
        ):
            delta = deadline_monotonic_microseconds - self.monotonic
            if delta > 0:
                self.base += dt.timedelta(microseconds=delta)
                self.monotonic = deadline_monotonic_microseconds
            raise capture.CaptureRefusal(
                "Direct-window recording reached its 30-minute deadline before "
                "operator confirmation."
            )
        if self.monotonic >= deadline_monotonic_microseconds:
            raise capture.CaptureRefusal(
                "Direct-window recording reached its 30-minute deadline before "
                "operator confirmation."
            )
        sample = self.confirm(prompt)
        if self.expire_during_watcher_wait and len(self.deadline_calls) == 1:
            self._expire_on_next_clock = True
        if sample.monotonic_microseconds > deadline_monotonic_microseconds:
            raise capture.CaptureRefusal(
                "Direct-window recording reached its 30-minute deadline before "
                "operator confirmation."
            )
        return sample

    def fresh_confirm_before_deadline(
        self, prompt: str, deadline_monotonic_microseconds: int
    ) -> Any:
        self.actions.append(("fresh-input-drained", prompt))
        return self.confirm_before_deadline(
            prompt,
            deadline_monotonic_microseconds,
        )

    def announce(self, message: str) -> None:
        if not isinstance(message, str) or not message:
            raise AssertionError("Coordinator emitted an empty announcement.")
        self.require_capture_environment()
        self.announcements.append(message)
        self.actions.append(("announce", message))
        if message.startswith("AUTOMATIC READINESS ARMED"):
            self.advance(3_000_000)
        if message.startswith(
            "READINESS ACCEPTED — ENTER CODE NOW for candidate-primary-edge-0:"
        ):
            self.candidate_load_announced_at = self.base
            self.advance(13_000_000)
        self.require_capture_environment()

    def wait_for_fresh_return_after(
        self,
        started: Any,
        minimum_microseconds: int,
        eligible_prompt: str,
        deadline_monotonic_microseconds: int | None = None,
    ) -> Any:
        remaining = (
            started.monotonic_microseconds
            + minimum_microseconds
            - self.monotonic
        )
        if remaining > 0:
            if (
                deadline_monotonic_microseconds is not None
                and self.monotonic + remaining >= deadline_monotonic_microseconds
            ):
                raise capture.CaptureRefusal(
                    "Direct-window recording reached its 30-minute deadline during "
                    "a timed operator action."
                )
            self.advance(remaining)
        if deadline_monotonic_microseconds is None:
            return self.confirm("MINIMUM MET — " + eligible_prompt)
        return self.confirm_before_deadline(
            "MINIMUM MET — " + eligible_prompt,
            deadline_monotonic_microseconds,
        )

    def start_watcher(
        self,
        stdout: Any,
        owned_children: list[tuple[str, Any]],
    ) -> tuple[Any, Any]:
        self.watcher_stream = stdout
        self.watcher_started_at = None
        self.watcher_next_elapsed_ms = 0
        self.watcher_trailer_written = False
        stdout.write(capture.WATCHER_LIVE_PREAMBLE.encode("utf-8"))
        stdout.flush()
        child = FakeChild(self.child_pid)
        self.child_pid += 1
        self.children.append(child)
        owned_children.append(("watcher", child))
        if self.fail_watcher_handoff:
            raise capture.CaptureRefusal("injected watcher ownership handoff failure")
        return child, io.BytesIO()

    def wait_for_watcher_connection(self, child: Any, watcher_path: pathlib.Path) -> Any:
        if child not in self.children or watcher_path.name != "watcher-stdout.log":
            raise AssertionError("Coordinator did not own the fake watcher.")
        return self.advance(1_000_000)

    def start_recording(
        self,
        window: Any,
        session_path: pathlib.Path,
        destination_leaf: str,
        owned_children: list[tuple[str, Any]],
    ) -> Any:
        if window.window_id != 808:
            raise AssertionError("Coordinator recorded the wrong fake window.")
        if destination_leaf != capture.SCREEN_RECORDING_LEAF:
            raise AssertionError("Coordinator selected the wrong recording leaf.")
        self.watcher_started_at = self.base
        self.watcher_next_elapsed_ms = 0
        self._append_live_watcher_samples(self.base)
        with capture.AnchoredStaging.create_new_file(
            session_path, destination_leaf
        ) as stream:
            stream.write(iso_bmff_fixture(f"recording-{self.process.pid}"))
            stream.flush()
            os.fsync(stream.fileno())
        child = FakeChild(self.child_pid)
        self.child_pid += 1
        self.children.append(child)
        owned_children.append(("recorder", child))
        if self.fail_recording_handoff:
            raise capture.CaptureRefusal("injected recorder ownership handoff failure")
        if self.fail_after_recording_start:
            self._fail_next_clock = True
        return child

    def capture_frame(
        self,
        window: Any,
        session_path: pathlib.Path,
        destination_leaf: str,
        deadline_monotonic_microseconds: int,
    ) -> None:
        self.frame_deadline_calls.append(deadline_monotonic_microseconds)
        if self.expire_during_frame:
            delta = deadline_monotonic_microseconds - self.monotonic
            if delta > 0:
                self.base += dt.timedelta(microseconds=delta)
                self.monotonic = deadline_monotonic_microseconds
            raise capture.CaptureRefusal(
                "Direct-window recording reached its 30-minute deadline during "
                "frame capture."
            )
        if self.monotonic >= deadline_monotonic_microseconds:
            raise capture.CaptureRefusal(
                "Direct-window recording reached its 30-minute deadline before "
                "frame capture."
            )
        if window.window_id != 808:
            raise AssertionError("Coordinator framed the wrong fake window.")
        if destination_leaf not in capture.CAPTURE_FRAME_LEAVES:
            raise AssertionError("Coordinator selected an unapproved frame leaf.")
        self.frame_count += 1
        self.actions.append(("frame", str(self.frame_count)))
        with capture.AnchoredStaging.create_new_file(
            session_path, destination_leaf
        ) as stream:
            stream.write(png_fixture(self.frame_count))
            stream.flush()
            os.fsync(stream.fileno())

    def stop_owned_child(
        self,
        child: Any,
        label: str,
        deadline_monotonic_microseconds: int | None = None,
    ) -> Any:
        del deadline_monotonic_microseconds
        if child not in self.children or label not in {
            "pinned watcher",
            "screen recorder",
            "watcher",
            "recorder",
        }:
            raise AssertionError("Coordinator signalled an unowned fake child.")
        requested = self.advance(1_000_000)
        if label == "pinned watcher":
            self._write_watcher_log()
        child.send_signal(2)
        if child.wait() != 0:
            raise AssertionError("Fake child failed to stop.")
        return capture.ChildStop(requested, self.advance(1_000_000))

    def ensure_owned_child_reaped(
        self, child: Any, label: str
    ) -> BaseException | None:
        if child not in self.children or label not in {"watcher", "recorder"}:
            raise AssertionError("Coordinator reaped an unowned fake child.")
        if child.poll() is None:
            child.send_signal(signal.SIGINT)
            child.wait()
        if child.poll() is None:
            raise capture.CaptureRefusal("Fake owned child could not be reaped.")
        return None

    def _write_watcher_log(self) -> None:
        if self.watcher_stream is None or self.watcher_started_at is None:
            raise AssertionError("Fake watcher log was requested before startup.")
        self._append_live_watcher_samples(self.base)
        if self.watcher_trailer_written:
            raise AssertionError("Fake watcher trailer was written twice.")
        self.watcher_stream.write(
            capture.WATCHER_CLEAN_TRAILER[1:].encode("utf-8")
        )
        self.watcher_stream.flush()
        self.watcher_trailer_written = True

    def _append_live_watcher_samples(self, through: dt.datetime) -> None:
        if (
            self.watcher_stream is None
            or self.watcher_started_at is None
            or self.watcher_trailer_written
        ):
            return
        span_ms = int((through - self.watcher_started_at).total_seconds() * 1000)
        fall_edge_at: dt.datetime | None = None
        if self.fall_at is not None:
            threshold_ms = int(
                (
                    self.fall_at
                    + dt.timedelta(milliseconds=100)
                    - self.watcher_started_at
                ).total_seconds()
                * 1000
            )
            edge_elapsed = ((threshold_ms + 99) // 100) * 100
            fall_edge_at = self.watcher_started_at + dt.timedelta(
                milliseconds=edge_elapsed
            )
        lines: list[str] = []
        for elapsed in range(self.watcher_next_elapsed_ms, span_ms + 1, 100):
            ordinal = elapsed // 100
            at = self.watcher_started_at + dt.timedelta(milliseconds=elapsed)
            if elapsed < 400:
                lives, state, z_value = 0, 13, 0
            elif elapsed < 800:
                lives, state, z_value = 4, 1, 2048
            elif any(
                unsafe_start <= at <= unsafe_end
                for unsafe_start, unsafe_end in self.readiness_unsafe_windows
            ):
                lives, state, z_value = 4, 13, 2048
            elif fall_edge_at is not None and at >= fall_edge_at:
                lives, state, z_value = 3, 4, 0
            elif self.candidate_load_announced_at is not None:
                load_elapsed_ms = int(
                    (at - self.candidate_load_announced_at).total_seconds() * 1000
                )
                if 1_000 <= load_elapsed_ms < 5_000:
                    lives, state, z_value = 4, 3, 2_048
                elif 5_000 <= load_elapsed_ms < 10_000:
                    lives, state, z_value = 4, 1, 2_048
                else:
                    lives, state, z_value = self.current_lives, 0, 2_048
            else:
                lives, state, z_value = self.current_lives, 0, 2048
            if (
                self.respawn_at is not None
                and at >= self.respawn_at - dt.timedelta(milliseconds=100)
            ):
                lives, state, z_value = 3, 0, 2048
            label = (
                "gameplay/non-death"
                if state == 0
                else "death-respawn"
                if state == 4
                else "game-over"
                if state == 5
                else f"other-{state}"
            )
            marker = "YES" if z_value < 0x400 else "no"
            timestamp = capture.format_utc(at)
            lines.append(
                f"{timestamp} sample={ordinal} elapsed-ms={elapsed} lives={lives} "
                f"death-state={state}({label}) player-Z={z_value} "
                f"[Z<0x400:{marker}] raw="
                f"{lives.to_bytes(4, 'little').hex()}/"
                f"{state.to_bytes(4, 'little').hex()}/"
                f"{z_value.to_bytes(4, 'little', signed=True).hex()}\n"
            )
        if lines:
            self.watcher_stream.write("".join(lines).encode("utf-8"))
            self.watcher_stream.flush()
            self.watcher_next_elapsed_ms = (span_ms // 100 + 1) * 100


class LateReadbackFailureCoordinator(capture.CaptureCoordinator):
    def _load_run(
        self, run_id: str, current_pins: Any | None = None
    ) -> Any:
        if self.staging.prefix.exists() and any(
            self.staging.prefix.rglob("capture-event-journal.json")
        ):
            raise capture.CaptureRefusal("injected post-commit readback failure")
        return super()._load_run(run_id, current_pins)


class CreateSealFailureJournal(capture.JournalWriter):
    def _open_provisional_seal(self, directory_fd: int) -> int:
        del directory_fd
        raise capture.CaptureRefusal("injected provisional seal create failure")


class PartialWriteSealFailureJournal(capture.JournalWriter):
    @staticmethod
    def _write_provisional_seal(file_fd: int, data: bytes) -> None:
        written = os.write(file_fd, data[: max(1, len(data) // 2)])
        if written <= 0:
            raise AssertionError("Injected partial seal write wrote no bytes.")
        raise capture.CaptureRefusal("injected provisional seal partial write failure")


class FsyncSealFailureJournal(capture.JournalWriter):
    @staticmethod
    def _fsync_provisional_seal(file_fd: int) -> None:
        del file_fd
        raise capture.CaptureRefusal("injected provisional seal fsync failure")


class DirectoryFsyncSealFailureJournal(capture.JournalWriter):
    @staticmethod
    def _fsync_provisional_directory(directory_fd: int) -> None:
        del directory_fd
        raise capture.CaptureRefusal(
            "injected provisional seal directory fsync failure"
        )


class ReturnSealFailureJournal(capture.JournalWriter):
    def commit_projected_seal(self, journal: dict[str, Any]) -> Any:
        super().commit_projected_seal(journal)
        raise capture.CaptureRefusal("injected provisional seal return failure")


class RollbackSealFailureJournal(capture.JournalWriter):
    def commit_projected_seal(self, journal: dict[str, Any]) -> Any:
        super().commit_projected_seal(journal)
        raise capture.CaptureRefusal("injected failure after provisional seal creation")

    def _unlink_provisional_seal(self, directory_fd: int) -> None:
        del directory_fd
        raise capture.CaptureRefusal("injected provisional seal rollback failure")


class BeforeLiveAppendFailureJournal(capture.JournalWriter):
    @staticmethod
    def _before_terminal_append(file_fd: int, data: bytes) -> None:
        del file_fd, data
        raise capture.CaptureRefusal("injected live terminal prewrite failure")


class PartialLiveAppendFailureJournal(capture.JournalWriter):
    @staticmethod
    def _write_terminal_line(file_fd: int, data: bytes) -> None:
        written = os.write(file_fd, data[: max(1, len(data) // 2)])
        if written <= 0:
            raise AssertionError("Injected partial live terminal write wrote no bytes.")
        raise capture.CaptureRefusal("injected live terminal partial write failure")


class LiveAppendFsyncFailureJournal(capture.JournalWriter):
    @staticmethod
    def _fsync_terminal_line(file_fd: int) -> None:
        del file_fd
        raise capture.CaptureRefusal("injected live terminal fsync failure")


class LiveAppendReturnFailureJournal(capture.JournalWriter):
    def _before_terminal_logical_commit(self) -> None:
        raise capture.CaptureRefusal("injected live terminal precommit return failure")


class LiveAppendKeyboardInterruptJournal(capture.JournalWriter):
    def _before_terminal_logical_commit(self) -> None:
        raise KeyboardInterrupt("injected post-readback asynchronous interruption")


class CombinedDurableCommitFaultJournal(capture.JournalWriter):
    rollback_tail_calls = 0
    unlink_calls = 0

    def _before_terminal_logical_commit(self) -> None:
        raise capture.CaptureRefusal("injected combined durable commit fault")

    @staticmethod
    def _rollback_terminal_tail(live_fd: int, precommit_size: int) -> None:
        del live_fd, precommit_size
        CombinedDurableCommitFaultJournal.rollback_tail_calls += 1
        raise capture.CaptureRefusal("injected terminal rollback failure")

    def _unlink_provisional_seal(self, directory_fd: int) -> None:
        del directory_fd
        type(self).unlink_calls += 1
        raise capture.CaptureRefusal("injected seal rollback failure")


class CombinedMismatchCleanupFailureJournal(PartialLiveAppendFailureJournal):
    @staticmethod
    def _rollback_terminal_tail(live_fd: int, precommit_size: int) -> None:
        del live_fd, precommit_size
        raise capture.CaptureRefusal("injected mismatched terminal rollback failure")

    def _unlink_provisional_seal(self, directory_fd: int) -> None:
        del directory_fd
        raise capture.CaptureRefusal("injected mismatched seal rollback failure")


class PostReadbackMutationCleanupFailureJournal(capture.JournalWriter):
    mutation_target = "live"

    def _before_terminal_logical_commit(self) -> None:
        if self.mutation_target == "live":
            live_fd = os.open(
                self.directory / "capture-events-live.ndjson",
                os.O_RDWR | getattr(os, "O_NOFOLLOW", 0),
            )
            try:
                original = os.pread(live_fd, 1, 0)
                if len(original) != 1:
                    raise AssertionError("Live mutation fixture found no byte.")
                os.pwrite(live_fd, b"[" if original != b"[" else b"{", 0)
                os.fsync(live_fd)
            finally:
                os.close(live_fd)
        elif self.mutation_target == "seal":
            if self._provisional_file_fd is None:
                raise AssertionError("Seal mutation fixture lacks held seal fd.")
            original = os.pread(self._provisional_file_fd, 1, 0)
            if len(original) != 1:
                raise AssertionError("Seal mutation fixture found no byte.")
            os.pwrite(
                self._provisional_file_fd,
                b"[" if original != b"[" else b"{",
                0,
            )
            os.fsync(self._provisional_file_fd)
        elif self.mutation_target == "seal-path":
            if (
                self._provisional_directory_fd is None
                or self._projected_seal_bytes is None
            ):
                raise AssertionError("Seal path fixture lacks provisional authority.")
            os.unlink(
                self._SEALED_LEAF,
                dir_fd=self._provisional_directory_fd,
            )
            replacement_fd = os.open(
                self._SEALED_LEAF,
                os.O_WRONLY
                | os.O_CREAT
                | os.O_EXCL
                | getattr(os, "O_NOFOLLOW", 0),
                0o600,
                dir_fd=self._provisional_directory_fd,
            )
            try:
                replacement = b"[" + self._projected_seal_bytes[1:]
                if os.write(replacement_fd, replacement) != len(replacement):
                    raise AssertionError("Seal path replacement write was incomplete.")
                os.fsync(replacement_fd)
            finally:
                os.close(replacement_fd)
            os.fsync(self._provisional_directory_fd)
        else:
            raise AssertionError("Unknown post-readback mutation target.")
        raise capture.CaptureRefusal(
            f"injected post-readback {self.mutation_target} mutation"
        )

    @staticmethod
    def _rollback_terminal_tail(live_fd: int, precommit_size: int) -> None:
        del live_fd, precommit_size
        raise capture.CaptureRefusal("injected mutation terminal rollback failure")

    def _unlink_provisional_seal(self, directory_fd: int) -> None:
        del directory_fd
        raise capture.CaptureRefusal("injected mutation seal rollback failure")


class PostReadbackLiveMutationJournal(PostReadbackMutationCleanupFailureJournal):
    mutation_target = "live"


class PostReadbackSealMutationJournal(PostReadbackMutationCleanupFailureJournal):
    mutation_target = "seal"


class PostReadbackSealPathMutationJournal(PostReadbackMutationCleanupFailureJournal):
    mutation_target = "seal-path"


class PostReadbackRestoreMutationJournal(capture.JournalWriter):
    mutation_target = "live"

    def _before_terminal_logical_commit(self) -> None:
        if self.mutation_target == "seal-path":
            if (
                self._provisional_directory_fd is None
                or self._projected_seal_bytes is None
            ):
                raise AssertionError("Restored seal path fixture lacks authority.")
            temporary_leaf = "capture-event-journal.original.tmp"
            os.rename(
                self._SEALED_LEAF,
                temporary_leaf,
                src_dir_fd=self._provisional_directory_fd,
                dst_dir_fd=self._provisional_directory_fd,
            )
            replacement_fd = os.open(
                self._SEALED_LEAF,
                os.O_WRONLY
                | os.O_CREAT
                | os.O_EXCL
                | getattr(os, "O_NOFOLLOW", 0),
                0o600,
                dir_fd=self._provisional_directory_fd,
            )
            try:
                if os.write(
                    replacement_fd, self._projected_seal_bytes
                ) != len(self._projected_seal_bytes):
                    raise AssertionError(
                        "Restored seal path replacement write was incomplete."
                    )
            finally:
                os.close(replacement_fd)
            os.unlink(
                self._SEALED_LEAF,
                dir_fd=self._provisional_directory_fd,
            )
            os.rename(
                temporary_leaf,
                self._SEALED_LEAF,
                src_dir_fd=self._provisional_directory_fd,
                dst_dir_fd=self._provisional_directory_fd,
            )
            raise capture.CaptureRefusal(
                "injected restored seal path post-readback mutation"
            )
        if self.mutation_target == "live":
            path = self.directory / "capture-events-live.ndjson"
            fd = os.open(path, os.O_RDWR | getattr(os, "O_NOFOLLOW", 0))
        else:
            if self._provisional_file_fd is None:
                raise AssertionError("Restored seal fixture lacks held fd.")
            fd = self._provisional_file_fd
        should_close = self.mutation_target == "live"
        try:
            original = os.pread(fd, 1, 0)
            if len(original) != 1:
                raise AssertionError("Restored mutation fixture found no byte.")
            changed = b"[" if original != b"[" else b"{"
            os.pwrite(fd, changed, 0)
            os.pwrite(fd, original, 0)
        finally:
            if should_close:
                os.close(fd)
        raise capture.CaptureRefusal(
            f"injected restored {self.mutation_target} post-readback mutation"
        )


class PostReadbackRestoreLiveMutationJournal(PostReadbackRestoreMutationJournal):
    mutation_target = "live"


class PostReadbackRestoreSealMutationJournal(PostReadbackRestoreMutationJournal):
    mutation_target = "seal"


class PostReadbackRestoreSealPathMutationJournal(
    PostReadbackRestoreMutationJournal
):
    mutation_target = "seal-path"


class ResolverFsyncFailureJournal(capture.JournalWriter):
    @staticmethod
    def _fsync_resolved_seal(file_fd: int) -> None:
        del file_fd
        raise capture.CaptureRefusal("injected resolver seal fsync failure")


class CloseFaultJournal(capture.JournalWriter):
    last_instance: "CloseFaultJournal | None" = None

    def __init__(self, *args: Any, **kwargs: Any) -> None:
        super().__init__(*args, **kwargs)
        self.close_calls: list[str] = []
        type(self).last_instance = self

    def _close_live_stream(self) -> None:
        self.close_calls.append("live")
        super()._close_live_stream()
        raise OSError("injected live close failure")

    def _close_provisional_file(self, file_fd: int) -> None:
        self.close_calls.append("file")
        super()._close_provisional_file(file_fd)
        raise OSError("injected provisional file close failure")

    def _close_provisional_directory(self, directory_fd: int) -> None:
        self.close_calls.append("directory")
        super()._close_provisional_directory(directory_fd)
        raise OSError("injected provisional directory close failure")


class PrecommitCloseFaultJournal(RollbackSealFailureJournal):
    last_instance: "PrecommitCloseFaultJournal | None" = None

    def __init__(self, *args: Any, **kwargs: Any) -> None:
        super().__init__(*args, **kwargs)
        self.close_calls: list[str] = []
        type(self).last_instance = self

    def _close_live_stream(self) -> None:
        self.close_calls.append("live")
        super()._close_live_stream()
        raise OSError("injected precommit live close failure")

    def _close_provisional_file(self, file_fd: int) -> None:
        self.close_calls.append("file")
        super()._close_provisional_file(file_fd)
        raise OSError("injected precommit file close failure")

    def _close_provisional_directory(self, directory_fd: int) -> None:
        self.close_calls.append("directory")
        super()._close_provisional_directory(directory_fd)
        raise OSError("injected precommit directory close failure")


class FaultingExitLease:
    def __init__(self, error: BaseException) -> None:
        self.error = error
        self.entered = False
        self.exited = False

    def __enter__(self) -> "FaultingExitLease":
        self.entered = True
        return self

    def __exit__(self, exc_type: Any, exc: Any, traceback: Any) -> None:
        del exc_type, exc, traceback
        self.exited = True
        raise self.error


class PostReturnInterruptCoordinator(capture.CaptureCoordinator):
    def _capture_session_owned(self, session_id: str, **kwargs: Any) -> str:
        super()._capture_session_owned(session_id, **kwargs)
        raise KeyboardInterrupt("injected committed return/store interruption")


class SpawnBoundary(capture.RealMacBoundary):
    def __init__(
        self,
        root: pathlib.Path,
        *,
        signal_value: int | None,
    ) -> None:
        super().__init__(root)
        self.signal_value = signal_value
        self.observed_masks: list[set[signal.Signals]] = []

    def _after_owned_child_spawn(self, child: Any, label: str) -> None:
        del child, label
        self.observed_masks.append(
            signal.pthread_sigmask(signal.SIG_BLOCK, ())
        )
        if self.signal_value is None:
            raise KeyboardInterrupt("injected after Popen before registration")
        os.kill(os.getpid(), self.signal_value)


def png_fixture(seed: int = 0) -> bytes:
    signature = b"\x89PNG\r\n\x1a\n"

    def chunk(kind: bytes, data: bytes) -> bytes:
        return (
            struct.pack(">I", len(data))
            + kind
            + data
            + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
        )

    ihdr = struct.pack(">IIBBBBB", 1, 1, 8, 2, 0, 0, 0)
    pixel = bytes((0, seed & 0xFF, (seed * 3) & 0xFF, (seed * 7) & 0xFF))
    return signature + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(pixel)) + chunk(b"IEND", b"")


def iso_bmff_fixture(seed: str = "fixture") -> bytes:
    def box(kind: bytes, payload: bytes) -> bytes:
        return struct.pack(">I", len(payload) + 8) + kind + payload

    payload = seed.encode("ascii")
    return box(b"ftyp", b"qt  \0\0\0\0qt  ") + box(b"moov", b"m" + payload) + box(b"mdat", b"d" + payload)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def expect_refusal(action: Any, contains: str | None = None) -> None:
    try:
        action()
    except capture.CaptureRefusal as exc:
        if contains is not None and contains not in str(exc):
            raise AssertionError(
                f"Refusal {exc!s} did not contain expected text {contains!r}."
            ) from exc
        return
    raise AssertionError("Expected fail-closed CaptureRefusal.")


def run_shared_coordinator_authority_regressions() -> int:
    automation_path = TOOL_PATH.with_name(
        "Automate-SpyroId65V3RuntimeSession.py"
    )
    automation_spec = importlib.util.spec_from_file_location(
        "id65_v3_unattended_authority_test", automation_path
    )
    if automation_spec is None or automation_spec.loader is None:
        raise AssertionError("Cannot load the unattended coordinator lease.")
    automation = importlib.util.module_from_spec(automation_spec)
    sys.modules[automation_spec.name] = automation
    automation_spec.loader.exec_module(automation)
    require(
        capture.CAPTURE_STAGING_PREFIX == automation.CAPTURE_STAGING_PREFIX
        and capture.CAPTURE_COORDINATOR_AUTHORITY_DIRECTORY
        == automation.CAPTURE_COORDINATOR_AUTHORITY_DIRECTORY
        and capture.CAPTURE_COORDINATOR_AUTHORITY_NAME
        == automation.CAPTURE_COORDINATOR_AUTHORITY_NAME
        and capture.CAPTURE_COORDINATOR_STAGING_LOCK_NAME
        == automation.CAPTURE_COORDINATOR_STAGING_LOCK_NAME,
        "Manual and unattended coordinator authority constants diverged.",
    )

    acquisition_refusals = 0
    for owner_kind in ("manual", "unattended"):
        with tempfile.TemporaryDirectory(
            prefix=f"id65-shared-authority-{owner_kind}-"
        ) as temporary:
            root = pathlib.Path(temporary).resolve(strict=True)
            os.chmod(root, 0o700)

            def new_manual() -> Any:
                return capture.CoordinatorLease(root)

            def new_unattended() -> Any:
                return automation.SharedCaptureCoordinatorLease(
                    root, expected_uid=os.getuid()
                )

            owner = new_manual() if owner_kind == "manual" else new_unattended()
            contender_factory = (
                new_unattended if owner_kind == "manual" else new_manual
            )
            owner.__enter__()
            staging = root / capture.CAPTURE_STAGING_PREFIX
            detached = staging.with_name(f"detached-{owner_kind}")
            authority_path = (
                root
                / capture.CAPTURE_COORDINATOR_AUTHORITY_DIRECTORY
                / capture.CAPTURE_COORDINATOR_AUTHORITY_NAME
            )
            authority_before = authority_path.stat(follow_symlinks=False)
            staging.rename(detached)
            staging.mkdir(mode=0o700)
            try:
                for _ in range(2):
                    contender = contender_factory()
                    try:
                        contender.__enter__()
                    except (
                        capture.CaptureRefusal,
                        automation.AutomationRefusal,
                    ) as exc:
                        require(
                            "already owns the lease" in str(exc),
                            "Replacement authority refusal had the wrong reason.",
                        )
                        acquisition_refusals += 1
                    else:
                        contender.__exit__(None, None, None)
                        raise AssertionError(
                            "A coordinator acquired replacement staging authority."
                        )
                authority_after = authority_path.stat(follow_symlinks=False)
                require(
                    (
                        authority_before.st_dev,
                        authority_before.st_ino,
                        authority_before.st_mode,
                        authority_before.st_uid,
                        authority_before.st_nlink,
                    )
                    == (
                        authority_after.st_dev,
                        authority_after.st_ino,
                        authority_after.st_mode,
                        authority_after.st_uid,
                        authority_after.st_nlink,
                    )
                    and stat.S_ISREG(authority_after.st_mode)
                    and stat.S_IMODE(authority_after.st_mode) == 0o600
                    and authority_after.st_uid == os.getuid()
                    and authority_after.st_nlink == 1
                    and not (
                        staging
                        / capture.CAPTURE_COORDINATOR_STAGING_LOCK_NAME
                    ).exists(),
                    "Stable authority changed or replacement staging gained a lock.",
                )
                try:
                    owner.assert_current()
                except (
                    capture.CaptureRefusal,
                    automation.AutomationRefusal,
                ) as exc:
                    require(
                        "rebound" in str(exc),
                        "Original owner did not diagnose staging rebinding.",
                    )
                else:
                    raise AssertionError(
                        "Original owner accepted a rebound staging directory."
                    )
            finally:
                try:
                    owner.__exit__(None, None, None)
                except (
                    capture.CaptureRefusal,
                    automation.AutomationRefusal,
                ) as exc:
                    require(
                        "rebound" in str(exc),
                        "Owner exit did not retain the staging-rebind refusal.",
                    )
                else:
                    raise AssertionError(
                        "Owner exit accepted a rebound staging directory."
                    )
            os.chmod(authority_path, 0o644)
            try:
                expect_refusal(
                    lambda: new_manual().__enter__(),
                    "stable authority inode is unsafe",
                )
            finally:
                os.chmod(authority_path, 0o600)
            authority_parent = authority_path.parent
            os.chmod(authority_parent, 0o755)
            try:
                expect_refusal(
                    lambda: new_manual().__enter__(),
                    "authority parent is not owned mode 0700",
                )
            finally:
                os.chmod(authority_parent, 0o700)
    return acquisition_refusals


def run_source_closure_regressions() -> int:
    coordinator_sha256 = "c" * 64
    baseline = capture.recompute_source_closure(coordinator_sha256)
    require(
        re.fullmatch(r"[0-9a-f]{64}", baseline) is not None,
        "The exact V3 source closure is not lowercase SHA-256.",
    )
    original_prefix = capture.SOURCE_CLOSURE_PREFIX_FIELDS
    original_suffix = capture.SOURCE_CLOSURE_SUFFIX_FIELDS
    rejects = 0

    def reject_variant(
        prefix: tuple[tuple[str, Any], ...],
        suffix: tuple[tuple[str, Any], ...],
        expected: str,
    ) -> None:
        nonlocal rejects
        capture.SOURCE_CLOSURE_PREFIX_FIELDS = prefix
        capture.SOURCE_CLOSURE_SUFFIX_FIELDS = suffix
        try:
            expect_refusal(
                lambda: capture.recompute_source_closure(coordinator_sha256),
                expected,
            )
            rejects += 1
        finally:
            capture.SOURCE_CLOSURE_PREFIX_FIELDS = original_prefix
            capture.SOURCE_CLOSURE_SUFFIX_FIELDS = original_suffix

    reject_variant(
        (original_prefix[1], original_prefix[0], *original_prefix[2:]),
        original_suffix,
        "reordered, missing, duplicated, or unknown",
    )
    reject_variant(
        original_prefix[:-1],
        original_suffix,
        "reordered, missing, duplicated, or unknown",
    )
    reject_variant(
        (*original_prefix, original_prefix[-1]),
        original_suffix,
        "reordered, missing, duplicated, or unknown",
    )
    mixed_suffix = tuple(
        (
            name,
            "0" * 64
            if name == "collisionWindingV2OutputModelSha256"
            else value,
        )
        for name, value in original_suffix
    )
    reject_variant(
        original_prefix,
        mixed_suffix,
        "repair lineage is internally mixed",
    )
    require(
        capture.recompute_source_closure(coordinator_sha256) == baseline,
        "Source-closure negative fixtures did not restore the exact manifest.",
    )
    return rejects


def run_source_topology_regressions() -> int:
    rejects = 0
    with tempfile.TemporaryDirectory(prefix="id65-v3-wrong-root-") as temp:
        expect_refusal(
            lambda: capture.SourceGuard(pathlib.Path(temp).resolve()),
            "exact audited Capture checkout",
        )
        rejects += 1

    repository_root = TOOL_PATH.parent.parent.resolve(strict=True)
    guard = capture.SourceGuard(repository_root)
    original_reader = capture.read_regular_file

    def pinned_reader(path: pathlib.Path, *args: Any, **kwargs: Any) -> Any:
        path = pathlib.Path(path)
        relative = path.relative_to(repository_root).as_posix()
        if relative in capture.PINNED_SOURCE_FILES:
            data = None
            if kwargs.get("include_bytes"):
                data = (
                    f'FILE "{capture.CANDIDATE_STEM}.bin" BINARY\n'
                    "  TRACK 01 MODE2/2352\n"
                    "    INDEX 01 00:00:00\n"
                ).encode("ascii")
            return capture.FileIdentity(
                os.path.realpath(path), 1, abs(hash(relative)) + 1,
                len(data or b"x"), capture.PINNED_SOURCE_FILES[relative], data,
            )
        if relative == capture.CANDIDATE_BIN_RELATIVE_PATH:
            return capture.FileIdentity(
                os.path.realpath(path), 1, 2, capture.EXPECTED_CANDIDATE_BIN_SIZE,
                capture.EXPECTED_CANDIDATE_BIN_SHA256,
            )
        return original_reader(path, *args, **kwargs)

    for relative in (
        capture.PROFILE_BUILDER_RELATIVE_PATH,
        capture.PROFILE_VERIFIER_RELATIVE_PATH,
        capture.AUTOMATION_RELATIVE_PATH,
    ):
        def tampered_reader(
            path: pathlib.Path, *args: Any, target: str = relative, **kwargs: Any
        ) -> Any:
            identity = pinned_reader(path, *args, **kwargs)
            if pathlib.Path(path).relative_to(repository_root).as_posix() == target:
                return dataclasses.replace(identity, sha256="0" * 64)
            return identity

        capture.read_regular_file = tampered_reader
        try:
            expect_refusal(guard.validate_all, f"Pinned source hash changed: {relative}")
            rejects += 1
        finally:
            capture.read_regular_file = original_reader
    return rejects


def run_verifier_binding_regressions() -> int:
    repository_root = TOOL_PATH.parent.parent.resolve(strict=True)
    guard = capture.SourceGuard(repository_root)
    original_reader = capture.read_regular_file
    verifier_path = repository_root / capture.PROFILE_VERIFIER_RELATIVE_PATH

    def malicious_reader(path: pathlib.Path, *args: Any, **kwargs: Any) -> Any:
        if pathlib.Path(path) == verifier_path:
            payload = b"raise RuntimeError('verifier bytes executed before hash pin')\n"
            return capture.FileIdentity(
                os.path.realpath(path), 1, 2, len(payload),
                hashlib.sha256(payload).hexdigest(), payload,
            )
        return original_reader(path, *args, **kwargs)

    capture.read_regular_file = malicious_reader
    try:
        expect_refusal(guard._load_verifier, "hash changed before dynamic import")
    finally:
        capture.read_regular_file = original_reader
    rejects = 1

    verifier = capture.SourceGuard(repository_root)._load_verifier()
    production_pins = verifier.PRODUCTION_PINS
    for field, value in (
        ("profile_id", "wrong-profile"),
        ("receipt_sha256", "0" * 64),
        ("settings_sha256", "1" * 64),
    ):
        altered = dataclasses.replace(production_pins, **{field: value})
        proxy = type("VerifierProxy", (), {})()
        proxy.__dict__.update(verifier.__dict__)
        proxy.PRODUCTION_PINS = altered
        expect_refusal(
            lambda proxy=proxy: guard._require_verifier_contract(proxy),
            f"production pin changed: {field}",
        )
        rejects += 1
    return rejects


def _synthetic_procargs2(
    argv: tuple[str, ...], environment: tuple[bytes, ...]
) -> bytes:
    executable = argv[0].encode("utf-8", "strict")
    return (
        len(argv).to_bytes(4, sys.byteorder, signed=True)
        + executable + b"\0\0\0"
        + b"\0".join(item.encode("utf-8", "strict") for item in argv)
        + b"\0"
        + b"\0".join(environment)
        + b"\0\0\0"
    )


def run_kern_procargs_home_regressions() -> int:
    home = "/private/tmp/id65-external-home"
    argv = ("/private/tmp/DuckStation", "/private/tmp/candidate.cue")
    good = _synthetic_procargs2(
        argv, (f"HOME={home}".encode(), b"VALUE=contains=equals")
    )
    parsed_argv, environment_hash, parsed_home = capture._parse_kern_procargs2(
        good, home
    )
    require(
        parsed_argv == argv
        and parsed_home == home
        and capture.SHA256_RE.fullmatch(environment_hash) is not None,
        "Synthetic KERN_PROCARGS2 did not bind exact argv/HOME/environment hash.",
    )
    cases = (
        (_synthetic_procargs2(argv, (b"A=B",)), home, "omits HOME"),
        (_synthetic_procargs2(argv, (f"HOME={home}".encode(), f"HOME={home}".encode())), home, "duplicated"),
        (_synthetic_procargs2(argv, (b"HOME=/Users/john",)), home, "not the pinned external HOME"),
        (_synthetic_procargs2(argv, (b"HOME=/tmp/duckstation-id65-isolated.xizA5B",)), home, "not the pinned external HOME"),
        (good, "/private/tmp/rebound-home", "not the pinned external HOME"),
        (_synthetic_procargs2(argv, (f"HOME={home}".encode(), b"BROKEN")), home, "no assignment"),
        (_synthetic_procargs2(argv, (f"HOME={home}".encode(), b"BAD=\xff")), home, "not UTF-8"),
    )
    for raw, expected, message in cases:
        expect_refusal(
            lambda raw=raw, expected=expected: capture._parse_kern_procargs2(
                raw, expected
            ),
            message,
        )
    return len(cases)


def run_snapshot_v2_regressions() -> int:
    snapshot = capture._offline_profile(
        "before", capture.SESSION_IDS[0],
        "id65v3-process-" + "1" * 32,
        "2026-08-10T20:00:00.000Z",
    )
    snapshot["dataFiles"].append({
        "relativePath": "cache/caf\u00e9.bin",
        "byteLength": 1,
        "sha256": "d" * 64,
    })
    snapshot["dataFiles"].sort(key=lambda item: item["relativePath"])
    capture._validate_profile_snapshot(
        snapshot,
        phase="before",
        session_id=capture.SESSION_IDS[0],
        process_instance_id="id65v3-process-" + "1" * 32,
        captured_at_utc="2026-08-10T20:00:00.000Z",
    )
    cases: list[tuple[dict[str, Any], str]] = []
    changed = copy.deepcopy(snapshot)
    changed["formatVersion"] = 1
    cases.append((changed, "formatVersion"))
    for relative in (
        "logs/unknown.bin", "savestates/slot.bin", "cache\\bad.bin", "cache"
    ):
        changed = copy.deepcopy(snapshot)
        changed["dataFiles"].append({
            "relativePath": relative, "byteLength": 1, "sha256": "e" * 64,
        })
        changed["dataFiles"].sort(key=lambda item: item["relativePath"])
        cases.append((changed, "path" if "\\" in relative else "unapproved"))
    changed = copy.deepcopy(snapshot)
    changed["handoffReceiptSha256"] = "f" * 64
    cases.append((changed, "handoffReceiptSha256"))
    changed = copy.deepcopy(snapshot)
    next(item for item in changed["dataFiles"] if item["relativePath"] == "settings.ini")["sha256"] = "f" * 64
    cases.append((changed, "settings.ini"))
    changed = copy.deepcopy(snapshot)
    next(
        item for item in changed["dataFiles"]
        if item["relativePath"] == "cache/caf\u00e9.bin"
    )["relativePath"] = "cache/cafe\u0301.bin"
    changed["dataFiles"].sort(key=lambda item: item["relativePath"])
    cases.append((changed, "segment-safe"))
    changed = copy.deepcopy(snapshot)
    next(
        item for item in changed["dataFiles"]
        if item["relativePath"] == "cache/caf\u00e9.bin"
    )["byteLength"] = capture.MAX_SAFE_INTEGER + 1
    cases.append((changed, "unapproved"))
    for changed, message in cases:
        expect_refusal(
            lambda changed=changed: capture._validate_profile_snapshot(
                changed,
                phase="before",
                session_id=capture.SESSION_IDS[0],
                process_instance_id="id65v3-process-" + "1" * 32,
                captured_at_utc="2026-08-10T20:00:00.000Z",
            ),
            message,
        )
    canonical = capture.canonical_lf_bytes(snapshot)
    require(
        b"cache/caf\\u00e9.bin" in canonical
        and "cache/caf\u00e9.bin".encode("utf-8") not in canonical
        and capture._decode_verifier_canonical_json_bytes(
            canonical, "snapshot"
        ) == snapshot,
        "Verifier canonical LF snapshot did not round-trip.",
    )
    expect_refusal(
        lambda: capture._decode_verifier_canonical_json_bytes(
            canonical[:-1], "snapshot"
        ),
        "LF-terminated",
    )
    return len(cases) + 1


def read_json(path: pathlib.Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def run_boot_identity_regressions() -> int:
    session = b"4B25140E-C24D-490D-BCC8-497264D9EC78\0"
    other_session = b"4B25140E-C24D-490D-BCC8-497264D9EC79\0"
    boot = struct.pack("=qq", 1_700_000_000, 123_456)
    other_boot_seconds = struct.pack("=qq", 1_700_000_001, 123_456)
    other_boot_microseconds = struct.pack("=qq", 1_700_000_000, 123_457)
    platform_uuid = "5B7C2FCC-ABC6-5BCB-8958-1773A87EF329"
    other_platform_uuid = "5B7C2FCC-ABC6-5BCB-8958-1773A87EF328"
    serial = "H747KV46LM"
    other_serial = "H747KV46LN"

    def platform_plist(
        *,
        uuid_value: Any = platform_uuid,
        serial_value: Any = serial,
        root_value: Any | None = None,
    ) -> bytes:
        value = root_value
        if value is None:
            value = [{
                "IOPlatformUUID": uuid_value,
                "IOPlatformSerialNumber": serial_value,
                "UnrelatedProperty": b"ignored",
            }]
        return capture.plistlib.dumps(
            value, fmt=capture.plistlib.FMT_BINARY, sort_keys=False
        )

    platform = platform_plist()

    class MockBootBoundary(capture.RealMacBoundary):
        def __init__(
            self,
            *,
            session_reads: tuple[Any, Any] = (session, session),
            boot_reads: tuple[Any, Any] = (boot, boot),
            platform_reads: tuple[Any, Any] = (platform, platform),
        ) -> None:
            self.session_reads = list(session_reads)
            self.boot_reads = list(boot_reads)
            self.platform_reads = list(platform_reads)
            self.calls: list[tuple[str, Any]] = []

        @staticmethod
        def _next(values: list[Any]) -> bytes:
            if not values:
                raise AssertionError("Mock boot identity exhausted a read queue.")
            value = values.pop(0)
            if isinstance(value, BaseException):
                raise value
            return value

        def _sysctlbyname_bytes(self, name: str) -> bytes:
            self.calls.append(("sysctl", name))
            if name == "kern.bootsessionuuid":
                return self._next(self.session_reads)
            if name == "kern.boottime":
                return self._next(self.boot_reads)
            raise AssertionError(f"Unexpected mock sysctl name: {name}")

        def _run_fixed(
            self,
            argv: list[str],
            *,
            allowed_codes: set[int] = {0},
            maximum_stdout: int,
        ) -> Any:
            del allowed_codes
            self.calls.append(("ioreg", tuple(argv)))
            require(
                tuple(argv) == capture.BOOT_PLATFORM_IOREG_ARGV
                and maximum_stdout == 4 * 1024 * 1024,
                "Boot identity issued a nonfixed ioreg query.",
            )
            return subprocess.CompletedProcess(
                argv, 0, self._next(self.platform_reads), b""
            )

    expected_calls = [
        ("sysctl", "kern.bootsessionuuid"),
        ("sysctl", "kern.boottime"),
        ("ioreg", capture.BOOT_PLATFORM_IOREG_ARGV),
        ("ioreg", capture.BOOT_PLATFORM_IOREG_ARGV),
        ("sysctl", "kern.boottime"),
        ("sysctl", "kern.bootsessionuuid"),
    ]
    valid = MockBootBoundary()
    digest = valid.boot_identity()
    expected_manifest = (
        "macos-boot-machine-identity-v2\n"
        "bootSessionUuid=4B25140E-C24D-490D-BCC8-497264D9EC78\n"
        "bootTimeSeconds=1700000000\n"
        "bootTimeMicroseconds=123456\n"
        "ioPlatformUuid=5B7C2FCC-ABC6-5BCB-8958-1773A87EF329\n"
        "ioPlatformSerialNumber=H747KV46LM\n"
    ).encode("ascii")
    require(
        digest == hashlib.sha256(expected_manifest).hexdigest()
        and valid.calls == expected_calls,
        "Boot identity v2 digest or mirrored query order changed.",
    )

    differentiated = {
        digest,
        MockBootBoundary(session_reads=(other_session, other_session)).boot_identity(),
        MockBootBoundary(boot_reads=(
            other_boot_seconds, other_boot_seconds
        )).boot_identity(),
        MockBootBoundary(boot_reads=(
            other_boot_microseconds, other_boot_microseconds
        )).boot_identity(),
        MockBootBoundary(platform_reads=(
            platform_plist(uuid_value=other_platform_uuid),
            platform_plist(uuid_value=other_platform_uuid),
        )).boot_identity(),
        MockBootBoundary(platform_reads=(
            platform_plist(serial_value=other_serial),
            platform_plist(serial_value=other_serial),
        )).boot_identity(),
    }
    require(
        len(differentiated) == 6,
        "Boot identity digest does not distinguish all bound components.",
    )

    refusal_count = 0

    def refuse(action: Any, expected: str | None = None) -> None:
        nonlocal refusal_count
        expect_refusal(action, expected)
        refusal_count += 1

    for malformed_session in (
        bytearray(session),
        b"",
        session[:-1],
        session[:-1].lower() + b"\0",
        b"00000000-0000-0000-0000-000000000000\0",
        session + b"\0",
    ):
        refuse(
            lambda malformed_session=malformed_session: MockBootBoundary(
                session_reads=(malformed_session, malformed_session)
            ).boot_identity(),
            "Boot-session UUID",
        )

    for malformed_boot in (
        bytearray(boot),
        b"",
        boot[:-1],
        struct.pack("=qq", 0, 0),
        struct.pack("=qq", 1_700_000_000, -1),
        struct.pack("=qq", 1_700_000_000, 1_000_000),
        struct.pack("=qq", int(capture.time.time()) + 60, 0),
    ):
        refuse(
            lambda malformed_boot=malformed_boot: MockBootBoundary(
                boot_reads=(malformed_boot, malformed_boot)
            ).boot_identity(),
            "Boot time" if isinstance(malformed_boot, bytes) and len(malformed_boot) == 16
            else "Boot-time",
        )

    malformed_platforms = (
        b"not a plist",
        platform_plist(root_value={}),
        platform_plist(root_value=[]),
        platform_plist(root_value=[{}, {}]),
        platform_plist(root_value=[False]),
        platform_plist(uuid_value=501),
        platform_plist(uuid_value="00000000-0000-0000-0000-000000000000"),
        platform_plist(uuid_value=platform_uuid.lower()),
        platform_plist(serial_value=501),
        platform_plist(serial_value="lowercase1"),
        platform_plist(serial_value="SHORT"),
        platform_plist(serial_value="00000000"),
        platform_plist(serial_value="AAAAAAAA"),
    )
    for malformed_platform in malformed_platforms:
        refuse(lambda malformed_platform=malformed_platform: MockBootBoundary(
            platform_reads=(malformed_platform, malformed_platform)
        ).boot_identity())

    refuse(
        lambda: MockBootBoundary(
            session_reads=(session, other_session)
        ).boot_identity(),
        "mirrored read",
    )
    refuse(
        lambda: MockBootBoundary(
            boot_reads=(boot, other_boot_seconds)
        ).boot_identity(),
        "mirrored read",
    )
    refuse(
        lambda: MockBootBoundary(
            boot_reads=(boot, other_boot_microseconds)
        ).boot_identity(),
        "mirrored read",
    )
    refuse(
        lambda: MockBootBoundary(platform_reads=(
            platform,
            platform_plist(uuid_value=other_platform_uuid),
        )).boot_identity(),
        "mirrored read",
    )
    refuse(
        lambda: MockBootBoundary(platform_reads=(
            platform,
            platform_plist(serial_value=other_serial),
        )).boot_identity(),
        "mirrored read",
    )
    refuse(
        lambda: MockBootBoundary(session_reads=(
            capture.CaptureRefusal("missing boot session"), session
        )).boot_identity(),
        "missing boot session",
    )
    refuse(
        lambda: MockBootBoundary(boot_reads=(
            capture.CaptureRefusal("missing boot time"), boot
        )).boot_identity(),
        "missing boot time",
    )
    refuse(
        lambda: MockBootBoundary(platform_reads=(
            capture.CaptureRefusal("missing platform identity"), platform
        )).boot_identity(),
        "missing platform identity",
    )
    unapproved = capture.RealMacBoundary.__new__(capture.RealMacBoundary)
    refuse(
        lambda: unapproved._sysctlbyname_bytes("kern.unapproved_identity"),
        "Unapproved boot-identity sysctl",
    )
    return refusal_count


def run_macos_boundary_regressions() -> int:
    refusal_count = 0

    def refuse(action: Any) -> None:
        nonlocal refusal_count
        expect_refusal(action)
        refusal_count += 1

    absent = object()

    def console_fixture(
        *,
        root_lock: Any = False,
        users: Any = absent,
        on_console: Any = True,
        login: Any = True,
        uid: Any = 501,
        name: Any = "john",
        user_lock: Any = absent,
    ) -> dict[str, Any]:
        session: dict[str, Any] = {
            "kCGSSessionOnConsoleKey": on_console,
            "kCGSessionLoginDoneKey": login,
            "kCGSSessionUserIDKey": uid,
            "kCGSSessionUserNameKey": name,
        }
        if user_lock is not absent:
            session["CGSSessionScreenIsLocked"] = user_lock
        return {
            "IOConsoleLocked": root_lock,
            "IOConsoleUsers": [session] if users is absent else users,
        }

    capture._require_exact_console_session(
        console_fixture(), expected_uid=501, expected_name="john"
    )
    capture._require_exact_console_session(
        [console_fixture(user_lock=False)], expected_uid=501,
        expected_name="john",
    )
    console_tampers = (
        "wrong-root",
        [],
        [console_fixture(), console_fixture()],
        [False],
        {"IOConsoleUsers": []},
        console_fixture(root_lock=0),
        console_fixture(root_lock=True),
        console_fixture(users={}),
        console_fixture(users=[False]),
        console_fixture(on_console=1),
        console_fixture(on_console=False),
        console_fixture(users=[
            console_fixture()["IOConsoleUsers"][0],
            console_fixture()["IOConsoleUsers"][0],
        ]),
        console_fixture(login=False),
        console_fixture(login=1),
        console_fixture(uid=502),
        console_fixture(uid=True),
        console_fixture(name="other"),
        console_fixture(name=501),
        console_fixture(user_lock=True),
        console_fixture(user_lock=0),
    )
    for value in console_tampers:
        refuse(lambda value=value: capture._require_exact_console_session(
            value, expected_uid=501, expected_name="john"
        ))
    refuse(lambda: capture._require_exact_console_session(
        console_fixture(), expected_uid=True, expected_name="john"
    ))
    refuse(lambda: capture._require_exact_console_session(
        console_fixture(), expected_uid=501, expected_name=""
    ))

    pid_bytes = capture.ctypes.sizeof(capture.ctypes.c_int)
    require(
        capture._decode_exact_uid_pid_list(
            query_byte_count=3 * pid_bytes,
            returned_byte_count=2 * pid_bytes,
            buffer_byte_count=67 * pid_bytes,
            raw_pids=[77, 42],
            coordinator_pid=42,
        ) == [42, 77],
        "UID-only PID vector did not preserve one exact sorted set.",
    )
    pid_tampers = (
        dict(query_byte_count=0, returned_byte_count=pid_bytes,
             buffer_byte_count=65 * pid_bytes, raw_pids=[42]),
        dict(query_byte_count=pid_bytes + 1, returned_byte_count=pid_bytes,
             buffer_byte_count=65 * pid_bytes, raw_pids=[42]),
        dict(query_byte_count=pid_bytes, returned_byte_count=pid_bytes,
             buffer_byte_count=pid_bytes, raw_pids=[42]),
        dict(query_byte_count=pid_bytes, returned_byte_count=0,
             buffer_byte_count=65 * pid_bytes, raw_pids=[]),
        dict(query_byte_count=pid_bytes, returned_byte_count=pid_bytes + 1,
             buffer_byte_count=65 * pid_bytes, raw_pids=[42]),
        dict(query_byte_count=pid_bytes, returned_byte_count=65 * pid_bytes,
             buffer_byte_count=65 * pid_bytes, raw_pids=list(range(1, 66))),
        dict(query_byte_count=pid_bytes, returned_byte_count=66 * pid_bytes,
             buffer_byte_count=65 * pid_bytes, raw_pids=list(range(1, 67))),
        dict(query_byte_count=2 * pid_bytes, returned_byte_count=2 * pid_bytes,
             buffer_byte_count=66 * pid_bytes, raw_pids=[42]),
        dict(query_byte_count=2 * pid_bytes, returned_byte_count=2 * pid_bytes,
             buffer_byte_count=66 * pid_bytes, raw_pids=[42, 0]),
        dict(query_byte_count=2 * pid_bytes, returned_byte_count=2 * pid_bytes,
             buffer_byte_count=66 * pid_bytes, raw_pids=[42, 42]),
        dict(query_byte_count=2 * pid_bytes, returned_byte_count=2 * pid_bytes,
             buffer_byte_count=66 * pid_bytes, raw_pids=[41, 43]),
    )
    for values in pid_tampers:
        refuse(lambda values=values: capture._decode_exact_uid_pid_list(
            **values, coordinator_pid=42
        ))

    def bsd_fixture(
        *, pid: int = 42, uid: int = 501, ruid: int = 501, svuid: int = 501
    ) -> Any:
        value = capture._ProcBsdInfo()
        value.pid = pid
        value.uid = uid
        value.ruid = ruid
        value.svuid = svuid
        return value

    capture._require_exact_bsd_identity(
        bsd_fixture(), expected_pid=42, expected_uid=501,
        require_real_and_saved_uid=True,
    )
    for value, all_uids in (
        (bsd_fixture(pid=43), False),
        (bsd_fixture(uid=502), False),
        (bsd_fixture(ruid=502), True),
        (bsd_fixture(svuid=502), True),
    ):
        refuse(lambda value=value, all_uids=all_uids:
            capture._require_exact_bsd_identity(
                value, expected_pid=42, expected_uid=501,
                require_real_and_saved_uid=all_uids,
            ))

    class FakeLibproc:
        def __init__(self) -> None:
            self.calls: list[tuple[int, int, bool, int]] = []

        def proc_listpids(
            self, kind: int, typeinfo: int, buffer: Any, buffer_bytes: int
        ) -> int:
            self.calls.append((kind, typeinfo, buffer is None, buffer_bytes))
            if buffer is None:
                return 2 * pid_bytes
            buffer[0] = os.getpid()
            buffer[1] = 77
            return 2 * pid_bytes

    fake = FakeLibproc()
    boundary = capture.RealMacBoundary.__new__(capture.RealMacBoundary)
    boundary._libproc = fake
    require(
        boundary._all_pids() == sorted([os.getpid(), 77])
        and fake.calls[0] == (
            capture.RealMacBoundary.PROC_UID_ONLY, os.geteuid(), True, 0
        )
        and fake.calls[1][0:2] == (
            capture.RealMacBoundary.PROC_UID_ONLY, os.geteuid()
        )
        and not fake.calls[1][2]
        and fake.calls[1][3] > 2 * pid_bytes,
        "Real boundary did not enumerate only the current effective UID.",
    )

    class VanishedPathLibproc:
        @staticmethod
        def proc_pidpath(pid: int, buffer: Any, buffer_bytes: int) -> int:
            capture.ctypes.set_errno(capture.errno.ENOENT)
            return 0

    vanished_path_boundary = capture.RealMacBoundary.__new__(
        capture.RealMacBoundary
    )
    vanished_path_boundary._libproc = VanishedPathLibproc()
    try:
        vanished_path_boundary._pid_path(77)
    except ProcessLookupError as exc:
        require(
            exc.args == (77,),
            "A vanished process path lost the exact PID identity.",
        )
    else:
        raise AssertionError(
            "ENOENT from proc_pidpath was not treated as a vanished process."
        )
    return refusal_count


def run_native_open_file_lifecycle_with_injected_procargs_regression(
    repository_root: pathlib.Path,
) -> None:
    class NativeObservationBoundary(capture.RealMacBoundary):
        expected_pid: int | None = None
        expected_argv: tuple[str, ...] | None = None
        injected_external_home: str | None = None

        def require_capture_environment(self) -> None:
            return

        def _pid_argv_environment(
            self, pid: int, expected_external_home: str
        ) -> tuple[tuple[str, ...], str, str]:
            del expected_external_home
            if (
                pid != self.expected_pid
                or self.expected_argv is None
                or self.injected_external_home is None
            ):
                raise ProcessLookupError(pid)
            environment = (
                f"HOME={self.injected_external_home}\0".encode("utf-8")
            )
            return (
                self.expected_argv,
                hashlib.sha256(environment).hexdigest(),
                self.injected_external_home,
            )

    boundary = NativeObservationBoundary(repository_root)
    require(
        boundary.duckstation_processes() == [] and boundary.listener_pids() == set(),
        "A real DuckStation process or GDB listener overlaps the native lifecycle gate.",
    )
    native_boot_identity_a = boundary.boot_identity()
    native_boot_identity_b = boundary.boot_identity()
    require(
        capture.SHA256_RE.fullmatch(native_boot_identity_a) is not None
        and native_boot_identity_a == native_boot_identity_b,
        "Native mirrored boot-machine identity was not stable across two calls.",
    )
    cue_path = pathlib.Path(
        os.path.realpath(repository_root / capture.CANDIDATE_CUE_RELATIVE_PATH)
    )
    bin_path = pathlib.Path(
        os.path.realpath(repository_root / capture.CANDIDATE_BIN_RELATIVE_PATH)
    )
    helper_source = r'''
#include <errno.h>
#include <fcntl.h>
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>

static int read_command(char expected) {
    char value = 0;
    ssize_t count;
    do {
        count = read(STDIN_FILENO, &value, 1);
    } while (count < 0 && errno == EINTR);
    return count == 1 && value == expected;
}

int main(int argc, char **argv) {
    const char *bin_path = getenv("ID65_NATIVE_TEST_BIN");
    const char *cache_path = getenv("ID65_NATIVE_TEST_CACHE_BIN");
    const char *extra_bin_path = getenv("ID65_NATIVE_TEST_EXTRA_BIN");
    const char *extra_cue_path = getenv("ID65_NATIVE_TEST_EXTRA_CUE");
    if (argc != 2 || bin_path == NULL || bin_path[0] != '/' ||
        cache_path == NULL || cache_path[0] != '/' ||
        extra_bin_path == NULL || extra_bin_path[0] != '/' ||
        extra_cue_path == NULL || extra_cue_path[0] != '/') return 20;
    int cue_fd = open(argv[1], O_RDONLY | O_CLOEXEC);
    if (cue_fd < 0) return 21;
    int bin_fd = open(bin_path, O_RDONLY | O_CLOEXEC);
    if (bin_fd < 0) { close(cue_fd); return 22; }
    int cache_fd = open(cache_path, O_RDWR | O_CLOEXEC);
    if (cache_fd < 0) { close(cue_fd); close(bin_fd); return 23; }
    setvbuf(stdout, NULL, _IONBF, 0);
    printf("READY %d %d %d\n", cue_fd, bin_fd, cache_fd);
    if (!read_command('C')) { close(cue_fd); close(bin_fd); close(cache_fd); return 24; }
    if (close(cue_fd) != 0) { close(bin_fd); close(cache_fd); return 25; }
    printf("CUE_CLOSED\n");
    if (!read_command('B')) { close(bin_fd); close(cache_fd); return 26; }
    int extra_bin_fd = open(extra_bin_path, O_RDONLY | O_CLOEXEC);
    if (extra_bin_fd < 0) { close(bin_fd); close(cache_fd); return 27; }
    printf("EXTRA_BIN_OPEN %d\n", extra_bin_fd);
    if (!read_command('b')) { close(extra_bin_fd); close(bin_fd); close(cache_fd); return 28; }
    if (close(extra_bin_fd) != 0) { close(bin_fd); close(cache_fd); return 29; }
    printf("EXTRA_BIN_CLOSED\n");
    if (!read_command('D')) { close(bin_fd); close(cache_fd); return 30; }
    int extra_cue_fd = open(extra_cue_path, O_RDONLY | O_CLOEXEC);
    if (extra_cue_fd < 0) { close(bin_fd); close(cache_fd); return 31; }
    printf("EXTRA_CUE_OPEN %d\n", extra_cue_fd);
    if (!read_command('d')) { close(extra_cue_fd); close(bin_fd); close(cache_fd); return 32; }
    if (close(extra_cue_fd) != 0) { close(bin_fd); close(cache_fd); return 33; }
    printf("EXTRA_CUE_CLOSED\n");
    if (!read_command('Q')) { close(bin_fd); close(cache_fd); return 34; }
    if (close(cache_fd) != 0) { close(bin_fd); return 35; }
    if (close(bin_fd) != 0) return 36;
    printf("EXITING\n");
    return 0;
}
'''.lstrip()

    def read_line(stream: Any, context: str) -> bytes:
        readable, _, _ = select.select([stream], [], [], 10.0)
        if not readable:
            raise AssertionError(f"Native lifecycle helper timed out during {context}.")
        line = stream.readline()
        if not line:
            raise AssertionError(f"Native lifecycle helper closed stdout during {context}.")
        return line.rstrip(b"\r\n")

    temp_path: pathlib.Path | None = None
    child: subprocess.Popen[bytes] | None = None
    with tempfile.TemporaryDirectory(
        prefix="id65-native-duckstation-lifecycle-"
    ) as temporary:
        temp_path = pathlib.Path(temporary).resolve(strict=True)
        source_path = temp_path / "DuckStation.c"
        helper_path = temp_path / "DuckStation"
        extra_bin_path = temp_path / "outside.bin"
        extra_cue_path = temp_path / "outside.cue"
        runtime_data_root = temp_path / "runtime-data"
        injected_external_home = temp_path / "external-home"
        injected_external_home.mkdir(mode=0o700)
        cache_path = runtime_data_root / capture.ALLOWED_PROCESS_CACHE_BIN_RELATIVE_PATH
        cache_path.parent.mkdir(parents=True)
        cache_path.write_bytes(b"native temporary Vulkan shader cache")
        cache_path.chmod(0o644)
        source_path.write_bytes(helper_source.encode("utf-8", "strict"))
        extra_bin_path.write_bytes(b"native outside-cache BIN")
        extra_cue_path.write_bytes(b"native extra CUE")
        compiled = subprocess.run(
            [
                "/usr/bin/clang", "-std=c11", "-O0", "-Wall", "-Wextra",
                "-Werror", "-o", str(helper_path), str(source_path),
            ],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
            close_fds=True,
            timeout=30.0,
            env={"LANG": "C", "LC_ALL": "C", "PATH": "/usr/bin:/bin"},
        )
        require(
            compiled.returncode == 0 and helper_path.is_file(),
            "Fixed clang failed to build the native DuckStation lifecycle helper: "
            + compiled.stderr.decode("utf-8", "replace"),
        )
        env = {
            "ID65_NATIVE_TEST_BIN": str(bin_path),
            "ID65_NATIVE_TEST_CACHE_BIN": str(cache_path),
            "ID65_NATIVE_TEST_EXTRA_BIN": str(extra_bin_path),
            "ID65_NATIVE_TEST_EXTRA_CUE": str(extra_cue_path),
            "LANG": "C",
            "LC_ALL": "C",
            "PATH": "/usr/bin:/bin",
            "HOME": os.path.realpath(injected_external_home),
        }
        try:
            child = subprocess.Popen(
                [str(helper_path), str(cue_path)],
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                env=env,
                close_fds=True,
            )
            require(
                child.stdin is not None and child.stdout is not None
                and child.stderr is not None,
                "Native lifecycle helper pipes were not created.",
            )
            boundary.expected_pid = child.pid
            boundary.expected_argv = (str(helper_path), str(cue_path))
            boundary.injected_external_home = os.path.realpath(
                injected_external_home
            )
            ready = read_line(child.stdout, "initial open-file handshake")
            require(
                ready.startswith(b"READY "),
                "Native lifecycle helper did not report both initial descriptors.",
            )
            initial_processes = boundary.duckstation_processes()
            require(
                len(initial_processes) == 1
                and initial_processes[0].pid == child.pid,
                "Real libproc did not observe the one native DuckStation helper.",
            )
            initial = initial_processes[0]
            cue_real = str(cue_path)
            bin_real = str(bin_path)
            cache_real = env["ID65_NATIVE_TEST_CACHE_BIN"]
            require(
                initial.argv == (str(helper_path), cue_real)
                and initial.external_home_real_path
                == os.path.realpath(injected_external_home)
                and initial.environment_nul_sha256
                == hashlib.sha256(
                    f"HOME={os.path.realpath(injected_external_home)}\0".encode(
                        "utf-8"
                    )
                ).hexdigest()
                and cue_real in initial.open_paths
                and bin_real in initial.open_paths
                and cache_real in initial.open_paths
                and not capture._candidate_process_lifecycle_ready(
                    initial, cue_real, bin_real
                ),
                "Real lsof/libproc did not observe the initial CUE+BIN lifecycle.",
            )
            initial_bin = [
                item for item in initial.open_files if item.real_path == bin_real
            ]
            require(
                len(initial_bin) == 1
                and re.fullmatch(r"[0-9]+r", initial_bin[0].fd) is not None,
                "Native lifecycle helper did not expose one read-only BIN descriptor: "
                + repr([(item.fd, item.real_path) for item in initial.open_files]),
            )
            initial_cache = [
                item for item in initial.open_files if item.real_path == cache_real
            ]
            require(
                len(initial_cache) == 1
                and re.fullmatch(r"[0-9]+u", initial_cache[0].fd) is not None,
                "Native lifecycle helper did not expose one update-mode shader cache: "
                + repr([(item.fd, item.real_path) for item in initial.open_files]),
            )

            child.stdin.write(b"C")
            child.stdin.flush()
            require(
                read_line(child.stdout, "CUE-close handshake") == b"CUE_CLOSED",
                "Native lifecycle helper did not close only the CUE descriptor.",
            )
            prior_handler = signal.getsignal(signal.SIGALRM)

            def timeout_handler(signum: int, frame: Any) -> None:
                del signum, frame
                raise TimeoutError("Inherited DuckStation wait exceeded 10 seconds.")

            signal.signal(signal.SIGALRM, timeout_handler)
            signal.alarm(10)
            try:
                observed = boundary.wait_for_duckstation(cue_real)
            finally:
                signal.alarm(0)
                signal.signal(signal.SIGALRM, prior_handler)
            observed_bin = [
                item for item in observed.open_files if item.real_path == bin_real
            ]
            observed_cache = [
                item for item in observed.open_files if item.real_path == cache_real
            ]
            require(
                observed.pid == child.pid
                and observed.argv == (str(helper_path), cue_real)
                and cue_real not in observed.open_paths
                and len(observed_bin) == 1
                and observed_bin[0] == initial_bin[0]
                and observed_cache == initial_cache
                and capture._candidate_process_lifecycle_ready(
                    observed, cue_real, bin_real
                ),
                "Inherited wait did not preserve the exact read-only BIN FD after CUE close.",
            )

            native_source = capture.SourceGuard(repository_root)
            native_source.profile_root = runtime_data_root
            native_source.external_home_root = injected_external_home
            native_coordinator = capture.CaptureCoordinator(
                boundary, source_guard=native_source
            )
            _, candidate_bin = native_source.candidate_identities()
            native_coordinator._validate_open_disc_files(
                observed, cue_real, candidate_bin
            )

            child.stdin.write(b"B")
            child.stdin.flush()
            require(
                read_line(child.stdout, "outside-BIN open handshake").startswith(
                    b"EXTRA_BIN_OPEN "
                ),
                "Native lifecycle helper did not open the outside-cache BIN.",
            )
            extra_bin_processes = boundary.duckstation_processes()
            require(
                len(extra_bin_processes) == 1
                and str(extra_bin_path) in extra_bin_processes[0].open_paths,
                "Real lsof did not observe the outside-cache BIN descriptor.",
            )
            expect_refusal(
                lambda: native_coordinator._validate_open_disc_files(
                    extra_bin_processes[0], cue_real, candidate_bin
                ),
                "another CUE or BIN open",
            )
            child.stdin.write(b"b")
            child.stdin.flush()
            require(
                read_line(child.stdout, "outside-BIN close handshake") ==
                b"EXTRA_BIN_CLOSED",
                "Native lifecycle helper did not close the outside-cache BIN.",
            )

            child.stdin.write(b"D")
            child.stdin.flush()
            require(
                read_line(child.stdout, "extra-CUE open handshake").startswith(
                    b"EXTRA_CUE_OPEN "
                ),
                "Native lifecycle helper did not open the extra CUE.",
            )
            extra_cue_processes = boundary.duckstation_processes()
            require(
                len(extra_cue_processes) == 1
                and str(extra_cue_path) in extra_cue_processes[0].open_paths,
                "Real lsof did not observe the extra CUE descriptor.",
            )
            expect_refusal(
                lambda: native_coordinator._validate_open_disc_files(
                    extra_cue_processes[0], cue_real, candidate_bin
                ),
                "another CUE or BIN open",
            )
            child.stdin.write(b"d")
            child.stdin.flush()
            require(
                read_line(child.stdout, "extra-CUE close handshake") ==
                b"EXTRA_CUE_CLOSED",
                "Native lifecycle helper did not close the extra CUE.",
            )
            final_clean_processes = boundary.duckstation_processes()
            require(len(final_clean_processes) == 1,
                    "Native lifecycle helper disappeared before clean exit.")
            native_coordinator._validate_open_disc_files(
                final_clean_processes[0], cue_real, candidate_bin
            )

            child.stdin.write(b"Q")
            child.stdin.flush()
            require(
                read_line(child.stdout, "clean-exit handshake") == b"EXITING",
                "Native lifecycle helper did not acknowledge clean exit.",
            )
            return_code = child.wait(timeout=10.0)
            stderr = child.stderr.read()
            require(
                return_code == 0 and stderr == b"",
                "Native lifecycle helper failed: "
                + stderr.decode("utf-8", "replace"),
            )
            require(
                boundary.duckstation_processes() == []
                and boundary.listener_pids() == set(),
                "Native lifecycle helper or listener remained after clean exit.",
            )
        finally:
            if child is not None and child.poll() is None:
                child.terminate()
                try:
                    child.wait(timeout=5.0)
                except subprocess.TimeoutExpired:
                    child.kill()
                    child.wait(timeout=5.0)
            if child is not None:
                for stream in (child.stdin, child.stdout, child.stderr):
                    if stream is not None:
                        stream.close()
    require(temp_path is not None and not temp_path.exists(),
            "Native lifecycle temporary directory was not removed.")


def run_native_watcher_bootstrap_regression(
    repository_root: pathlib.Path,
) -> None:
    class ExitedChild:
        pid = 77777

        @staticmethod
        def poll() -> int:
            return 17

        @staticmethod
        def wait(timeout: float | None = None) -> int:
            del timeout
            return 17

        @staticmethod
        def send_signal(value: int) -> None:
            raise AssertionError(f"Exited diagnostic child was signalled: {value}")

    boundary = capture.RealMacBoundary.__new__(capture.RealMacBoundary)
    boundary.repository_root = pathlib.Path(os.path.abspath(repository_root))
    temp_path: pathlib.Path | None = None
    watcher_pycs_before = set(
        TOOL_PATH.parent.glob("__pycache__/Watch-SpyroId65RuntimeState.*.pyc")
    )
    with tempfile.TemporaryDirectory(prefix="id65-watcher-bootstrap-") as temp:
        # macOS exposes /var as a symlink; use its canonical private ancestry
        # because the production diagnostic reader deliberately rejects links.
        temp_path = pathlib.Path(temp).resolve(strict=True)
        output_path = temp_path / "watcher-self-test.log"
        output_fd = os.open(
            output_path,
            os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
            0o600,
        )
        with os.fdopen(output_fd, "wb", buffering=0) as output:
            owned_watcher: list[tuple[str, Any]] = []
            child, child_stdin = boundary.start_watcher_self_test(
                output, owned_watcher
            )
            require(
                owned_watcher == [("watcher", child)],
                "Native watcher self-test ownership handoff was not exact.",
            )
            child_stdin.close()
            return_code = child.wait(timeout=30.0)
            output.flush()
            os.fsync(output.fileno())
        require(
            return_code == 0
            and child.poll() == 0
            and output_path.read_bytes()
            == b"WATCH_SPYRO_ID65_RUNTIME_STATE_SELF_TEST_OK\n",
            "Production pinned-watcher bootstrap did not execute the exact self-test.",
        )

        diagnostic_path = temp_path / "watcher-early-exit.log"
        diagnostic = b"Pinned watcher bootstrap refused test bytes.\n"
        diagnostic_fd = os.open(
            diagnostic_path,
            os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
            0o600,
        )
        try:
            written = os.write(diagnostic_fd, diagnostic)
            require(written == len(diagnostic), "Diagnostic fixture write was partial.")
            os.fsync(diagnostic_fd)
        finally:
            os.close(diagnostic_fd)
        expect_refusal(
            lambda: boundary.wait_for_watcher_connection(
                ExitedChild(), diagnostic_path
            ),
            "exit status 17; output prefix b'Pinned watcher bootstrap refused test bytes.\\n'",
        )
        require(
            diagnostic_path.read_bytes() == diagnostic,
            "Early-exit watcher diagnostics were not preserved exactly.",
        )

    require(
        temp_path is not None
        and not temp_path.exists()
        and watcher_pycs_before
        == set(TOOL_PATH.parent.glob(
            "__pycache__/Watch-SpyroId65RuntimeState.*.pyc"
        )),
        "Native watcher bootstrap left temporary or bytecode debris.",
    )


def run_live_watcher_gate_regressions() -> int:
    base = dt.datetime(2026, 8, 11, 23, 0, tzinfo=dt.timezone.utc)

    def elapsed_samples(
        elapsed_values: list[int], *, state_value: int = 0
    ) -> list[dict[str, Any]]:
        return [
            {
                "atUtc": capture.format_utc(
                    base + dt.timedelta(milliseconds=elapsed)
                ),
                "elapsedMilliseconds": elapsed,
                "lives": 4,
                "ordinal": ordinal,
                "playerZ": 2048,
                "state": state_value,
            }
            for ordinal, elapsed in enumerate(elapsed_values)
        ]

    def samples(count: int, span_ms: int, *, state_value: int = 0) -> list[dict[str, Any]]:
        return elapsed_samples(
            [
                round(ordinal * span_ms / (count - 1)) if count > 1 else 0
                for ordinal in range(count)
            ],
            state_value=state_value,
        )

    def live_bytes(values: list[dict[str, Any]]) -> bytes:
        complete = capture._offline_watcher_log(values)
        preamble = capture.WATCHER_LIVE_PREAMBLE.encode("utf-8")
        trailer = capture.WATCHER_CLEAN_TRAILER.encode("utf-8")
        require(
            complete.startswith(preamble) and complete.endswith(trailer),
            "Offline watcher fixture diverged from the live parser envelope.",
        )
        block = complete[len(preamble):-len(trailer)]
        return preamble + block + b"\n"

    accepted = samples(19, 2_000)
    capture.CaptureCoordinator._require_gameplay_suffix(
        accepted, accepted[-1]
    )
    jittered_cover = elapsed_samples([ordinal * 101 for ordinal in range(21)])
    capture.CaptureCoordinator._require_gameplay_suffix(
        jittered_cover, jittered_cover[-1]
    )
    post_startup = elapsed_samples([ordinal * 100 for ordinal in range(43)])
    capture.CaptureCoordinator._require_gameplay_suffix(
        post_startup,
        post_startup[-1],
        strictly_after_ordinal=20,
    )
    expect_refusal(
        lambda: capture.CaptureCoordinator._require_gameplay_suffix(
            post_startup[:22],
            post_startup[21],
            strictly_after_ordinal=20,
        ),
        "begin after recording startup",
    )
    shortest_cover = elapsed_samples(
        [0, *range(100, 2_101, 100), 2_101]
    )
    shortest_cover[0]["state"] = 13
    capture.CaptureCoordinator._require_gameplay_suffix(
        shortest_cover, shortest_cover[-1]
    )
    unsafe_cover = copy.deepcopy(shortest_cover)
    unsafe_cover[0]["state"] = 0
    unsafe_cover[1]["state"] = 13
    expect_refusal(
        lambda: capture.CaptureCoordinator._require_gameplay_suffix(
            unsafe_cover, unsafe_cover[-1]
        ),
        "safe gameplay",
    )
    expect_refusal(
        lambda: capture.CaptureCoordinator._require_gameplay_suffix(
            samples(18, 2_000), samples(18, 2_000)[-1]
        ),
        "19-sample",
    )
    short = samples(19, 1_999)
    expect_refusal(
        lambda: capture.CaptureCoordinator._require_gameplay_suffix(
            short, short[-1]
        ),
        "2.000-second",
    )
    title = samples(19, 2_000, state_value=13)
    expect_refusal(
        lambda: capture.CaptureCoordinator._require_gameplay_suffix(
            title, title[-1]
        ),
        "safe gameplay",
    )
    unsafe_postconfirm = copy.deepcopy(accepted)
    unsafe_postconfirm[-1]["state"] = 13
    expect_refusal(
        lambda: capture.CaptureCoordinator._require_gameplay_suffix(
            unsafe_postconfirm, unsafe_postconfirm[-1]
        ),
        "safe gameplay",
    )
    confirmation = capture.ClockSample(capture.format_utc(base), 10)
    first_postconfirm = elapsed_samples([0, 100, 200])
    confirmation_50 = capture.ClockSample(
        capture.format_utc(base + dt.timedelta(milliseconds=50)), 20
    )
    require(
        capture._select_first_watcher_sample_after(
            first_postconfirm, confirmation_50
        )
        == first_postconfirm[1],
        "Readiness did not retain the first post-confirmation watcher endpoint.",
    )
    lag_250 = samples(2, 250)
    require(
        capture._select_first_watcher_sample_after(lag_250[1:], confirmation)
        == lag_250[1],
        "A watcher sample exactly 250ms after confirmation was not accepted.",
    )
    lag_251 = samples(2, 251)
    expect_refusal(
        lambda: capture._select_first_watcher_sample_after(
            lag_251[1:], confirmation
        ),
        "within 250ms",
    )
    clock_gap = elapsed_samples([0, 100, 200, 451])
    expect_refusal(
        lambda: capture._parse_live_watcher_bytes(live_bytes(clock_gap)),
        "clocks are discontinuous",
    )
    started = capture.ClockSample(capture.format_utc(base), 1_000_000)
    expect_refusal(
        lambda: capture._require_minimum_interval(
            started,
            capture.ClockSample(capture.format_utc(base), 9_499_999),
            8_500_000,
            "8.5-second boundary",
        ),
        "shorter",
    )
    capture._require_minimum_interval(
        started,
        capture.ClockSample(capture.format_utc(base), 9_500_000),
        8_500_000,
        "8.5-second boundary",
    )
    hold = samples(190, 20_000)
    capture._require_safe_hold_samples(hold)
    unsafe_hold = copy.deepcopy(hold)
    unsafe_hold[len(unsafe_hold) // 2]["state"] = 13
    expect_refusal(
        lambda: capture._require_safe_hold_samples(unsafe_hold),
        "continuous safe",
    )

    growing_values = copy.deepcopy(accepted)
    growing_values.append({
        "atUtc": capture.format_utc(base + dt.timedelta(milliseconds=2_100)),
        "elapsedMilliseconds": 2_100,
        "lives": 4,
        "ordinal": 19,
        "playerZ": 2048,
        "state": 0,
    })
    full = live_bytes(growing_values)
    prior = live_bytes(accepted)
    require(full.startswith(prior), "Live watcher fixtures are not byte-prefix growing.")
    delta = full[len(prior):]
    split = max(1, len(delta) // 2)
    require(
        len(capture._parse_live_watcher_bytes(prior + delta[:split])) == 19,
        "Live parser did not preserve an incomplete trailing sample.",
    )
    require(
        len(capture._parse_live_watcher_bytes(full)) == 20,
        "Live parser did not accept the completed trailing sample.",
    )
    expect_refusal(
        lambda: capture._parse_live_watcher_bytes(
            prior + b"malformed complete watcher line\n"
        ),
        "unapproved interior line",
    )

    refusal_count = 1
    with tempfile.TemporaryDirectory(prefix="id65-live-watcher-reader-") as temp:
        root = pathlib.Path(temp).resolve(strict=True)
        os.chmod(root, 0o700)

        def fixture() -> tuple[pathlib.Path, Any, Any]:
            directory = root / f"case-{len(list(root.iterdir()))}"
            directory.mkdir(mode=0o700)
            path = directory / "watcher-stdout.log"
            fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
            writer = os.fdopen(fd, "wb", buffering=0)
            writer.write(prior)
            reader = capture.IncrementalWatcherReader(directory, writer.fileno())
            require(len(reader.snapshot()) == 19, "Anchored reader lost its first prefix.")
            return path, writer, reader

        path, writer, reader = fixture()
        writer.write(delta[:split])
        require(len(reader.snapshot()) == 19, "Anchored reader rejected partial growth.")
        writer.write(delta[split:])
        require(len(reader.snapshot()) == 20, "Anchored reader rejected complete growth.")
        reader.close()
        writer.close()

        for mutation, expected in (
            ("shrink", "shrank"),
            ("rewrite", "rewrote"),
            ("same-rewrite", "rewrote"),
            ("replace", "path or inode changed"),
        ):
            path, writer, reader = fixture()
            if mutation == "shrink":
                os.ftruncate(writer.fileno(), len(prior) - 1)
            elif mutation == "rewrite":
                os.pwrite(writer.fileno(), b"X", 0)
            elif mutation == "same-rewrite":
                os.pwrite(writer.fileno(), prior[:1], 0)
            else:
                replacement = path.with_name("replacement.log")
                replacement.write_bytes(prior)
                os.chmod(replacement, 0o600)
                path.unlink()
                replacement.rename(path)
            expect_refusal(reader.snapshot, expected)
            refusal_count += 1
            reader.close()
            writer.close()
    return refusal_count


def run_native_tty_countdown_regression() -> None:
    pid, master_fd = pty.fork()
    if pid == 0:
        try:
            boundary = capture.RealMacBoundary.__new__(capture.RealMacBoundary)
            boundary._last_monotonic = -1
            boundary.require_capture_environment = lambda: None
            started = boundary.clock()
            boundary.wait_for_fresh_return_after(
                started,
                500_000,
                "COUNTDOWN ELIGIBLE — press a fresh Return.",
            )
            os.write(STDOUT_FILENO := 1, b"COUNTDOWN_FRESH_RETURN_OK\n")
            os._exit(0)
        except BaseException as exc:
            os.write(2, ("COUNTDOWN_CHILD_ERROR " + repr(exc) + "\n").encode())
            os._exit(97)

    output = bytearray()
    status: int | None = None

    def read_until(fragment: bytes, timeout: float) -> None:
        deadline = capture.time.monotonic() + timeout
        while fragment not in output:
            remaining = deadline - capture.time.monotonic()
            if remaining <= 0:
                raise AssertionError(
                    f"Native TTY countdown timed out waiting for {fragment!r}: "
                    + output.decode("utf-8", "replace")
                )
            readable, _, _ = select.select([master_fd], [], [], remaining)
            if not readable:
                continue
            try:
                chunk = os.read(master_fd, 4096)
            except OSError:
                chunk = b""
            if not chunk:
                break
            output.extend(chunk)

    try:
        read_until(b"TIMER RUNNING", 5.0)
        # A single buffered readline can prefetch this entire burst. Production
        # must consume through the same raw descriptor used by select so none of
        # these early Returns can satisfy the later eligible prompt.
        os.write(master_fd, b"\n\n\n")
        read_until(b"Too early:", 5.0)
        read_until(b"MINIMUM MET", 5.0)
        require(
            b"COUNTDOWN_FRESH_RETURN_OK" not in output,
            "A burst of buffered early Returns bypassed the production countdown.",
        )
        os.write(master_fd, b"\n")
        read_until(b"COUNTDOWN_FRESH_RETURN_OK", 5.0)
        _, status = os.waitpid(pid, 0)
        require(
            os.waitstatus_to_exitcode(status) == 0
            and output.count(b"Too early:") == 3
            and b"nothing was recorded" in output
            and b"COUNTDOWN ELIGIBLE" in output,
            "Production countdown did not distinguish early and fresh Returns: "
            + output.decode("utf-8", "replace"),
        )
    finally:
        if status is None:
            try:
                os.kill(pid, signal.SIGTERM)
            except ProcessLookupError:
                pass
            os.waitpid(pid, 0)
        os.close(master_fd)


def run_native_fresh_visual_confirmation_regression() -> None:
    pid, master_fd = pty.fork()
    if pid == 0:
        try:
            boundary = capture.RealMacBoundary.__new__(capture.RealMacBoundary)
            boundary._last_monotonic = -1
            boundary.require_capture_environment = lambda: None
            for phase in (
                b"READINESS_PHASE\n",
                b"STATE3_PHASE\n",
                b"STATE1_PHASE\n",
                b"SETTLED_SUFFIX_PHASE\n",
            ):
                os.write(1, phase)
                capture.time.sleep(0.1)
            boundary.fresh_confirm_before_deadline(
                "FRESH VISUAL ELIGIBLE — press one new Return.",
                capture.time.monotonic_ns() // 1000 + 5_000_000,
            )
            os.write(1, b"FRESH_VISUAL_RETURN_OK\n")
            os._exit(0)
        except BaseException as exc:
            os.write(2, ("FRESH_VISUAL_CHILD_ERROR " + repr(exc) + "\n").encode())
            os._exit(97)

    output = bytearray()
    status: int | None = None

    def read_until(fragment: bytes, timeout: float) -> None:
        deadline = capture.time.monotonic() + timeout
        while fragment not in output:
            remaining = deadline - capture.time.monotonic()
            if remaining <= 0:
                raise AssertionError(
                    f"Fresh visual TTY timed out waiting for {fragment!r}: "
                    + output.decode("utf-8", "replace")
                )
            readable, _, _ = select.select([master_fd], [], [], remaining)
            if not readable:
                continue
            try:
                chunk = os.read(master_fd, 4096)
            except OSError:
                chunk = b""
            if not chunk:
                break
            output.extend(chunk)

    try:
        for phase in (
            b"READINESS_PHASE",
            b"STATE3_PHASE",
            b"STATE1_PHASE",
            b"SETTLED_SUFFIX_PHASE",
        ):
            read_until(phase, 5.0)
            os.write(master_fd, b"\n")
        read_until(b"FRESH VISUAL ELIGIBLE", 5.0)
        require(
            b"FRESH_VISUAL_RETURN_OK" not in output
            and b"QUEUED RETURNS DRAINED (4)" in output,
            "Queued pre-prompt Returns satisfied the visual confirmation.",
        )
        os.write(master_fd, b"\n")
        read_until(b"FRESH_VISUAL_RETURN_OK", 5.0)
        _, status = os.waitpid(pid, 0)
        require(
            os.waitstatus_to_exitcode(status) == 0,
            "Fresh visual confirmation did not accept one post-prompt Return: "
            + output.decode("utf-8", "replace"),
        )
    finally:
        if status is None:
            try:
                os.kill(pid, signal.SIGTERM)
            except ProcessLookupError:
                pass
            os.waitpid(pid, 0)
        os.close(master_fd)


def run_native_screencapture_regression(repository_root: pathlib.Path) -> None:
    """Exercise both production media paths against one native Cocoa window."""

    helper_source = r'''
#import <Cocoa/Cocoa.h>
#import <dispatch/dispatch.h>
#include <stdio.h>
#include <unistd.h>

int main(void) {
    @autoreleasepool {
        NSApplication *app = [NSApplication sharedApplication];
        [app setActivationPolicy:NSApplicationActivationPolicyAccessory];
        NSRect rect = NSMakeRect(120.0, 120.0, 640.0, 480.0);
        NSWindow *window = [[NSWindow alloc]
            initWithContentRect:rect
                      styleMask:(NSWindowStyleMaskTitled |
                                 NSWindowStyleMaskClosable)
                        backing:NSBackingStoreBuffered
                          defer:NO];
        [window setTitle:@"ID65 Native Screencapture Gate"];
        [window setBackgroundColor:[NSColor colorWithCalibratedRed:0.18
                                                            green:0.34
                                                             blue:0.72
                                                            alpha:1.0]];
        [window makeKeyAndOrderFront:nil];
        [app activateIgnoringOtherApps:YES];
        setvbuf(stdout, NULL, _IONBF, 0);
        printf("WINDOW %ld\n", (long)[window windowNumber]);
        dispatch_source_t source = dispatch_source_create(
            DISPATCH_SOURCE_TYPE_READ, STDIN_FILENO, 0,
            dispatch_get_main_queue());
        dispatch_source_set_event_handler(source, ^{
            char value = 0;
            ssize_t count = read(STDIN_FILENO, &value, 1);
            if (count <= 0 || value == 'Q') {
                [app terminate:nil];
            }
        });
        dispatch_resume(source);
        [app run];
    }
    return 0;
}
'''.lstrip()

    root_dash = repository_root / "-"
    system_root_dash = pathlib.Path("/-")
    require(
        not root_dash.exists() and not root_dash.is_symlink()
        and not system_root_dash.exists() and not system_root_dash.is_symlink(),
        "A pre-existing literal '-' file blocks the native media debris proof.",
    )
    cocoa_child: subprocess.Popen[bytes] | None = None
    recording_child: Any | None = None
    temporary_path: pathlib.Path | None = None
    with tempfile.TemporaryDirectory(prefix="id65-native-screencapture-") as temporary:
        temporary_path = pathlib.Path(temporary).resolve(strict=True)
        source_path = temporary_path / "CaptureWindow.m"
        helper_path = temporary_path / "CaptureWindow"
        source_path.write_bytes(helper_source.encode("utf-8", "strict"))
        compiled = subprocess.run(
            [
                "/usr/bin/clang", "-fobjc-arc", "-fblocks", "-Wall", "-Wextra",
                "-Werror", "-framework", "Cocoa", "-o", str(helper_path),
                str(source_path),
            ],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
            close_fds=True,
            timeout=30.0,
            env={"LANG": "C", "LC_ALL": "C", "PATH": "/usr/bin:/bin"},
        )
        require(
            compiled.returncode == 0 and helper_path.is_file(),
            "Fixed clang failed to build the Cocoa capture window: "
            + compiled.stderr.decode("utf-8", "replace"),
        )

        fake_repository = temporary_path / "repository"
        fake_repository.mkdir(mode=0o700)
        current = fake_repository
        for part in capture.guard_relative_path(capture.CAPTURE_STAGING_PREFIX):
            current = current / part
            current.mkdir(mode=0o700)
        run_id = "id65v3-20260811t230000z-0123456789abcdef"
        run_path = current / run_id
        run_path.mkdir(mode=0o700)
        session_path = run_path / f"00-{capture.SESSION_IDS[0]}"
        session_path.mkdir(mode=0o700)
        boundary = capture.RealMacBoundary(fake_repository)

        try:
            cocoa_child = subprocess.Popen(
                [str(helper_path)],
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                env={"LANG": "C", "LC_ALL": "C", "PATH": "/usr/bin:/bin"},
                close_fds=True,
            )
            require(
                cocoa_child.stdin is not None
                and cocoa_child.stdout is not None
                and cocoa_child.stderr is not None,
                "Native Cocoa helper pipes were not created.",
            )
            readable, _, _ = select.select([cocoa_child.stdout], [], [], 10.0)
            require(bool(readable), "Native Cocoa helper did not publish its window ID.")
            line = cocoa_child.stdout.readline().rstrip(b"\r\n")
            match = re.fullmatch(rb"WINDOW ([1-9][0-9]*)", line)
            require(match is not None, "Native Cocoa helper published an invalid window ID.")
            published_window_id = int(match.group(1))
            window: capture.WindowObservation | None = None
            window_deadline = capture.time.monotonic() + 10.0
            while capture.time.monotonic() < window_deadline:
                try:
                    observed = boundary.observe_window(cocoa_child.pid)
                except capture.CaptureRefusal:
                    capture.time.sleep(0.05)
                    continue
                if observed.window_id == published_window_id:
                    window = observed
                    break
                capture.time.sleep(0.05)
            require(
                window is not None and window.width >= 640 and window.height >= 480,
                "Native Cocoa helper was not visible through the production window query.",
            )
            directory_identity = capture._private_directory_identity(session_path)

            boundary.capture_frame(
                window,
                session_path,
                "lone-pad.png",
                capture.time.monotonic_ns() // 1000 + 30_000_000,
            )
            frame = capture.read_regular_file(
                session_path / "lone-pad.png",
                include_bytes=True,
                expected_owner=os.getuid(),
                expected_mode=0o600,
                maximum_bytes=capture.MAX_TEXT_BYTES,
                expected_parent_identity=directory_identity,
            )
            capture._validate_png_bytes(frame.bytes_value or b"")
            require(frame.byte_length > 0, "Native frame capture was empty.")

            owned_recording: list[tuple[str, Any]] = []
            recording_child = boundary.start_recording(
                window,
                session_path,
                capture.SCREEN_RECORDING_LEAF,
                owned_recording,
            )
            require(
                owned_recording == [("recorder", recording_child)],
                "Native recorder ownership handoff was not exact.",
            )
            require(recording_child.poll() is None, "Native recorder exited at startup.")
            # A short real interval is enough to force at least one encoded video frame.
            capture.time.sleep(1.0)
            boundary.stop_owned_child(recording_child, "screen recorder")
            recording_child = None
            recording = capture.read_regular_file(
                session_path / capture.SCREEN_RECORDING_LEAF,
                expected_owner=os.getuid(),
                expected_mode=0o600,
                validator=capture._validate_iso_bmff_fd,
                expected_parent_identity=directory_identity,
            )
            require(recording.byte_length > 0, "Native window recording was empty.")

            collision_leaf = "edge-before.png"
            collision = session_path / collision_leaf
            collision.write_bytes(b"collision")
            os.chmod(collision, 0o600)
            expect_refusal(
                lambda: boundary.capture_frame(
                    window,
                    session_path,
                    collision_leaf,
                    capture.time.monotonic_ns() // 1000 + 30_000_000,
                ),
                "already exists",
            )
            collision.unlink()
            collision.symlink_to("missing-target")
            expect_refusal(
                lambda: boundary.capture_frame(
                    window,
                    session_path,
                    collision_leaf,
                    capture.time.monotonic_ns() // 1000 + 30_000_000,
                ),
                "already exists",
            )
            collision.unlink()

            hardlink_source = session_path / "hardlink-source.png"
            hardlink_source.write_bytes(frame.bytes_value or b"")
            os.chmod(hardlink_source, 0o600)
            os.link(hardlink_source, collision)
            expect_refusal(
                lambda: capture.read_regular_file(
                    collision,
                    expected_owner=os.getuid(),
                    expected_mode=0o600,
                ),
                "single-link",
            )
            collision.unlink()
            hardlink_source.unlink()

            expect_refusal(
                lambda: boundary.capture_frame(
                    window,
                    temporary_path,
                    "edge-before.png",
                    capture.time.monotonic_ns() // 1000 + 30_000_000,
                ),
                "outside the authoritative repository",
            )
            expect_refusal(
                lambda: boundary.capture_frame(
                    window,
                    session_path,
                    "unapproved.png",
                    capture.time.monotonic_ns() // 1000 + 30_000_000,
                ),
                "not approved",
            )

            moved = run_path / "00-candidate-primary-edge-0-moved"
            session_path.rename(moved)
            session_path.mkdir(mode=0o700)
            rebound = session_path / "edge-after.png"
            rebound.write_bytes(frame.bytes_value or b"")
            os.chmod(rebound, 0o600)
            expect_refusal(
                lambda: capture.read_regular_file(
                    rebound,
                    expected_owner=os.getuid(),
                    expected_mode=0o600,
                    expected_parent_identity=directory_identity,
                ),
                "directory identity changed",
            )
        finally:
            if recording_child is not None and recording_child.poll() is None:
                recording_child.send_signal(signal.SIGINT)
                recording_child.wait(timeout=15.0)
            if cocoa_child is not None and cocoa_child.poll() is None:
                if cocoa_child.stdin is not None:
                    cocoa_child.stdin.write(b"Q")
                    cocoa_child.stdin.flush()
                cocoa_child.wait(timeout=10.0)
            if cocoa_child is not None:
                stderr = cocoa_child.stderr.read() if cocoa_child.stderr is not None else b""
                require(
                    cocoa_child.returncode == 0 and stderr == b"",
                    "Native Cocoa helper did not exit cleanly: "
                    + stderr.decode("utf-8", "replace"),
                )
                for stream in (cocoa_child.stdin, cocoa_child.stdout, cocoa_child.stderr):
                    if stream is not None:
                        stream.close()

        require(
            not (fake_repository / "-").exists()
            and not (fake_repository / "-").is_symlink(),
            "Production screencapture created a literal repository-root '-' file.",
        )
    require(
        temporary_path is not None
        and not temporary_path.exists()
        and not root_dash.exists()
        and not root_dash.is_symlink()
        and not system_root_dash.exists()
        and not system_root_dash.is_symlink(),
        "Native screencapture left temporary or literal '-' debris.",
    )


def run(*, offline_only: bool = False) -> None:
    require(type(offline_only) is bool, "Offline-only selector is not exact bool.")
    source_text = TOOL_PATH.read_text(encoding="utf-8")
    capture._require_recording_duration(0, 1)
    capture._require_recording_duration(
        10,
        10 + capture.MAX_RECORDING_DURATION_MICROSECONDS,
    )
    duration_refusals = 0
    for end in (10, 10 + capture.MAX_RECORDING_DURATION_MICROSECONDS + 1):
        try:
            capture._require_recording_duration(10, end)
        except capture.CaptureRefusal:
            duration_refusals += 1
        else:
            raise AssertionError("Recording-duration boundary tamper was accepted.")
    for start, end in ((-1, 0), (2, 1)):
        try:
            capture._require_recording_duration(start, end)
        except capture.CaptureRefusal:
            duration_refusals += 1
        else:
            raise AssertionError("Recording-duration clock tamper was accepted.")
    require(
        duration_refusals == 4
        and capture.MAX_RECORDING_DURATION_MICROSECONDS == 1_800_000_000,
        "The exact 30-minute recording-duration boundary is not closed.",
    )
    require(
        capture.MAX_FALL_AUTHORIZATION_BINDING_MILLISECONDS == 30_000,
        "Fall authorization binding window is not exactly 30 seconds.",
    )
    require(
        capture.MAX_POST_DEATH_RECOVERY_MILLISECONDS == 15_000,
        "Post-death recovery bound is not exactly 15 seconds.",
    )
    capture._require_maximum_file_bytes(
        capture.MAX_MEDIA_BYTES,
        capture.MAX_MEDIA_BYTES,
        pathlib.Path("synthetic-at-cap.mov"),
    )
    expect_refusal(
        lambda: capture._require_maximum_file_bytes(
            capture.MAX_MEDIA_BYTES + 1,
            capture.MAX_MEDIA_BYTES,
            pathlib.Path("synthetic-over-cap.mov"),
        ),
        "exceeds its bounded size",
    )
    require(
        capture.MAX_MEDIA_BYTES == 64 * 1024 * 1024 * 1024,
        "Python and C# media-size caps diverged.",
    )
    with tempfile.TemporaryDirectory(
        prefix="id65-bounded-stream-growth-"
    ) as growth_temporary:
        growth_root = pathlib.Path(os.path.realpath(growth_temporary))
        os.chmod(growth_root, 0o700)
        growth_path = growth_root / "growing.mov"
        growth_path.write_bytes(b"1234")
        os.chmod(growth_path, 0o600)
        growth_identity = os.stat(growth_path, follow_symlinks=False)
        growth_fd = os.open(growth_path, os.O_WRONLY | os.O_APPEND)
        original_os_read = capture.os.read
        growth_injected = False

        def growing_read(fd: int, byte_count: int) -> bytes:
            nonlocal growth_injected
            info = os.fstat(fd)
            if (
                not growth_injected
                and info.st_dev == growth_identity.st_dev
                and info.st_ino == growth_identity.st_ino
            ):
                os.write(growth_fd, b"5")
                os.fsync(growth_fd)
                growth_injected = True
            return original_os_read(fd, byte_count)

        capture.os.read = growing_read
        try:
            expect_refusal(
                lambda: capture.read_regular_file(
                    growth_path,
                    expected_owner=os.getuid(),
                    expected_mode=0o600,
                    maximum_bytes=4,
                ),
                "exceeds its bounded size",
            )
        finally:
            capture.os.read = original_os_read
            os.close(growth_fd)
        require(
            growth_injected,
            "Bounded stream growth fixture did not grow during read.",
        )
    fall_base = dt.datetime(2026, 8, 10, 19, 0, tzinfo=dt.timezone.utc)
    def fall_sample(at: dt.datetime, ordinal: int, lives: int, state: int, z: int) -> dict[str, Any]:
        return {
            "atUtc": capture.format_utc(at),
            "elapsedMilliseconds": ordinal * 100,
            "lives": lives,
            "ordinal": ordinal,
            "playerZ": z,
            "state": state,
        }
    at_limit = [
        fall_sample(fall_base + dt.timedelta(milliseconds=offset), ordinal, 4, 0, 1162)
        for ordinal, offset in enumerate(range(0, 30_000, 100))
    ]
    at_limit.append(
        fall_sample(fall_base + dt.timedelta(seconds=30), len(at_limit), 3, 4, 882)
    )
    fall_ordinal = len(at_limit) - 1
    require(
        capture._fall_binding_is_accepted(at_limit, fall_base, fall_ordinal)
        and capture._fall_binding_is_accepted(
            at_limit,
            fall_base + dt.timedelta(seconds=30),
            fall_ordinal,
        ),
        "Fall authorization rejected its inclusive 0/30-second boundaries.",
    )
    after_limit = copy.deepcopy(at_limit)
    after_limit[fall_ordinal]["atUtc"] = capture.format_utc(
        fall_base + dt.timedelta(milliseconds=30_001)
    )
    before_cue = copy.deepcopy(at_limit)
    before_cue[fall_ordinal]["atUtc"] = capture.format_utc(
        fall_base - dt.timedelta(milliseconds=1)
    )
    unsafe_interior = copy.deepcopy(at_limit)
    unsafe_interior[150]["state"] = 13
    wrong_edge = copy.deepcopy(at_limit)
    wrong_edge[fall_ordinal - 1]["playerZ"] = 900
    require(
        not capture._fall_binding_is_accepted(after_limit, fall_base, fall_ordinal)
        and not capture._fall_binding_is_accepted(before_cue, fall_base, fall_ordinal)
        and not capture._fall_binding_is_accepted(unsafe_interior, fall_base, fall_ordinal)
        and not capture._fall_binding_is_accepted(wrong_edge, fall_base, fall_ordinal),
        "Fall authorization admitted an out-of-window, unsafe-interior, or wrong edge.",
    )
    respawn_base = dt.datetime(2026, 8, 10, 19, 40, tzinfo=dt.timezone.utc)
    recovery_limit = [fall_sample(respawn_base, 0, 4, 0, 1162)]
    recovery_limit.extend(
        fall_sample(
            respawn_base + dt.timedelta(milliseconds=ordinal * 100),
            ordinal,
            3,
            4,
            882,
        )
        for ordinal in range(1, 151)
    )
    recovery_limit.append(
        fall_sample(
            respawn_base + dt.timedelta(milliseconds=15_100),
            151,
            3,
            0,
            1200,
        )
    )
    require(
        capture._respawn_binding_is_accepted(
            recovery_limit,
            1,
            respawn_base + dt.timedelta(milliseconds=15_100),
            151,
        ),
        "Respawn binding rejected exact 15,000ms recovery with S=R=N.",
    )
    recovery_late = copy.deepcopy(recovery_limit)
    recovery_late[151]["atUtc"] = capture.format_utc(
        respawn_base + dt.timedelta(milliseconds=15_101)
    )
    recovery_late[151]["elapsedMilliseconds"] = 15_101
    require(
        not capture._respawn_binding_is_accepted(
            recovery_late,
            1,
            respawn_base + dt.timedelta(milliseconds=15_101),
            151,
        )
        and not capture._respawn_binding_is_accepted(
            recovery_limit,
            1,
            respawn_base + dt.timedelta(milliseconds=15_000),
            151,
        )
        and not capture._respawn_binding_is_accepted(
            recovery_limit,
            1,
            respawn_base + dt.timedelta(milliseconds=15_100),
            152,
        ),
        "Respawn binding admitted late S, non-first N, or R>N.",
    )
    require(
        capture.EXPECTED_SETTINGS_SHA256
        == "469ef98771f26395352bac1d43f90e4be14693fd4ba5316e4d71e0557a7f74f9"
        and capture.EXPECTED_RUNTIME_PROFILE_SHA256
        == "29d01f1b791e4dc08ebfc4d62cf5a1b91600a5c55ec175ac6da45b13123100ad"
        and "6e024a6a245835b2f21f1da942a7b3c18dd1284c4110f6895bb08575bc031d8c"
        not in source_text
        and "27cd5d9083257632f035e0bee7f01b5761b79af032a16d682ab197a0bb8ea790"
        not in source_text
        and "5d2bb1f72a2e19735b9d4dcdb25288993c13c18aabd1dc8d8c636a3fad173ec3"
        not in source_text,
        "The deliberate stable settings/profile repin retained obsolete authority.",
    )
    forbidden_source = (
        "osascript",
        "pyautogui",
        "CGRequestScreenCaptureAccess",
        "shell=True",
        ".kill(",
        "CreateBIN",
        "FocusedRuntimePass",
        "DeclaredState",
        "RecordedAtUtc",
    )
    for token in forbidden_source:
        require(token not in source_text, f"Forbidden producer surface is present: {token}")
    require(
        "subprocess.Popen" in source_text
        and "Watch-SpyroId65RuntimeState.py" in source_text
        and "/usr/sbin/screencapture" in source_text,
        "Producer does not expose only its two expected owned subprocess families.",
    )
    require(
        re.search(r"\binput\(", source_text) is None,
        "Producer reintroduced buffered input() into a raw-fd capture workflow.",
    )
    require(
        "PROC_UID_ONLY = 4" in source_text
        and "PROC_" + "ALL" + "_PIDS" not in source_text,
        "Producer does not use only current-effective-UID libproc enumeration.",
    )
    require(
        "kern.host" + "uuid" not in source_text
        and "kern.boot" + "uuid" not in source_text,
        "Producer retains an obsolete or fallback boot UUID source.",
    )
    shared_authority_rejects = run_shared_coordinator_authority_regressions()
    source_closure_rejects = run_source_closure_regressions()
    source_topology_rejects = run_source_topology_regressions()
    verifier_binding_rejects = run_verifier_binding_regressions()
    kern_home_rejects = run_kern_procargs_home_regressions()
    snapshot_v2_rejects = run_snapshot_v2_regressions()
    require(
        shared_authority_rejects == 4
        and source_closure_rejects == 4
        and source_topology_rejects == 4
        and verifier_binding_rejects == 4
        and kern_home_rejects == 7
        and snapshot_v2_rejects == 10,
        "The shared-authority/source-closure refusal matrices did not execute exactly.",
    )
    boundary_refusals = run_macos_boundary_regressions()
    boot_boundary_refusals = run_boot_identity_regressions()
    live_watcher_refusals = run_live_watcher_gate_regressions()
    run_native_tty_countdown_regression()
    run_native_fresh_visual_confirmation_regression()
    run_native_watcher_bootstrap_regression(TOOL_PATH.parent.parent)
    run_native_open_file_lifecycle_with_injected_procargs_regression(
        TOOL_PATH.parent.parent
    )
    native_screencapture_count = 0
    native_aqua_media_gate = "SKIPPED" if offline_only else "PASSED"
    if not offline_only:
        run_native_screencapture_regression(TOOL_PATH.parent.parent)
        native_screencapture_count = 1

    parser = capture.build_parser()
    for argv in (
        ["capture-session", "../escape", capture.SESSION_IDS[0]],
        ["capture-session", "id65v3-20260810t200000z-0123456789abcdef", "made-up"],
        ["preflight", "--timestamp", "2026-08-10T00:00:00.000Z"],
        ["verify-staging", "id65v3-20260810t200000z-0123456789abcdef", "extra"],
    ):
        try:
            parsed = parser.parse_args(argv)
            if getattr(parsed, "run_id", "").startswith("../"):
                expect_refusal(
                    lambda: capture.CaptureCoordinator.__new__(
                        capture.CaptureCoordinator
                    )._load_run(parsed.run_id)
                )
            else:
                raise AssertionError("Strict CLI accepted caller provenance or unknown session.")
        except SystemExit as exc:
            require(exc.code == 2, "Strict CLI rejection returned an unexpected exit code.")

    with tempfile.TemporaryDirectory(prefix="id65-v3-capture-smoke-") as temp:
        # macOS exposes /var as a symlink; the production opener deliberately
        # refuses symlink ancestry, so make the injected root canonical too.
        root = pathlib.Path(temp).resolve(strict=True)
        source = FakeSource(root)
        boundary = FakeBoundary(root, source)
        lease_state = {"active": False}

        @contextlib.contextmanager
        def observed_lease() -> Any:
            require(not lease_state["active"], "Test lease was re-entered.")
            lease_state["active"] = True
            try:
                yield
            finally:
                lease_state["active"] = False

        source.prelaunch_probe = lambda: require(
            lease_state["active"]
            and not (root / capture.CAPTURE_STAGING_PREFIX).exists(),
            "Full prelaunch proof did not run under the held lease before staging.",
        )
        coordinator = capture.CaptureCoordinator(
            boundary,
            source_guard=source,
            lease_factory=observed_lease,
            run_id_factory=lambda sample: (
                "id65v3-20260810t200000z-0123456789abcdef"
            ),
        )
        coordinator.preflight()

        valid_process = boundary._make_process(4242, boundary.base)
        coordinator._validate_process(valid_process)
        cue_identity, _ = source.candidate_identities()
        transient_cue_open = capture.OpenFileObservation(
            "22r",
            cue_identity.device,
            cue_identity.inode,
            cue_identity.byte_length,
            1,
            cue_identity.real_path,
        )
        process_while_cue_open = dataclasses.replace(
            valid_process,
            open_files=(*valid_process.open_files, transient_cue_open),
        )
        require(
            not capture._candidate_process_lifecycle_ready(
                process_while_cue_open,
                os.path.realpath(source.cue_path),
                os.path.realpath(source.bin_path),
            )
            and capture._candidate_process_lifecycle_ready(
                valid_process,
                os.path.realpath(source.cue_path),
                os.path.realpath(source.bin_path),
            ),
            "CUE-open to CUE-closed lifecycle did not converge on the exact open BIN.",
        )

        def with_argv(*argv: str) -> Any:
            values = tuple(argv)
            return dataclasses.replace(
                valid_process,
                argv=values,
                argv_nul_sha256=hashlib.sha256(
                    b"\0".join(item.encode("utf-8", "strict") for item in values)
                    + b"\0"
                ).hexdigest(),
            )

        executable_arg, cue_arg = valid_process.argv
        expect_refusal(
            lambda: coordinator._validate_process(
                with_argv(executable_arg, "/tmp/wrong.cue")
            ),
            "external-HOME DuckStation",
        )
        expect_refusal(
            lambda: coordinator._validate_process(with_argv(executable_arg)),
            "external-HOME DuckStation",
        )
        expect_refusal(
            lambda: coordinator._validate_process(
                with_argv(executable_arg, cue_arg, cue_arg)
            ),
            "external-HOME DuckStation",
        )
        expect_refusal(
            lambda: coordinator._validate_process(
                with_argv(executable_arg, cue_arg, "/tmp/extra.bin")
            ),
            "external-HOME DuckStation",
        )
        expect_refusal(
            lambda: coordinator._validate_process(
                dataclasses.replace(valid_process, argv_nul_sha256="f" * 64)
            ),
            "external-HOME DuckStation",
        )

        source.cue_identity_path_override = "/tmp/source-drift.cue"
        expect_refusal(
            lambda: coordinator._validate_process(valid_process),
            "source identity changed",
        )
        source.cue_identity_path_override = None
        source.cue_hash_override = "f" * 64
        expect_refusal(
            lambda: coordinator._validate_process(valid_process),
            "source identity changed",
        )
        source.cue_hash_override = None

        bin_open = valid_process.open_files[0]

        def opened_file(
            path: pathlib.Path, fd: str, *, link_count: int = 1
        ) -> Any:
            info = path.stat()
            return capture.OpenFileObservation(
                fd,
                info.st_dev,
                info.st_ino,
                info.st_size,
                link_count,
                os.path.realpath(path),
            )

        cache_open = opened_file(source.cache_path, "34u")
        process_with_cache = dataclasses.replace(
            valid_process, open_files=(bin_open, cache_open)
        )
        coordinator._validate_process(process_with_cache)
        for changed_cache, expected_fragment in (
            ((cache_open, cache_open), "duplicate allowed shader-cache"),
            ((dataclasses.replace(cache_open, fd="34r"),),
             "not one update-mode single-link"),
            ((dataclasses.replace(cache_open, fd="34w"),),
             "not one update-mode single-link"),
            ((dataclasses.replace(cache_open, link_count=2),),
             "not one update-mode single-link"),
            ((dataclasses.replace(cache_open, device=cache_open.device + 1),),
             "differs from its stable current file"),
            ((dataclasses.replace(cache_open, inode=cache_open.inode + 1),),
             "differs from its stable current file"),
            ((dataclasses.replace(
                cache_open, byte_length=cache_open.byte_length + 1
            ),), "differs from its stable current file"),
        ):
            expect_refusal(
                lambda changed_cache=changed_cache: coordinator._validate_process(
                    dataclasses.replace(
                        valid_process,
                        open_files=(bin_open, *changed_cache),
                    )
                ),
                expected_fragment,
            )

        original_cache_bytes = source.cache_path.read_bytes()
        source.cache_path.chmod(0o600)
        try:
            expect_refusal(
                lambda: coordinator._validate_process(process_with_cache),
                "wrong mode",
            )
        finally:
            source.cache_path.chmod(0o644)

        with source.cache_path.open("r+b") as oversized:
            oversized.truncate(capture.MAX_PROCESS_CACHE_BIN_BYTES + 1)
        oversized_cache = opened_file(source.cache_path, "34u")
        try:
            expect_refusal(
                lambda: coordinator._validate_process(
                    dataclasses.replace(
                        valid_process,
                        open_files=(bin_open, oversized_cache),
                    )
                ),
                "exceeds its bounded size",
            )
        finally:
            source.cache_path.write_bytes(original_cache_bytes)
            source.cache_path.chmod(0o644)
        cache_open = opened_file(source.cache_path, "34u")
        process_with_cache = dataclasses.replace(
            valid_process, open_files=(bin_open, cache_open)
        )

        cache_target = source.cache_path.with_name("vulkan_shaders.target")
        source.cache_path.rename(cache_target)
        source.cache_path.symlink_to(cache_target.name)
        symlink_cache = opened_file(source.cache_path, "34u")
        try:
            expect_refusal(
                lambda: coordinator._validate_process(
                    dataclasses.replace(
                        valid_process,
                        open_files=(bin_open, symlink_cache),
                    )
                ),
                "symbolic link",
            )
        finally:
            source.cache_path.unlink()
            cache_target.rename(source.cache_path)
            source.cache_path.chmod(0o644)

        profile_alias = root / "fake-profile-symlink"
        profile_alias.symlink_to(source.profile_root, target_is_directory=True)
        real_profile_root = source.profile_root
        source.profile_root = profile_alias
        try:
            expect_refusal(
                lambda: coordinator._validate_process(process_with_cache),
                "symbolic link",
            )
        finally:
            source.profile_root = real_profile_root
            profile_alias.unlink()

        outside_bin = root / "outside.bin"
        outside_cue = root / "outside.cue"
        same_basename = root / "same-basename-outside" / "vulkan_shaders.bin"
        unknown_cache_bin = source.cache_path.parent / "unknown.bin"
        same_basename.parent.mkdir()
        for path, contents in (
            (outside_bin, b"outside BIN"),
            (outside_cue, b"outside CUE"),
            (same_basename, b"same basename outside profile cache"),
            (unknown_cache_bin, b"unknown profile cache BIN"),
        ):
            path.write_bytes(contents)
        for path in (outside_bin, outside_cue, same_basename, unknown_cache_bin):
            extra_open = opened_file(path, "35r")
            expect_refusal(
                lambda extra_open=extra_open: coordinator._validate_process(
                    dataclasses.replace(
                        valid_process,
                        open_files=(bin_open, extra_open),
                    )
                ),
                "another CUE or BIN open",
            )

        for label, changed in (
            ("missing", ()),
            ("duplicate", (bin_open, bin_open)),
            ("fd", (dataclasses.replace(bin_open, fd="not-a-fd"),)),
            ("path", (dataclasses.replace(bin_open, real_path="/tmp/wrong.bin"),)),
            ("mode", (dataclasses.replace(bin_open, fd="21u"),)),
            ("inode", (dataclasses.replace(bin_open, inode=bin_open.inode + 1),)),
        ):
            expect_refusal(
                lambda changed=changed: coordinator._validate_process(
                    dataclasses.replace(valid_process, open_files=changed)
                )
            )

        expect_refusal(
            lambda: coordinator._validate_process(
                dataclasses.replace(
                    valid_process,
                    open_files=(bin_open, transient_cue_open),
                )
            ),
            "still has the parsed CUE open",
        )

        boundary.running = True
        boundary.exited = False
        boundary.process = valid_process
        observed, observed_cue, observed_bin = coordinator._revalidate_process(
            valid_process
        )
        require(
            observed == valid_process
            and observed_cue.real_path == os.path.realpath(source.cue_path)
            and observed_bin.real_path == os.path.realpath(source.bin_path),
            "Mid-session process revalidation rejected the stable BIN descriptor.",
        )
        boundary.process = process_with_cache
        cache_opened_mid_session, _, _ = coordinator._revalidate_process(
            valid_process
        )
        require(
            cache_opened_mid_session == process_with_cache,
            "Mid-session revalidation rejected an exact shader-cache open.",
        )
        boundary.process = valid_process
        cache_closed_mid_session, _, _ = coordinator._revalidate_process(
            process_with_cache
        )
        require(
            cache_closed_mid_session == valid_process,
            "Mid-session revalidation rejected an exact shader-cache close.",
        )

        replacement_cache = source.cache_path.with_name("replacement-cache.tmp")
        replacement_cache.write_bytes(original_cache_bytes + b" replaced")
        replacement_cache.chmod(0o644)
        os.replace(replacement_cache, source.cache_path)
        replaced_cache_open = opened_file(source.cache_path, "36u")
        process_with_replaced_cache = dataclasses.replace(
            valid_process, open_files=(bin_open, replaced_cache_open)
        )
        boundary.process = process_with_replaced_cache
        cache_replaced_mid_session, _, _ = coordinator._revalidate_process(
            process_with_cache
        )
        require(
            cache_replaced_mid_session == process_with_replaced_cache,
            "Mid-session revalidation persistence-bound the mutable shader cache.",
        )

        def reject_mid_session(
            changed_files: tuple[Any, ...], expected_fragment: str
        ) -> None:
            boundary.process = dataclasses.replace(
                valid_process, open_files=changed_files
            )
            try:
                expect_refusal(
                    lambda: coordinator._revalidate_process(valid_process),
                    expected_fragment,
                )
            finally:
                boundary.process = valid_process

        reject_mid_session((), "one exact pinned disc file descriptor")
        reject_mid_session(
            (bin_open, bin_open), "one exact pinned disc file descriptor"
        )
        reject_mid_session(
            (dataclasses.replace(bin_open, fd="22r"),),
            "BIN file descriptor identity changed",
        )
        reject_mid_session(
            (dataclasses.replace(bin_open, fd="21u"),),
            "read-only descriptor differs",
        )
        reject_mid_session(
            (dataclasses.replace(bin_open, real_path="/tmp/wrong.bin"),),
            "one exact pinned disc file descriptor",
        )
        for changed_identity in (
            dataclasses.replace(bin_open, device=bin_open.device + 1),
            dataclasses.replace(bin_open, inode=bin_open.inode + 1),
            dataclasses.replace(bin_open, byte_length=bin_open.byte_length + 1),
            dataclasses.replace(bin_open, link_count=2),
        ):
            reject_mid_session(
                (changed_identity,), "read-only descriptor differs"
            )
        boundary.running = False

        boundary.locked = True
        expect_refusal(coordinator.preflight, "unavailable")
        boundary.locked = False
        boundary.permission_ok = False
        expect_refusal(coordinator.preflight, "unavailable")
        boundary.permission_ok = True
        boundary.listener_before = True
        expect_refusal(coordinator.preflight, "listener")
        boundary.listener_before = False
        boundary.running = True
        expect_refusal(coordinator.preflight, "already running")
        boundary.running = False
        source.refuse = True
        expect_refusal(coordinator.preflight, "source drift")
        source.refuse = False

        staging_parent = root / capture.CAPTURE_STAGING_PREFIX
        staging_parent.parent.mkdir(parents=True)
        symlink_target = root / "elsewhere"
        symlink_target.mkdir()
        staging_parent.symlink_to(symlink_target, target_is_directory=True)
        expect_refusal(coordinator.preflight, "ancestry")
        staging_parent.unlink()

        hardlink_a = root / "hardlink-a"
        hardlink_b = root / "hardlink-b"
        hardlink_a.write_bytes(b"same inode")
        os.link(hardlink_a, hardlink_b)
        expect_refusal(lambda: capture.read_regular_file(hardlink_a), "single-link")

        exclusive_dir = root / "exclusive"
        exclusive_dir.mkdir()
        exclusive_dir.chmod(0o700)
        with capture.AnchoredStaging.create_new_file(exclusive_dir, "raw.bin") as stream:
            stream.write(b"first")
        expect_refusal(
            lambda: capture.AnchoredStaging.create_new_file(exclusive_dir, "raw.bin"),
            "already exists",
        )

        run_id = "id65v3-20260810t200000z-0123456789abcdef"
        expect_refusal(
            lambda: coordinator.capture_session(capture.SESSION_IDS[1]),
            "active run",
        )
        prelaunch_calls_before_run = source.prelaunch_calls
        snapshot_calls_before_run = source.snapshot_calls
        validate_calls_before_run = source.validate_calls
        for session_id in capture.SESSION_IDS:
            require(
                coordinator.capture_session(session_id) == run_id,
                "Coordinator did not preserve its internally generated run ID.",
            )
        launch_prompts = [
            prompt for prompt in boundary.prompts
            if "exactly this HOME, executable, and CUE" in prompt
        ]
        require(
            len(launch_prompts) == len(capture.SESSION_IDS)
            and all(os.path.realpath(source.cue_path) in prompt
                    and os.path.realpath(source.external_home_root) in prompt
                    and os.path.realpath(source.executable_path) in prompt
                    for prompt in launch_prompts),
            "Launch prompts did not display exact external HOME/executable/CUE.",
        )
        resume_prompts = [
            prompt for prompt in boundary.prompts
            if prompt.startswith("LOCAL RESUME —")
        ]
        require(
            len(resume_prompts) == len(capture.SESSION_IDS)
            and all(
                "TogglePause hotkey Space exactly once" in prompt
                and "never Escape/Start" in prompt
                and "do not open Pause, Settings, or any menu" in prompt
                and "do not enter any load code yet" in prompt
                for prompt in resume_prompts
            ),
            "Local resume prompts do not distinguish Space from Start/menu actions.",
        )
        for session_id, load_sequence in capture.LOAD_SEQUENCE_INSTRUCTIONS.items():
            readiness_announcements = [
                message for message in boundary.announcements
                if message.startswith("AUTOMATIC READINESS ARMED")
                and session_id in message
                and "DO NOT ENTER ANY LOAD CODE" in message
            ]
            announcements = [
                message for message in boundary.announcements
                if session_id in message and load_sequence in message
            ]
            require(
                len(readiness_announcements) == 1
                and all(load_sequence not in message for message in readiness_announcements)
                and all(
                    "no Return or chat reply is needed" in message
                    for message in readiness_announcements
                )
                and len(announcements) == 1
                and announcements[0].startswith(
                    f"READINESS ACCEPTED — ENTER CODE NOW for {session_id}: "
                )
                and "Enter that exact sequence once" in announcements[0],
                f"Selector readiness/load-code split is incomplete for {session_id}.",
            )
            readiness_action_index = boundary.actions.index(
                ("announce", readiness_announcements[0])
            )
            announcement_index = boundary.actions.index(
                ("announce", announcements[0])
            )
            next_prompt_index = next(
                index
                for index in range(announcement_index + 1, len(boundary.actions))
                if boundary.actions[index][0] == "prompt"
            )
            require(
                readiness_action_index < announcement_index
                < next_prompt_index,
                f"Load-code announcement was not strictly after accepted readiness "
                f"and before the next prompt for {session_id}.",
            )
        require(
            all(
                "immediately after Return" not in text
                for _kind, text in boundary.actions
            ),
            "Coordinator retained the premature post-Return load-code instruction.",
        )
        for marker_id, instruction in capture.MARKER_INSTRUCTIONS.items():
            matching = [prompt for prompt in boundary.prompts if instruction in prompt]
            require(
                len(matching) >= 2
                and any("READY — do not start yet" in prompt for prompt in matching)
                and any("MINIMUM MET" in prompt for prompt in matching),
                f"Timed marker prompts lost their closed action/minimum UX for {marker_id}.",
            )
        require(
            any("TASK 5/6" in prompt and "complete perimeter in both directions" in prompt
                for prompt in boundary.prompts)
            and any("TASK 6/6" in prompt and "near each of the three corners" in prompt
                    for prompt in boundary.prompts)
            and all("charge toward each of the three edges" not in prompt
                    for prompt in boundary.prompts)
            and any("20.5-second hands-off minimum" in prompt
                    for prompt in boundary.prompts)
            and any("5.5-second fall/respawn minimum" in prompt
                    for prompt in boundary.prompts),
            "Candidate prompts lost task numbering or explicit minimum durations.",
        )
        landing_reference_prompts = [
            prompt for prompt in boundary.prompts
            if "On the first completed landing with normal control" in prompt
            or "AUTOMATED LANDING TELEMETRY ACCEPTED" in prompt
        ]
        position_prompts = [
            prompt for prompt in boundary.prompts
            if "will then capture the pre-fall frame." in prompt
        ]
        fall_prompts = [
            prompt for prompt in boundary.prompts
            if prompt.startswith("Two-operator fall authorization")
        ]
        expected_edge_prompts = (
            ("E0=A-B", "rear/base edge", "-Y, opposite spawn yaw",
             "(384,272,512)"),
            ("E1=B-C", "right-hand/east slope", "+X", "(440,384,512)"),
            ("E2=C-A", "left-hand/west slope", "-X", "(328,384,512)"),
        )
        require(
            len(landing_reference_prompts) == len(expected_edge_prompts)
            and len(position_prompts) == len(expected_edge_prompts)
            and len(fall_prompts) == len(expected_edge_prompts)
            and len(set(position_prompts)) == len(expected_edge_prompts),
            "Coordinator did not emit three distinct closed edge assignments.",
        )
        require(
            "AUTOMATED LANDING TELEMETRY ACCEPTED" in landing_reference_prompts[0]
            and "one clean triangular pad" in landing_reference_prompts[0]
            and "HUD shows exactly 4 lives" in landing_reference_prompts[0]
            and any(
                action == ("fresh-input-drained", landing_reference_prompts[0])
                for action in boundary.actions
            )
            and all(
                "untouched controllable Spyro's BODY yaw 0x40" in prompt
                and "visible apex C" in prompt
                and (
                    "If it does not, stop; do not remap" in prompt
                    or "If any check fails, stop; do not remap" in prompt
                )
                and "Camera alignment is not evidence of world direction" in prompt
                for prompt in landing_reference_prompts
            ),
            "Completed-landing prompts lost the confirm-or-abort body-facing boundary.",
        )
        landing_reference_actions = [
            action_index
            for action_index, action in enumerate(boundary.actions)
            if action[0] == "prompt" and action[1] in landing_reference_prompts
        ]
        for index, expected_fields in enumerate(expected_edge_prompts):
            position_prompt = position_prompts[index]
            fall_prompt = fall_prompts[index]
            require(
                all(field in position_prompt for field in expected_fields)
                and all(field in fall_prompt for field in expected_fields)
                and "CONTROLLER OPERATOR" in fall_prompt
                and "OBSERVING TERMINAL OPERATOR" in fall_prompt
                and "within 30 seconds" in fall_prompt
                and "BODY yaw 0x40" in position_prompt
                and "visible apex C" in position_prompt
                and "not a camera-view direction" in position_prompt
                and "regardless of camera" in position_prompt
                and (index != 0 or "Turn around from body-facing C" in position_prompt),
                f"Edge prompt {index} lost its exact physical assignment.",
            )
            position_action = boundary.actions.index(("prompt", position_prompt))
            fall_action = boundary.actions.index(("prompt", fall_prompt))
            frames_between = [
                action for action in boundary.actions[position_action + 1:fall_action]
                if action[0] == "frame"
            ]
            require(
                landing_reference_actions[index] < position_action < fall_action
                and len(frames_between) == 1,
                f"Edge {expected_fields[0]} did not capture exactly one pre-fall frame "
                "after positioning and before departure confirmation.",
            )
        require(
            all(child.signals == [2] for child in boundary.children),
            "Coordinator did not signal only each owned child exactly once.",
        )
        run_path = root / capture.CAPTURE_STAGING_PREFIX / run_id
        session_path = run_path / f"00-{capture.SESSION_IDS[0]}"
        require((run_path / "run-origin.json").is_file(), "Run origin was not created.")
        require(
            (session_path / "capture-event-journal.json").is_file(),
            "Sealed capture journal was not created.",
        )
        coordinator.verify_staging(run_id)
        require(
            len([item for item in run_path.iterdir() if item.is_dir()])
            == len(capture.SESSION_IDS),
            "Coordinator did not seal the exact seven-session run.",
        )
        require(
            source.prelaunch_calls - prelaunch_calls_before_run == 1
            and source.snapshot_calls - snapshot_calls_before_run == 23
            and source.validate_calls - validate_calls_before_run == 31,
            "One-time prelaunch or subprocess-free snapshot/static call counts drifted: "
            f"prelaunch={source.prelaunch_calls - prelaunch_calls_before_run} "
            f"snapshots={source.snapshot_calls - snapshot_calls_before_run} "
            f"static={source.validate_calls - validate_calls_before_run}.",
        )
        repeated_prelaunch_root = root / "repeated-prelaunch-regression"
        repeated_prelaunch_root.mkdir(mode=0o700)
        repeated_prelaunch_source = FakeSource(repeated_prelaunch_root)
        capture._validate_prelaunch_proof(
            repeated_prelaunch_source.verify_prelaunch_once()
        )
        expect_refusal(
            repeated_prelaunch_source.verify_prelaunch_once,
            "repeated prelaunch proof",
        )

        origin = read_json(run_path / "run-origin.json")
        journal = read_json(session_path / "capture-event-journal.json")
        expected_pins = {key: origin[key] for key in capture.CAPTURE_PIN_KEYS}
        terminal = capture.verify_journal(
            journal,
            expected_run_id=run_id,
            expected_session_id=capture.SESSION_IDS[0],
            expected_ordinal=0,
            expected_previous_hash=capture.ZERO_SHA256,
            expected_run_origin_sha256=hashlib.sha256(
                capture.canonical_bytes(origin)
            ).hexdigest(),
            expected_pins=expected_pins,
        )
        require(
            capture.SHA256_RE.fullmatch(terminal.terminal_hash) is not None,
            "Terminal hash is invalid.",
        )
        partial_expected_pins = dict(expected_pins)
        partial_expected_pins.pop("profileVerifierSha256")
        expect_refusal(
            lambda: capture.verify_journal(
                journal,
                expected_run_id=run_id,
                expected_session_id=capture.SESSION_IDS[0],
                expected_ordinal=0,
                expected_previous_hash=capture.ZERO_SHA256,
                expected_run_origin_sha256=hashlib.sha256(
                    capture.canonical_bytes(origin)
                ).hexdigest(),
                expected_pins=partial_expected_pins,
            ),
            "exact frozen pin closure",
        )
        full_pin_closure_rejects = 1
        legacy_namespace_rejects = 0
        retired_v2_run_id = "id65v2-20260810t200000z-0123456789abcdef"
        require(
            capture.RUN_ID_RE.fullmatch(run_id) is not None
            and capture.RUN_ID_RE.fullmatch(retired_v2_run_id) is None,
            "The active run-ID grammar did not exclusively select the V3 namespace.",
        )
        expect_refusal(
            lambda: coordinator._load_run(retired_v2_run_id, expected_pins),
            "fixed lowercase namespace",
        )
        legacy_namespace_rejects += 1

        origin_path = run_path / "run-origin.json"
        original_origin_bytes = origin_path.read_bytes()
        retired_v2_origin = copy.deepcopy(origin)
        retired_v2_origin["format"] = (
            "spyro-editor-id65-v2-runtime-capture-run-origin"
        )
        try:
            origin_path.write_bytes(capture.canonical_bytes(retired_v2_origin))
            origin_path.chmod(0o600)
            expect_refusal(
                lambda: coordinator._load_run(run_id, expected_pins),
                "Run origin identity changed",
            )
        finally:
            origin_path.write_bytes(original_origin_bytes)
            origin_path.chmod(0o600)
        legacy_namespace_rejects += 1

        legacy_version_domain_rejects = 0
        legacy_origin_v1 = copy.deepcopy(origin)
        legacy_origin_v1["formatVersion"] = 1
        try:
            origin_path.write_bytes(capture.canonical_bytes(legacy_origin_v1))
            origin_path.chmod(0o600)
            expect_refusal(
                lambda: coordinator._load_run(run_id, expected_pins),
                "Run origin identity changed",
            )
        finally:
            origin_path.write_bytes(original_origin_bytes)
            origin_path.chmod(0o600)
        legacy_version_domain_rejects += 1

        profile_before = read_json(session_path / "runtime-profile-before.json")
        profile_before_artifact = next(
            event["payload"]
            for event in journal["events"]
            if event["eventType"] == "profile-before-captured"
        )
        retired_v2_profile = copy.deepcopy(profile_before)
        retired_v2_profile["format"] = (
            "spyro-editor-id65-v2-runtime-profile-snapshot"
        )
        expect_refusal(
            lambda: coordinator._validate_staged_profile(
                retired_v2_profile,
                profile_before_artifact,
                capture.SESSION_IDS[0],
                journal["processInstanceId"],
            ),
            "Runtime profile snapshot identity",
        )
        legacy_namespace_rejects += 1

        legacy_snapshot_v1 = copy.deepcopy(profile_before)
        legacy_snapshot_v1["formatVersion"] = 1
        expect_refusal(
            lambda: coordinator._validate_staged_profile(
                legacy_snapshot_v1,
                profile_before_artifact,
                capture.SESSION_IDS[0],
                journal["processInstanceId"],
            ),
            "Runtime profile snapshot identity",
        )
        legacy_version_domain_rejects += 1

        retired_v2_journals: list[dict[str, Any]] = []
        changed = copy.deepcopy(journal)
        changed["format"] = (
            "spyro-editor-id65-v2-runtime-capture-event-journal"
        )
        retired_v2_journals.append(changed)
        changed = copy.deepcopy(journal)
        changed["runId"] = retired_v2_run_id
        retired_v2_journals.append(changed)
        changed = copy.deepcopy(journal)
        changed["processInstanceId"] = (
            "id65v2-process-0123456789abcdef0123456789abcdef"
        )
        retired_v2_journals.append(changed)
        for changed in retired_v2_journals:
            expect_refusal(
                lambda changed=changed: capture.verify_journal(
                    changed,
                    expected_run_id=run_id,
                    expected_session_id=capture.SESSION_IDS[0],
                    expected_ordinal=0,
                    expected_previous_hash=capture.ZERO_SHA256,
                    expected_run_origin_sha256=hashlib.sha256(
                        capture.canonical_bytes(origin)
                    ).hexdigest(),
                    expected_pins=expected_pins,
                ),
                "Capture journal header identity changed",
            )
            legacy_namespace_rejects += 1

        for legacy_journal_version in (1, 2):
            changed = copy.deepcopy(journal)
            changed["formatVersion"] = legacy_journal_version
            expect_refusal(
                lambda changed=changed: capture.verify_journal(
                    changed,
                    expected_run_id=run_id,
                    expected_session_id=capture.SESSION_IDS[0],
                    expected_ordinal=0,
                    expected_previous_hash=capture.ZERO_SHA256,
                    expected_run_origin_sha256=hashlib.sha256(
                        capture.canonical_bytes(origin)
                    ).hexdigest(),
                    expected_pins=expected_pins,
                ),
                "Capture journal header identity changed",
            )
            legacy_version_domain_rejects += 1

        legacy_source_lines = ["id65-v3-runtime-source-closure-v1"]
        legacy_source_lines.extend(
            f"{name}={value}"
            for name, value in capture.SOURCE_CLOSURE_PREFIX_FIELDS
        )
        legacy_source_lines.append(
            f"captureCoordinatorSha256={origin['captureCoordinatorSha256']}"
        )
        legacy_source_lines.extend(
            f"{name}={value}"
            for name, value in capture.SOURCE_CLOSURE_SUFFIX_FIELDS
        )
        legacy_source_closure = hashlib.sha256(
            ("\n".join(legacy_source_lines) + "\n").encode("utf-8", "strict")
        ).hexdigest()
        require(
            legacy_source_closure != origin["sourceClosureSha256"],
            "The retired source-closure-v1 domain collided with v2.",
        )
        legacy_source_origin = copy.deepcopy(origin)
        legacy_source_origin["sourceClosureSha256"] = legacy_source_closure
        try:
            origin_path.write_bytes(capture.canonical_bytes(legacy_source_origin))
            origin_path.chmod(0o600)
            expect_refusal(
                lambda: coordinator._load_run(run_id, expected_pins),
                "Run origin differs from current frozen source pins",
            )
        finally:
            origin_path.write_bytes(original_origin_bytes)
            origin_path.chmod(0o600)
        legacy_version_domain_rejects += 1

        legacy_event_journal = copy.deepcopy(journal)
        legacy_event_header = {
            key: value
            for key, value in legacy_event_journal.items()
            if key != "events"
        }
        legacy_event = legacy_event_journal["events"][0]
        current_event_manifest = capture._event_manifest(
            legacy_event_header, legacy_event
        )
        require(
            current_event_manifest.startswith(
                b"id65-v3-runtime-capture-event-v2\n"
            ),
            "Current capture-event domain is not v2.",
        )
        legacy_event["eventSha256"] = hashlib.sha256(
            current_event_manifest.replace(
                b"id65-v3-runtime-capture-event-v2\n",
                b"id65-v3-runtime-capture-event-v1\n",
                1,
            )
        ).hexdigest()
        expect_refusal(
            lambda: capture.verify_journal(
                legacy_event_journal,
                expected_run_id=run_id,
                expected_session_id=capture.SESSION_IDS[0],
                expected_ordinal=0,
                expected_previous_hash=capture.ZERO_SHA256,
                expected_run_origin_sha256=hashlib.sha256(
                    capture.canonical_bytes(origin)
                ).hexdigest(),
                expected_pins=expected_pins,
            ),
            "Capture journal event hash changed",
        )
        legacy_version_domain_rejects += 1
        require(
            legacy_namespace_rejects == 6,
            "The retired V2 run/process/inner-format rejection matrix drifted.",
        )
        require(
            legacy_version_domain_rejects == 6,
            "The retired V1/V2 schema and domain rejection matrix drifted.",
        )
        primary_events = journal["events"]
        primary_selector = next(
            event for event in primary_events
            if event["eventType"] == "selector-started"
        )
        primary_completed_start = next(
            event for event in primary_events
            if event["eventType"] == "check-started"
            and event["payload"]["markerId"]
            == "candidate.completed-landing-hud4"
        )
        primary_landing_binding = next(
            event for event in primary_events
            if event["eventType"] == "watcher-marker-bound"
            and event["payload"]["markerRole"] == "landing-marked"
        )
        primary_samples = coordinator._parse_watcher_samples(
            session_path / "watcher-stdout.log"
        )
        require(
            capture._candidate_primary_landing_evidence_is_accepted(
                primary_samples,
                primary_selector["atUtc"],
                primary_selector["monotonicMicroseconds"],
                primary_completed_start["atUtc"],
                primary_completed_start["monotonicMicroseconds"],
                primary_landing_binding["payload"]["sampleOrdinal"],
            ),
            "Sealed primary journal does not independently prove its assisted landing.",
        )

        def rechain_capture_journal(changed_journal: dict[str, Any]) -> None:
            previous_hash = capture.ZERO_SHA256
            for sequence, event in enumerate(changed_journal["events"]):
                event["sequence"] = sequence
                event["previousEventSha256"] = previous_hash
                event["eventSha256"] = capture.sha256_bytes(
                    capture._event_manifest(changed_journal, event)
                )
                previous_hash = event["eventSha256"]

        pin_tamper_root = root / "full-pin-closure-regression"
        pin_tamper_root.mkdir(mode=0o700)
        shutil.copytree(
            root / capture.CAPTURE_STAGING_PREFIX,
            pin_tamper_root / capture.CAPTURE_STAGING_PREFIX,
            copy_function=shutil.copy2,
        )
        pin_tamper_source = FakeSource(pin_tamper_root)
        pin_origin_path = (
            pin_tamper_root
            / capture.CAPTURE_STAGING_PREFIX
            / run_id
            / "run-origin.json"
        )
        pin_origin = read_json(pin_origin_path)
        pin_origin["repositoryRootRealPath"] = os.path.realpath(pin_tamper_root)
        pin_origin_path.write_bytes(capture.canonical_bytes(pin_origin))
        pin_origin_path.chmod(0o600)
        pin_journal_path = (
            pin_tamper_root
            / capture.CAPTURE_STAGING_PREFIX
            / run_id
            / f"00-{capture.SESSION_IDS[0]}"
            / "capture-event-journal.json"
        )
        pin_journal = read_json(pin_journal_path)
        pin_journal["runOriginSha256"] = hashlib.sha256(
            capture.canonical_bytes(pin_origin)
        ).hexdigest()
        pin_journal["profileVerifierSha256"] = "0" * 64
        rechain_capture_journal(pin_journal)
        pin_journal_path.write_bytes(capture.canonical_bytes(pin_journal))
        pin_journal_path.chmod(0o600)
        pin_coordinator = capture.CaptureCoordinator(
            FakeBoundary(pin_tamper_root, pin_tamper_source),
            source_guard=pin_tamper_source,
            lease_factory=contextlib.nullcontext,
        )
        expect_refusal(
            lambda: pin_coordinator._load_run(
                run_id, pin_tamper_source.pins
            ),
            "Capture journal pin changed: profileVerifierSha256",
        )
        full_pin_closure_rejects += 1
        require(
            full_pin_closure_rejects == 2,
            "The exact journal pin-closure refusal matrix drifted.",
        )

        assisted_full_run_rejects = 0
        for assisted_case in ("selector", "check-start", "raw-transition"):
            tamper_root = root / f"assisted-{assisted_case}-full-run"
            tamper_root.mkdir(mode=0o700)
            shutil.copytree(
                root / capture.CAPTURE_STAGING_PREFIX,
                tamper_root / capture.CAPTURE_STAGING_PREFIX,
                copy_function=shutil.copy2,
            )
            tamper_source = FakeSource(tamper_root)
            shutil.copy2(source.executable_path, tamper_source.executable_path)
            shutil.copy2(source.cue_path, tamper_source.cue_path)
            shutil.copy2(source.bin_path, tamper_source.bin_path)
            shutil.copy2(source.cache_path, tamper_source.cache_path)
            tamper_origin_path = (
                tamper_root
                / capture.CAPTURE_STAGING_PREFIX
                / run_id
                / "run-origin.json"
            )
            tamper_origin = read_json(tamper_origin_path)
            tamper_origin["repositoryRootRealPath"] = os.path.realpath(tamper_root)
            tamper_origin_path.write_bytes(capture.canonical_bytes(tamper_origin))
            tamper_origin_path.chmod(0o600)
            tamper_coordinator = capture.CaptureCoordinator(
                FakeBoundary(tamper_root, tamper_source),
                source_guard=tamper_source,
                lease_factory=contextlib.nullcontext,
            )
            tamper_session = (
                tamper_root
                / capture.CAPTURE_STAGING_PREFIX
                / run_id
                / f"00-{capture.SESSION_IDS[0]}"
            )
            tamper_journal_path = tamper_session / "capture-event-journal.json"
            tamper_live_path = tamper_session / "capture-events-live.ndjson"
            tamper_watcher_path = tamper_session / "watcher-stdout.log"
            tamper_journal = read_json(tamper_journal_path)
            tamper_journal["runOriginSha256"] = hashlib.sha256(
                capture.canonical_bytes(tamper_origin)
            ).hexdigest()
            tamper_process = next(
                item for item in tamper_journal["events"]
                if item["eventType"] == "process-start-observed"
            )["payload"]
            tamper_executable = tamper_source.executable_identity()
            tamper_cue, tamper_bin = tamper_source.candidate_identities()
            tamper_process.update({
                "executableByteLength": tamper_executable.byte_length,
                "executableDevice": tamper_executable.device,
                "executableInode": tamper_executable.inode,
            })
            tamper_source.executable_path = pathlib.Path(
                tamper_process["executableRealPath"]
            )
            tamper_source.external_home_root = pathlib.Path(
                tamper_process["externalHomeRealPath"]
            )
            tamper_source.cue_path = pathlib.Path(tamper_process["argv"][1])
            tamper_disc = next(
                item for item in tamper_journal["events"]
                if item["eventType"] == "disc-open-observed"
            )["payload"]
            for prefix, identity in (("cue", tamper_cue), ("bin", tamper_bin)):
                tamper_disc[f"{prefix}Device"] = identity.device
                tamper_disc[f"{prefix}Inode"] = identity.inode
                tamper_disc[f"{prefix}ByteLength"] = identity.byte_length
            tamper_source.bin_path = pathlib.Path(tamper_disc["binRealPath"])
            if assisted_case in {"selector", "check-start"}:
                wanted_type = (
                    "selector-started"
                    if assisted_case == "selector"
                    else "check-started"
                )
                wanted_marker = (
                    "selector"
                    if assisted_case == "selector"
                    else "candidate.completed-landing-hud4"
                )
                event = next(
                    item
                    for item in tamper_journal["events"]
                    if item["eventType"] == wanted_type
                    and item["payload"].get("markerId") == wanted_marker
                )
                delta_microseconds = -2_100_000 if assisted_case == "selector" else 100_000
                event["atUtc"] = capture.format_utc(
                    capture.parse_utc(event["atUtc"])
                    + dt.timedelta(microseconds=delta_microseconds)
                )
                event["monotonicMicroseconds"] += delta_microseconds
            else:
                changed_samples = tamper_coordinator._parse_watcher_samples(
                    tamper_watcher_path
                )
                departure = next(
                    item
                    for item in changed_samples
                    if item["state"] == 3 and item["lives"] == 4
                )
                departure["state"] = 0
                changed_watcher = capture._offline_watcher_log(changed_samples)
                tamper_watcher_path.write_bytes(changed_watcher)
                tamper_watcher_path.chmod(0o600)
                watcher_payload = next(
                    item["payload"]
                    for item in tamper_journal["events"]
                    if item["eventType"] == "watcher-stopped"
                )
                watcher_payload["byteLength"] = len(changed_watcher)
                watcher_payload["sha256"] = hashlib.sha256(
                    changed_watcher
                ).hexdigest()
            rechain_capture_journal(tamper_journal)
            tamper_journal_bytes = capture.canonical_bytes(tamper_journal)
            tamper_live_bytes = b"".join(
                capture.canonical_bytes(event) + b"\n"
                for event in tamper_journal["events"]
            )
            tamper_journal_path.write_bytes(tamper_journal_bytes)
            tamper_live_path.write_bytes(tamper_live_bytes)
            tamper_journal_path.chmod(0o600)
            tamper_live_path.chmod(0o600)
            tamper_watcher_bytes = tamper_watcher_path.read_bytes()
            expect_refusal(
                lambda coordinator=tamper_coordinator: coordinator._load_run(
                    run_id,
                    tamper_source.pins,
                ),
                "Primary landing evidence",
            )
            require(
                tamper_journal_path.read_bytes() == tamper_journal_bytes
                and tamper_live_path.read_bytes() == tamper_live_bytes
                and tamper_watcher_path.read_bytes() == tamper_watcher_bytes,
                f"Assisted {assisted_case} refusal mutated its preserved run.",
            )
            assisted_full_run_rejects += 1

        tamper_count = 0
        mutations: list[Any] = []
        changed = copy.deepcopy(journal)
        changed["runId"] = run_id[:-1] + "0"
        mutations.append(changed)
        changed = copy.deepcopy(journal)
        changed["sessionId"] = capture.SESSION_IDS[1]
        mutations.append(changed)
        changed = copy.deepcopy(journal)
        changed["sessionOrdinal"] = 1
        mutations.append(changed)
        changed = copy.deepcopy(journal)
        changed["sourceClosureSha256"] = "e" * 64
        mutations.append(changed)
        changed = copy.deepcopy(journal)
        changed["captureCoordinatorSha256"] = "f" * 64
        mutations.append(changed)
        changed = copy.deepcopy(journal)
        changed["watcherSha256"] = "1" * 64
        mutations.append(changed)
        changed = copy.deepcopy(journal)
        changed["events"][0]["previousEventSha256"] = "2" * 64
        mutations.append(changed)
        changed = copy.deepcopy(journal)
        changed["events"][1]["sequence"] = 99
        mutations.append(changed)
        changed = copy.deepcopy(journal)
        changed["events"][1]["eventSha256"] = "3" * 64
        mutations.append(changed)
        changed = copy.deepcopy(journal)
        changed["events"][1]["monotonicMicroseconds"] = changed["events"][0]["monotonicMicroseconds"]
        mutations.append(changed)
        changed = copy.deepcopy(journal)
        changed["events"][-1]["eventType"] = "capture-aborted"
        mutations.append(changed)
        changed = copy.deepcopy(journal)
        changed["events"].append(copy.deepcopy(changed["events"][-1]))
        mutations.append(changed)
        changed = copy.deepcopy(journal)
        del changed["events"][3]
        mutations.append(changed)
        changed = copy.deepcopy(journal)
        changed["events"][0]["unknown"] = True
        mutations.append(changed)
        for changed in mutations:
            expect_refusal(
                lambda changed=changed: capture.verify_journal(
                    changed,
                    expected_run_id=run_id,
                    expected_session_id=capture.SESSION_IDS[0],
                    expected_ordinal=0,
                    expected_previous_hash=capture.ZERO_SHA256,
                    expected_run_origin_sha256=hashlib.sha256(
                        capture.canonical_bytes(origin)
                    ).hexdigest(),
                    expected_pins=expected_pins,
                )
            )
            tamper_count += 1

        noncanonical = session_path / "noncanonical.json"
        noncanonical.write_text('{"b":1, "a":2}', encoding="utf-8")
        noncanonical.chmod(0o600)
        expect_refusal(
            lambda: coordinator._read_canonical_json(noncanonical), "not exact canonical"
        )
        duplicate = session_path / "duplicate.json"
        duplicate.write_text('{"a":1,"a":2}', encoding="utf-8")
        duplicate.chmod(0o600)
        expect_refusal(lambda: coordinator._read_canonical_json(duplicate), "Duplicate")
        bom = session_path / "bom.json"
        bom.write_bytes(b"\xef\xbb\xbf{}")
        bom.chmod(0o600)
        expect_refusal(lambda: coordinator._read_canonical_json(bom), "BOM")

        seal_fault_cases = (
            ("create", CreateSealFailureJournal, "provisional seal"),
            ("partial-write", PartialWriteSealFailureJournal, "provisional seal"),
            ("fsync", FsyncSealFailureJournal, "provisional seal"),
            ("directory-fsync", DirectoryFsyncSealFailureJournal, "provisional seal"),
            ("post-return", ReturnSealFailureJournal, "provisional seal"),
            ("live-prewrite", BeforeLiveAppendFailureJournal, "live terminal"),
            ("live-partial-write", PartialLiveAppendFailureJournal, "live terminal"),
            ("live-fsync", LiveAppendFsyncFailureJournal, "live terminal"),
        )
        original_journal_writer = capture.JournalWriter
        seal_fault_rejects = 0

        abort_guard_root = root / "abort-seal-coexistence-guard"
        abort_guard_root.mkdir(mode=0o700)
        abort_guard = capture.JournalWriter(
            abort_guard_root,
            {},
            capture.ZERO_SHA256,
            lambda: capture.ClockSample(
                "2026-08-10T23:19:00.000Z", 1_000_000
            ),
        )
        abort_guard_seal = abort_guard_root / "capture-event-journal.json"
        abort_guard_seal.write_bytes(b"sealed")
        os.chmod(abort_guard_seal, 0o600)
        expect_refusal(
            lambda: abort_guard.abort("synthetic"),
            "cannot coexist",
        )
        abort_guard_live = (
            abort_guard_root / "capture-events-live.ndjson"
        ).read_bytes()
        require(
            b'"eventType":"capture-aborted"' not in abort_guard_live,
            "Abort defense gate wrote an aborted event beside a sealed leaf.",
        )
        seal_fault_rejects += 1

        close_unit_root = root / "exception-total-close-unit-regression"
        close_unit_root.mkdir(mode=0o700)
        close_unit = CloseFaultJournal(
            close_unit_root,
            {},
            capture.ZERO_SHA256,
            lambda: capture.ClockSample(
                "2026-08-10T23:19:30.000Z", 1_500_000
            ),
        )
        close_unit_dummy = close_unit_root / "provisional-close-dummy"
        close_unit_dummy.write_bytes(b"x")
        close_unit_dummy.chmod(0o600)
        close_unit._provisional_file_fd = os.open(
            close_unit_dummy, os.O_RDWR | getattr(os, "O_NOFOLLOW", 0)
        )
        close_unit._provisional_directory_fd = os.open(
            close_unit_root,
            os.O_RDONLY | getattr(os, "O_DIRECTORY", 0),
        )
        try:
            close_unit.close_after_failed_cleanup()
        except OSError as exc:
            require(
                str(exc) == "injected live close failure",
                "Exception-total close did not retain its first failure.",
            )
        else:
            raise AssertionError("Injected close failures were swallowed.")
        require(
            close_unit.close_calls == ["live", "file", "directory"]
            and close_unit._live.closed
            and close_unit._provisional_file_fd is None
            and close_unit._provisional_directory_fd is None
            and close_unit._provisional_identity is None,
            "Exception-total close skipped a later handle or retained authority.",
        )
        for fault_index, (fault_label, journal_type, fragment) in enumerate(
            seal_fault_cases
        ):
            fault_root = root / f"seal-{fault_label}-regression"
            fault_root.mkdir(mode=0o700)
            fault_source = FakeSource(fault_root)
            fault_boundary = FakeBoundary(fault_root, fault_source)
            fault_run_id = (
                f"id65v3-20260810t{2320 + fault_index:04d}00z-"
                f"{fault_index + 1:016x}"
            )
            fault_coordinator = capture.CaptureCoordinator(
                fault_boundary,
                source_guard=fault_source,
                lease_factory=contextlib.nullcontext,
                run_id_factory=lambda sample, value=fault_run_id: value,
            )
            capture.JournalWriter = journal_type
            try:
                expect_refusal(
                    lambda coordinator=fault_coordinator: coordinator.capture_session(
                        capture.SESSION_IDS[0]
                    ),
                    fragment,
                )
            finally:
                capture.JournalWriter = original_journal_writer
            fault_session = (
                fault_root
                / capture.CAPTURE_STAGING_PREFIX
                / fault_run_id
                / f"00-{capture.SESSION_IDS[0]}"
            )
            fault_live = (
                fault_session / "capture-events-live.ndjson"
            ).read_bytes()
            fault_seal = fault_session / "capture-event-journal.json"
            require(
                not fault_seal.exists()
                and b'"eventType":"capture-aborted"' in fault_live
                and fault_live.count(b'"eventType":"capture-aborted"') == 1
                and b'"eventType":"session-sealed"' not in fault_live
                and all(
                    child.signals == [signal.SIGINT] and child.poll() == 0
                    for child in fault_boundary.children
                )
                and not (
                    fault_seal.exists()
                    and b'"eventType":"capture-aborted"' in fault_live
                ),
                f"Provisional seal {fault_label} fault was not rolled back exactly.",
            )
            seal_fault_rejects += 1

        resolved_commit_count = 0
        for resolved_index, (resolved_label, journal_type) in enumerate(
            (
                ("post-readback-return", LiveAppendReturnFailureJournal),
                ("post-readback-keyboard", LiveAppendKeyboardInterruptJournal),
                ("combined-durable", CombinedDurableCommitFaultJournal),
            )
        ):
            resolved_root = root / f"resolved-{resolved_label}-regression"
            resolved_root.mkdir(mode=0o700)
            resolved_source = FakeSource(resolved_root)
            resolved_boundary = FakeBoundary(resolved_root, resolved_source)
            resolved_run_id = (
                "id65v3-20260811t00"
                f"{resolved_index:02d}00z-{resolved_index + 20:016x}"
            )
            resolved_coordinator = capture.CaptureCoordinator(
                resolved_boundary,
                source_guard=resolved_source,
                lease_factory=contextlib.nullcontext,
                run_id_factory=lambda sample, value=resolved_run_id: value,
            )
            if journal_type is CombinedDurableCommitFaultJournal:
                CombinedDurableCommitFaultJournal.rollback_tail_calls = 0
                CombinedDurableCommitFaultJournal.unlink_calls = 0
            capture.JournalWriter = journal_type
            try:
                resolved_result = resolved_coordinator.capture_session(
                    capture.SESSION_IDS[0]
                )
            finally:
                capture.JournalWriter = original_journal_writer
            resolved_session = (
                resolved_root
                / capture.CAPTURE_STAGING_PREFIX
                / resolved_run_id
                / f"00-{capture.SESSION_IDS[0]}"
            )
            resolved_live = (
                resolved_session / "capture-events-live.ndjson"
            ).read_bytes()
            resolved_seal = resolved_session / "capture-event-journal.json"
            loaded_resolved = resolved_coordinator._load_run(
                resolved_run_id, resolved_source.pins
            )
            require(
                resolved_result == resolved_run_id
                and resolved_seal.exists()
                and resolved_live.count(b'"eventType":"session-sealed"') == 1
                and b'"eventType":"capture-aborted"' not in resolved_live
                and loaded_resolved.session_count == 1
                and (
                    journal_type is not CombinedDurableCommitFaultJournal
                    or (
                        CombinedDurableCommitFaultJournal.rollback_tail_calls == 0
                        and CombinedDurableCommitFaultJournal.unlink_calls == 0
                    )
                ),
                f"Durable {resolved_label} commit was reported as a failed capture.",
            )
            resolved_commit_count += 1

        postcommit_close_root = root / "postcommit-close-fault-regression"
        postcommit_close_root.mkdir(mode=0o700)
        postcommit_close_source = FakeSource(postcommit_close_root)
        postcommit_close_boundary = FakeBoundary(
            postcommit_close_root, postcommit_close_source
        )
        postcommit_close_run_id = (
            "id65v3-20260811t004500z-6666666666666666"
        )
        postcommit_close_coordinator = capture.CaptureCoordinator(
            postcommit_close_boundary,
            source_guard=postcommit_close_source,
            lease_factory=contextlib.nullcontext,
            run_id_factory=lambda sample: postcommit_close_run_id,
        )
        CloseFaultJournal.last_instance = None
        capture.JournalWriter = CloseFaultJournal
        try:
            postcommit_close_result = (
                postcommit_close_coordinator.capture_session(
                    capture.SESSION_IDS[0]
                )
            )
        finally:
            capture.JournalWriter = original_journal_writer
        postcommit_close_instance = CloseFaultJournal.last_instance
        require(
            postcommit_close_result == postcommit_close_run_id
            and postcommit_close_instance is not None
            and postcommit_close_instance.close_calls
            == ["live", "file", "directory"]
            and postcommit_close_instance._provisional_file_fd is None
            and postcommit_close_instance._provisional_directory_fd is None
            and postcommit_close_coordinator._load_run(
                postcommit_close_run_id, postcommit_close_source.pins
            ).session_count
            == 1,
            "Postcommit close faults overrode success or retained handle authority.",
        )

        mismatch_root = root / "combined-mismatched-cleanup-regression"
        mismatch_root.mkdir(mode=0o700)
        mismatch_source = FakeSource(mismatch_root)
        mismatch_boundary = FakeBoundary(mismatch_root, mismatch_source)
        mismatch_run_id = "id65v3-20260811t001000z-7777777777777777"
        mismatch_coordinator = capture.CaptureCoordinator(
            mismatch_boundary,
            source_guard=mismatch_source,
            lease_factory=contextlib.nullcontext,
            run_id_factory=lambda sample: mismatch_run_id,
        )
        capture.JournalWriter = CombinedMismatchCleanupFailureJournal
        try:
            expect_refusal(
                lambda: mismatch_coordinator.capture_session(
                    capture.SESSION_IDS[0]
                ),
                "exact cleanup did not complete",
            )
        finally:
            capture.JournalWriter = original_journal_writer
        mismatch_session = (
            mismatch_root
            / capture.CAPTURE_STAGING_PREFIX
            / mismatch_run_id
            / f"00-{capture.SESSION_IDS[0]}"
        )
        mismatch_live = (
            mismatch_session / "capture-events-live.ndjson"
        ).read_bytes()
        mismatch_seal = mismatch_session / "capture-event-journal.json"
        mismatch_identity_before = (
            mismatch_seal.read_bytes(),
            mismatch_live,
            os.stat(mismatch_seal, follow_symlinks=False).st_ino,
            os.stat(
                mismatch_session / "capture-events-live.ndjson",
                follow_symlinks=False,
            ).st_ino,
        )
        expect_refusal(
            lambda: mismatch_coordinator._load_run(
                mismatch_run_id, mismatch_source.pins
            ),
        )
        expect_refusal(
            lambda: mismatch_coordinator._select_run(
                capture.SESSION_IDS[0], mismatch_source.pins
            ),
        )
        expect_refusal(
            lambda: mismatch_coordinator.verify_staging(mismatch_run_id),
        )
        mismatch_identity_after = (
            mismatch_seal.read_bytes(),
            (mismatch_session / "capture-events-live.ndjson").read_bytes(),
            os.stat(mismatch_seal, follow_symlinks=False).st_ino,
            os.stat(
                mismatch_session / "capture-events-live.ndjson",
                follow_symlinks=False,
            ).st_ino,
        )
        mismatch_sealed_json = json.loads(
            mismatch_seal.read_text(encoding="utf-8")
        )
        mismatch_expected_live = b"".join(
            capture.canonical_bytes(event) + b"\n"
            for event in mismatch_sealed_json["events"]
        )
        require(
            mismatch_seal.exists()
            and b'"eventType":"capture-aborted"' not in mismatch_live
            and mismatch_live != mismatch_expected_live
            and mismatch_identity_after == mismatch_identity_before,
            "Mismatched combined cleanup fault became consumable or mutated on refusal.",
        )
        seal_fault_rejects += 1

        post_readback_mutation_rejects = 0
        for mutation_index, (mutation_label, journal_type) in enumerate(
            (
                ("live", PostReadbackLiveMutationJournal),
                ("seal", PostReadbackSealMutationJournal),
                ("seal-path", PostReadbackSealPathMutationJournal),
            )
        ):
            mutation_root = root / f"post-readback-{mutation_label}-regression"
            mutation_root.mkdir(mode=0o700)
            mutation_source = FakeSource(mutation_root)
            mutation_boundary = FakeBoundary(mutation_root, mutation_source)
            mutation_run_id = (
                "id65v3-20260811t00"
                f"{40 + mutation_index:02d}00z-{mutation_index + 50:016x}"
            )
            mutation_coordinator = capture.CaptureCoordinator(
                mutation_boundary,
                source_guard=mutation_source,
                lease_factory=contextlib.nullcontext,
                run_id_factory=lambda sample, value=mutation_run_id: value,
            )
            capture.JournalWriter = journal_type
            try:
                expect_refusal(
                    lambda coordinator=mutation_coordinator: coordinator.capture_session(
                        capture.SESSION_IDS[0]
                    ),
                    "exact cleanup did not complete",
                )
            finally:
                capture.JournalWriter = original_journal_writer
            mutation_session = (
                mutation_root
                / capture.CAPTURE_STAGING_PREFIX
                / mutation_run_id
                / f"00-{capture.SESSION_IDS[0]}"
            )
            mutation_live_path = mutation_session / "capture-events-live.ndjson"
            mutation_seal_path = mutation_session / "capture-event-journal.json"
            mutation_before = (
                mutation_live_path.read_bytes(),
                mutation_seal_path.read_bytes(),
                os.stat(mutation_live_path, follow_symlinks=False).st_ino,
                os.stat(mutation_seal_path, follow_symlinks=False).st_ino,
                tuple(sorted(item.name for item in mutation_session.iterdir())),
            )
            expect_refusal(
                lambda coordinator=mutation_coordinator: coordinator._load_run(
                    mutation_run_id, mutation_source.pins
                )
            )
            expect_refusal(
                lambda coordinator=mutation_coordinator: coordinator._select_run(
                    capture.SESSION_IDS[0], mutation_source.pins
                )
            )
            expect_refusal(
                lambda coordinator=mutation_coordinator: coordinator.verify_staging(
                    mutation_run_id
                )
            )
            mutation_after = (
                mutation_live_path.read_bytes(),
                mutation_seal_path.read_bytes(),
                os.stat(mutation_live_path, follow_symlinks=False).st_ino,
                os.stat(mutation_seal_path, follow_symlinks=False).st_ino,
                tuple(sorted(item.name for item in mutation_session.iterdir())),
            )
            require(
                mutation_before == mutation_after
                and b'"eventType":"capture-aborted"'
                not in mutation_before[0],
                f"Post-readback {mutation_label} mutation became consumable or changed on refusal.",
            )
            post_readback_mutation_rejects += 1

        resolver_durability_rows = 0
        for durability_index, (durability_label, journal_type) in enumerate(
            (
                ("restored-live", PostReadbackRestoreLiveMutationJournal),
                ("restored-seal", PostReadbackRestoreSealMutationJournal),
                (
                    "restored-seal-path",
                    PostReadbackRestoreSealPathMutationJournal,
                ),
            )
        ):
            durability_root = root / f"resolver-{durability_label}-regression"
            durability_root.mkdir(mode=0o700)
            durability_source = FakeSource(durability_root)
            durability_boundary = FakeBoundary(
                durability_root, durability_source
            )
            durability_run_id = (
                "id65v3-20260811t00"
                f"{50 + durability_index:02d}00z-{durability_index + 60:016x}"
            )
            durability_coordinator = capture.CaptureCoordinator(
                durability_boundary,
                source_guard=durability_source,
                lease_factory=contextlib.nullcontext,
                run_id_factory=lambda sample, value=durability_run_id: value,
            )
            capture.JournalWriter = journal_type
            try:
                durability_result = durability_coordinator.capture_session(
                    capture.SESSION_IDS[0]
                )
            finally:
                capture.JournalWriter = original_journal_writer
            durability_session = (
                durability_root
                / capture.CAPTURE_STAGING_PREFIX
                / durability_run_id
                / f"00-{capture.SESSION_IDS[0]}"
            )
            durability_live = (
                durability_session / "capture-events-live.ndjson"
            ).read_bytes()
            require(
                durability_result == durability_run_id
                and durability_live.count(
                    b'"eventType":"session-sealed"'
                )
                == 1
                and b'"eventType":"capture-aborted"'
                not in durability_live
                and durability_coordinator._load_run(
                    durability_run_id, durability_source.pins
                ).session_count
                == 1,
                f"Restored {durability_label} state was not freshly durable before success.",
            )
            resolver_durability_rows += 1

        resolver_fsync_root = root / "resolver-fsync-failure-regression"
        resolver_fsync_root.mkdir(mode=0o700)
        resolver_fsync_source = FakeSource(resolver_fsync_root)
        resolver_fsync_boundary = FakeBoundary(
            resolver_fsync_root, resolver_fsync_source
        )
        resolver_fsync_run_id = (
            "id65v3-20260811t005500z-8888888888888888"
        )
        resolver_fsync_coordinator = capture.CaptureCoordinator(
            resolver_fsync_boundary,
            source_guard=resolver_fsync_source,
            lease_factory=contextlib.nullcontext,
            run_id_factory=lambda sample: resolver_fsync_run_id,
        )
        capture.JournalWriter = ResolverFsyncFailureJournal
        try:
            expect_refusal(
                lambda: resolver_fsync_coordinator.capture_session(
                    capture.SESSION_IDS[0]
                ),
            )
        finally:
            capture.JournalWriter = original_journal_writer
        resolver_fsync_session = (
            resolver_fsync_root
            / capture.CAPTURE_STAGING_PREFIX
            / resolver_fsync_run_id
            / f"00-{capture.SESSION_IDS[0]}"
        )
        resolver_fsync_live = (
            resolver_fsync_session / "capture-events-live.ndjson"
        ).read_bytes()
        require(
            not (
                (resolver_fsync_session / "capture-event-journal.json").exists()
                and b'"eventType":"session-sealed"'
                in resolver_fsync_live
            )
            and b'"eventType":"capture-aborted"' in resolver_fsync_live,
            "Resolver fsync failure was reported as a durable committed pair.",
        )
        resolver_durability_rows += 1

        rollback_root = root / "seal-rollback-failure-regression"
        rollback_root.mkdir(mode=0o700)
        rollback_source = FakeSource(rollback_root)
        rollback_boundary = FakeBoundary(rollback_root, rollback_source)
        rollback_run_id = "id65v3-20260810t233000z-9999999999999999"
        rollback_coordinator = capture.CaptureCoordinator(
            rollback_boundary,
            source_guard=rollback_source,
            lease_factory=contextlib.nullcontext,
            run_id_factory=lambda sample: rollback_run_id,
        )
        PrecommitCloseFaultJournal.last_instance = None
        capture.JournalWriter = PrecommitCloseFaultJournal
        try:
            expect_refusal(
                lambda: rollback_coordinator.capture_session(capture.SESSION_IDS[0]),
                "exact cleanup did not complete",
            )
        finally:
            capture.JournalWriter = original_journal_writer
        rollback_session = (
            rollback_root
            / capture.CAPTURE_STAGING_PREFIX
            / rollback_run_id
            / f"00-{capture.SESSION_IDS[0]}"
        )
        rollback_live = (
            rollback_session / "capture-events-live.ndjson"
        ).read_bytes()
        rollback_seal = rollback_session / "capture-event-journal.json"
        rollback_identity_before = (
            os.stat(rollback_seal, follow_symlinks=False).st_dev,
            os.stat(rollback_seal, follow_symlinks=False).st_ino,
            rollback_seal.read_bytes(),
            rollback_live,
            tuple(sorted(item.name for item in rollback_session.iterdir())),
        )
        fresh_rollback_coordinator = capture.CaptureCoordinator(
            rollback_boundary,
            source_guard=rollback_source,
            lease_factory=contextlib.nullcontext,
            run_id_factory=lambda sample: "id65v3-20260810t233100z-8888888888888888",
        )
        expect_refusal(
            lambda: fresh_rollback_coordinator._load_run(rollback_run_id),
            "Live NDJSON differs from the sealed journal projection",
        )
        expect_refusal(
            lambda: fresh_rollback_coordinator._select_run(
                capture.SESSION_IDS[0], rollback_source.pins
            ),
        )
        expect_refusal(
            lambda: fresh_rollback_coordinator.verify_staging(rollback_run_id),
        )
        rollback_identity_after = (
            os.stat(rollback_seal, follow_symlinks=False).st_dev,
            os.stat(rollback_seal, follow_symlinks=False).st_ino,
            rollback_seal.read_bytes(),
            (rollback_session / "capture-events-live.ndjson").read_bytes(),
            tuple(sorted(item.name for item in rollback_session.iterdir())),
        )
        precommit_close_instance = PrecommitCloseFaultJournal.last_instance
        require(
            rollback_seal.exists()
            and b'"eventType":"capture-aborted"' not in rollback_live
            and b'"eventType":"session-sealed"' not in rollback_live
            and all(
                child.signals == [signal.SIGINT] and child.poll() == 0
                for child in rollback_boundary.children
            )
            and not (
                rollback_seal.exists()
                and b'"eventType":"capture-aborted"' in rollback_live
            )
            and rollback_identity_after == rollback_identity_before,
            "Failed provisional rollback was mislabeled as an ordinary clean abort.",
        )
        require(
            precommit_close_instance is not None
            and precommit_close_instance.close_calls
            == ["live", "file", "directory"]
            and precommit_close_instance._provisional_file_fd is None
            and precommit_close_instance._provisional_directory_fd is None,
            "Precommit cleanup failure skipped a later close or retained authority.",
        )
        seal_fault_rejects += 1

        postcommit_lease_count = 0
        for lease_index, lease_error in enumerate(
            (
                OSError("injected postcommit lease unlock failure"),
                KeyboardInterrupt("injected postcommit lease close interruption"),
            )
        ):
            lease_root = root / f"postcommit-lease-{lease_index}-regression"
            lease_root.mkdir(mode=0o700)
            lease_source = FakeSource(lease_root)
            lease_boundary = FakeBoundary(lease_root, lease_source)
            lease_run_id = (
                "id65v3-20260811t00"
                f"{20 + lease_index:02d}00z-{lease_index + 30:016x}"
            )
            faulting_lease = FaultingExitLease(lease_error)
            lease_coordinator = capture.CaptureCoordinator(
                lease_boundary,
                source_guard=lease_source,
                lease_factory=lambda value=faulting_lease: value,
                run_id_factory=lambda sample, value=lease_run_id: value,
            )
            lease_result = lease_coordinator.capture_session(
                capture.SESSION_IDS[0]
            )
            lease_session = (
                lease_root
                / capture.CAPTURE_STAGING_PREFIX
                / lease_run_id
                / f"00-{capture.SESSION_IDS[0]}"
            )
            lease_live = (
                lease_session / "capture-events-live.ndjson"
            ).read_bytes()
            require(
                lease_result == lease_run_id
                and faulting_lease.entered
                and faulting_lease.exited
                and (lease_session / "capture-event-journal.json").exists()
                and lease_live.count(b'"eventType":"session-sealed"') == 1
                and b'"eventType":"capture-aborted"' not in lease_live
                and lease_coordinator._load_run(
                    lease_run_id, lease_source.pins
                ).session_count == 1,
                "Postcommit lease release fault overrode a consumable capture.",
            )
            postcommit_lease_count += 1

        original_repository_root = capture._repository_root_from_script
        original_real_boundary = capture.RealMacBoundary
        original_capture_coordinator = capture.CaptureCoordinator
        original_print = builtins.print
        cli_run_id = "id65v3-20260811t003000z-4444444444444444"
        cli_calls: list[str] = []
        cli_stderr = io.StringIO()

        class CliCommittedCoordinator:
            def __init__(self, boundary: Any) -> None:
                del boundary

            def capture_session(self, session_id: str) -> str:
                cli_calls.append(session_id)
                return cli_run_id

        def failing_success_print(*values: Any, **kwargs: Any) -> None:
            text = " ".join(str(value) for value in values)
            if text.startswith("ID65_V3_CAPTURE_SESSION_SEALED"):
                raise BrokenPipeError("injected committed success stdout failure")
            original_print(*values, **kwargs)

        try:
            capture._repository_root_from_script = lambda: root
            capture.RealMacBoundary = lambda repository_root: object()
            capture.CaptureCoordinator = CliCommittedCoordinator
            builtins.print = failing_success_print
            with contextlib.redirect_stderr(cli_stderr):
                cli_result = capture.main(
                    ["capture-session", capture.SESSION_IDS[0]]
                )
        finally:
            builtins.print = original_print
            capture.CaptureCoordinator = original_capture_coordinator
            capture.RealMacBoundary = original_real_boundary
            capture._repository_root_from_script = original_repository_root
        require(
            cli_result == 0
            and cli_calls == [capture.SESSION_IDS[0]]
            and "REFUSED" not in cli_stderr.getvalue()
            and "unusable" not in cli_stderr.getvalue(),
            "Committed capture stdout failure was mislabeled REFUSED.",
        )

        cli_stderr = io.StringIO()

        class CliReturnInterruptCoordinator:
            def __init__(self, boundary: Any) -> None:
                del boundary
                self.last_committed_run_id: str | None = None

            def capture_session(self, session_id: str) -> str:
                cli_calls.append(session_id + ".return-interrupt")
                self.last_committed_run_id = cli_run_id
                raise KeyboardInterrupt(
                    "injected CLI committed return/store interruption"
                )

        try:
            capture._repository_root_from_script = lambda: root
            capture.RealMacBoundary = lambda repository_root: object()
            capture.CaptureCoordinator = CliReturnInterruptCoordinator
            with contextlib.redirect_stderr(cli_stderr):
                cli_interrupt_result = capture.main(
                    ["capture-session", capture.SESSION_IDS[0]]
                )
        finally:
            capture.CaptureCoordinator = original_capture_coordinator
            capture.RealMacBoundary = original_real_boundary
            capture._repository_root_from_script = original_repository_root
        require(
            cli_interrupt_result == 0
            and cli_calls[-1]
            == capture.SESSION_IDS[0] + ".return-interrupt"
            and "REFUSED" not in cli_stderr.getvalue(),
            "CLI return/store interruption overrode coordinator commit state.",
        )

        return_interrupt_root = root / "postcommit-return-store-regression"
        return_interrupt_root.mkdir(mode=0o700)
        return_interrupt_source = FakeSource(return_interrupt_root)
        return_interrupt_boundary = FakeBoundary(
            return_interrupt_root, return_interrupt_source
        )
        return_interrupt_run_id = (
            "id65v3-20260811t003500z-5555555555555555"
        )
        return_interrupt_coordinator = PostReturnInterruptCoordinator(
            return_interrupt_boundary,
            source_guard=return_interrupt_source,
            lease_factory=contextlib.nullcontext,
            run_id_factory=lambda sample: return_interrupt_run_id,
        )
        require(
            return_interrupt_coordinator.capture_session(
                capture.SESSION_IDS[0]
            )
            == return_interrupt_run_id
            and return_interrupt_coordinator.last_committed_run_id
            == return_interrupt_run_id
            and return_interrupt_coordinator._load_run(
                return_interrupt_run_id, return_interrupt_source.pins
            ).session_count
            == 1,
            "Postcommit return/store interruption overrode durable success.",
        )

        spawn_handoff_reaps = 0
        for spawn_label in ("watcher", "recorder", "frame capture"):
            for signal_value in (signal.SIGINT, signal.SIGTERM):
                spawn_boundary = SpawnBoundary(
                    root, signal_value=signal_value
                )
                spawn_child = ResistantFakeChild(
                    124_000 + spawn_handoff_reaps
                )
                spawn_registry: list[tuple[str, Any]] = []
                try:
                    spawn_boundary._spawn_and_register_owned_child(
                        lambda value=spawn_child: value,
                        spawn_label,
                        spawn_registry,
                    )
                except KeyboardInterrupt:
                    pass
                else:
                    raise AssertionError(
                        "Deferred termination did not resume after owned-child registration."
                    )
                require(
                    len(spawn_registry) == 1
                    and spawn_registry[0] == (spawn_label, spawn_child),
                    "Deferred termination escaped before exact child registration.",
                )
                require(
                    spawn_child.poll() == -signal.SIGKILL
                    and spawn_child.signals
                    == [signal.SIGINT, signal.SIGTERM, signal.SIGKILL],
                    "Deferred termination did not preserve exact-child cleanup ownership.",
                )
                require(
                    len(spawn_boundary.observed_masks) == 1
                    and signal.SIGINT in spawn_boundary.observed_masks[0]
                    and signal.SIGTERM in spawn_boundary.observed_masks[0],
                    "Spawn-to-registration handoff did not block both termination signals.",
                )
                spawn_handoff_reaps += 1
        for spawn_label in ("watcher", "recorder", "frame capture"):
            spawn_boundary = SpawnBoundary(root, signal_value=None)
            spawn_child = ResistantFakeChild(125_000 + spawn_handoff_reaps)
            spawn_registry = []
            try:
                spawn_boundary._spawn_and_register_owned_child(
                    lambda value=spawn_child: value,
                    spawn_label,
                    spawn_registry,
                )
            except KeyboardInterrupt:
                pass
            else:
                raise AssertionError(
                    "Injected pre-registration interruption did not propagate."
                )
            require(
                not spawn_registry
                and spawn_child.poll() == -signal.SIGKILL
                and spawn_child.signals
                == [signal.SIGINT, signal.SIGTERM, signal.SIGKILL],
                "Pre-registration interruption leaked its exact spawned child.",
            )
            spawn_handoff_reaps += 1

        deadline_root = root / "absolute-recording-deadline-regression"
        deadline_root.mkdir(mode=0o700)
        deadline_source = FakeSource(deadline_root)
        deadline_boundary = FakeBoundary(deadline_root, deadline_source)
        deadline_boundary.expire_on_deadline_call = 3
        deadline_coordinator = capture.CaptureCoordinator(
            deadline_boundary,
            source_guard=deadline_source,
            lease_factory=contextlib.nullcontext,
            run_id_factory=lambda sample: (
                "id65v3-20260810t230000z-0123456789abcdef"
            ),
        )
        expect_refusal(
            lambda: deadline_coordinator.capture_session(capture.SESSION_IDS[0]),
            "30-minute deadline",
        )
        deadline_session = (
            deadline_root
            / capture.CAPTURE_STAGING_PREFIX
            / "id65v3-20260810t230000z-0123456789abcdef"
            / f"00-{capture.SESSION_IDS[0]}"
        )
        deadline_live = (deadline_session / "capture-events-live.ndjson").read_bytes()
        require(
            len(deadline_boundary.deadline_calls) == 3
            and len(set(deadline_boundary.deadline_calls)) == 1
            and len(deadline_boundary.children) == 2
            and all(child.signals == [2] and child.poll() == 0
                    for child in deadline_boundary.children)
            and b'"eventType":"capture-aborted"' in deadline_live
            and not (deadline_session / "capture-event-journal.json").exists(),
            "Absolute recording deadline reset, leaked a child, or sealed its aborted session.",
        )

        watcher_deadline_root = root / "watcher-deadline-boundary"
        watcher_deadline_root.mkdir(mode=0o700)
        watcher_deadline_source = FakeSource(watcher_deadline_root)
        watcher_deadline_boundary = FakeBoundary(
            watcher_deadline_root, watcher_deadline_source
        )
        watcher_deadline_boundary.expire_during_watcher_wait = True
        watcher_deadline_coordinator = capture.CaptureCoordinator(
            watcher_deadline_boundary,
            source_guard=watcher_deadline_source,
            lease_factory=contextlib.nullcontext,
            run_id_factory=lambda sample: (
                "id65v3-20260810t234000z-aaaaaaaaaaaaaaaa"
            ),
        )
        expect_refusal(
            lambda: watcher_deadline_coordinator.capture_session(
                capture.SESSION_IDS[0]
            ),
            "30-minute deadline",
        )
        watcher_deadline_session = (
            watcher_deadline_root
            / capture.CAPTURE_STAGING_PREFIX
            / "id65v3-20260810t234000z-aaaaaaaaaaaaaaaa"
            / f"00-{capture.SESSION_IDS[0]}"
        )
        watcher_deadline_live = (
            watcher_deadline_session / "capture-events-live.ndjson"
        ).read_bytes()
        require(
            len(watcher_deadline_boundary.deadline_calls) == 1
            and len(watcher_deadline_boundary.children) == 2
            and all(
                child.signals == [signal.SIGINT] and child.poll() == 0
                for child in watcher_deadline_boundary.children
            )
            and watcher_deadline_live.count(b'"eventType":"selector-started"') == 1
            and b'"eventType":"landing-marked"' not in watcher_deadline_live
            and b'"eventType":"capture-aborted"' in watcher_deadline_live
            and not (watcher_deadline_session / "capture-event-journal.json").exists()
            and not (watcher_deadline_session / "lone-pad.png").exists(),
            "Watcher deadline wait sealed/journaled a marker or leaked owned children.",
        )

        frame_deadline_root = root / "frame-deadline-boundary"
        frame_deadline_root.mkdir(mode=0o700)
        frame_deadline_source = FakeSource(frame_deadline_root)
        frame_deadline_boundary = FakeBoundary(
            frame_deadline_root, frame_deadline_source
        )
        frame_deadline_boundary.expire_during_frame = True
        frame_deadline_coordinator = capture.CaptureCoordinator(
            frame_deadline_boundary,
            source_guard=frame_deadline_source,
            lease_factory=contextlib.nullcontext,
            run_id_factory=lambda sample: (
                "id65v3-20260810t235000z-bbbbbbbbbbbbbbbb"
            ),
        )
        expect_refusal(
            lambda: frame_deadline_coordinator.capture_session(
                capture.SESSION_IDS[0]
            ),
            "30-minute deadline",
        )
        frame_deadline_session = (
            frame_deadline_root
            / capture.CAPTURE_STAGING_PREFIX
            / "id65v3-20260810t235000z-bbbbbbbbbbbbbbbb"
            / f"00-{capture.SESSION_IDS[0]}"
        )
        frame_deadline_live = (
            frame_deadline_session / "capture-events-live.ndjson"
        ).read_bytes()
        require(
            len(frame_deadline_boundary.frame_deadline_calls) == 1
            and len(set(frame_deadline_boundary.deadline_calls)) == 1
            and frame_deadline_boundary.frame_deadline_calls[0]
            == frame_deadline_boundary.deadline_calls[0]
            and all(
                child.signals == [signal.SIGINT] and child.poll() == 0
                for child in frame_deadline_boundary.children
            )
            and b'"eventType":"capture-aborted"' in frame_deadline_live
            and b'"artifactId":"candidate-primary-edge-0.lone-pad"'
            not in frame_deadline_live
            and not (frame_deadline_session / "capture-event-journal.json").exists()
            and not (frame_deadline_session / "lone-pad.png").exists(),
            "Frame deadline produced metadata/seal or leaked owned children.",
        )

        startup_root = root / "recorder-startup-ownership-regression"
        startup_root.mkdir(mode=0o700)
        startup_source = FakeSource(startup_root)
        startup_boundary = FakeBoundary(startup_root, startup_source)
        startup_boundary.fail_after_recording_start = True
        startup_coordinator = capture.CaptureCoordinator(
            startup_boundary,
            source_guard=startup_source,
            lease_factory=contextlib.nullcontext,
            run_id_factory=lambda sample: (
                "id65v3-20260810t235500z-cccccccccccccccc"
            ),
        )
        expect_refusal(
            lambda: startup_coordinator.capture_session(capture.SESSION_IDS[0]),
            "immediately after recording child creation",
        )
        startup_session = (
            startup_root
            / capture.CAPTURE_STAGING_PREFIX
            / "id65v3-20260810t235500z-cccccccccccccccc"
            / f"00-{capture.SESSION_IDS[0]}"
        )
        startup_live = (
            startup_session / "capture-events-live.ndjson"
        ).read_bytes()
        require(
            len(startup_boundary.children) == 2
            and all(
                child.signals == [signal.SIGINT] and child.poll() == 0
                for child in startup_boundary.children
            )
            and b'"eventType":"recording-started"' not in startup_live
            and b'"eventType":"capture-aborted"' in startup_live,
            "Recorder startup refusal leaked the newly returned owned child.",
        )
        handoff_abort_count = 0
        for handoff_label, watcher_fault, recorder_fault in (
            ("watcher", True, False),
            ("recorder", False, True),
        ):
            handoff_root = root / f"{handoff_label}-ownership-handoff-regression"
            handoff_root.mkdir(mode=0o700)
            handoff_source = FakeSource(handoff_root)
            handoff_boundary = FakeBoundary(handoff_root, handoff_source)
            handoff_boundary.fail_watcher_handoff = watcher_fault
            handoff_boundary.fail_recording_handoff = recorder_fault
            handoff_run_id = (
                "id65v3-20260810t235700z-"
                + ("dddddddddddddddd" if watcher_fault else "eeeeeeeeeeeeeeee")
            )
            handoff_coordinator = capture.CaptureCoordinator(
                handoff_boundary,
                source_guard=handoff_source,
                lease_factory=contextlib.nullcontext,
                run_id_factory=lambda sample, value=handoff_run_id: value,
            )
            expect_refusal(
                lambda coordinator=handoff_coordinator: coordinator.capture_session(
                    capture.SESSION_IDS[0]
                ),
                "ownership handoff failure",
            )
            handoff_session = (
                handoff_root
                / capture.CAPTURE_STAGING_PREFIX
                / handoff_run_id
                / f"00-{capture.SESSION_IDS[0]}"
            )
            handoff_live = (
                handoff_session / "capture-events-live.ndjson"
            ).read_bytes()
            require(
                len(handoff_boundary.children) == (1 if watcher_fault else 2)
                and all(
                    child.signals == [signal.SIGINT] and child.poll() == 0
                    for child in handoff_boundary.children
                )
                and b'"eventType":"capture-aborted"' in handoff_live
                and b'"eventType":"session-sealed"' not in handoff_live,
                f"{handoff_label} ownership handoff failure leaked or sealed.",
            )
            handoff_abort_count += 1

        resistant_boundary = capture.RealMacBoundary(root)
        resistant_child = ResistantFakeChild(123_456)
        expect_refusal(
            lambda: resistant_boundary.stop_owned_child(
                resistant_child, "resistant test child"
            ),
            "required forced termination",
        )
        require(
            resistant_child.signals
            == [signal.SIGINT, signal.SIGTERM, signal.SIGKILL]
            and resistant_child.poll() == -signal.SIGKILL,
            "Resistant exact child was not forcibly reaped in signal order.",
        )
        reap_fault_rejects = 0
        for fault_child in (
            FaultingReapChild(123_457, poll_faults=1),
            FaultingReapChild(123_458, send_faults={signal.SIGINT}),
            FaultingReapChild(123_459, send_faults={signal.SIGTERM}),
            FaultingReapChild(123_460, wait_faults=2),
        ):
            issue = resistant_boundary.ensure_owned_child_reaped(
                fault_child, "fault-injected test child"
            )
            require(
                issue is not None and fault_child.poll() == -signal.SIGKILL,
                "Lifecycle API fault did not preserve error after verified reap.",
            )
            reap_fault_rejects += 1
        unreapable = FaultingReapChild(
            123_461,
            send_faults={signal.SIGINT, signal.SIGTERM, signal.SIGKILL},
            never_reap=True,
        )
        expect_refusal(
            lambda: resistant_boundary.ensure_owned_child_reaped(
                unreapable, "unreapable test child"
            ),
            "could not be reaped",
        )
        require(
            unreapable.poll() is None,
            "Unreapable-child fixture unexpectedly reported a reaped process.",
        )
        reap_fault_rejects += 1

        stop_poll_child = FaultingReapChild(123_463, poll_faults=1)
        try:
            resistant_boundary.stop_owned_child(
                stop_poll_child, "initial-poll-fault test child"
            )
        except capture.CaptureRefusal as exc:
            require(
                "lifecycle API error after verified reap" in str(exc)
                and isinstance(exc.__cause__, OSError),
                "Initial stop poll error was not retained as the refusal cause.",
            )
        else:
            raise AssertionError("Initial stop poll fault was swallowed.")
        require(
            stop_poll_child.poll() == -signal.SIGKILL
            and stop_poll_child.signals
            == [signal.SIGINT, signal.SIGTERM, signal.SIGKILL],
            "Initial stop poll fault prevented exact-child escalation and reap.",
        )

        class RejectingRegistry(list[tuple[str, Any]]):
            def append(self, value: tuple[str, Any]) -> None:
                del value
                raise KeyboardInterrupt("injected ownership registry interruption")

        handoff_child = ResistantFakeChild(123_462)
        try:
            resistant_boundary._register_owned_child(
                handoff_child, "handoff test child", RejectingRegistry()
            )
        except KeyboardInterrupt:
            pass
        else:
            raise AssertionError("Ownership handoff interruption was swallowed.")
        require(
            handoff_child.poll() == -signal.SIGKILL
            and handoff_child.signals
            == [signal.SIGINT, signal.SIGTERM, signal.SIGKILL],
            "Ownership handoff interruption leaked its exact child.",
        )

        class FrameInterruptionBoundary(capture.RealMacBoundary):
            def __init__(self, repository_root: pathlib.Path) -> None:
                super().__init__(repository_root)
                self.frame_child = ResistantFakeChild(123_463)

            def _start_screencapture(
                self,
                window: Any,
                session_path: pathlib.Path,
                destination_leaf: str,
                kind: str,
                owned_children: list[tuple[str, Any]],
                owned_label: str,
            ) -> Any:
                del window, session_path, destination_leaf, kind
                owned_children.append((owned_label, self.frame_child))
                raise KeyboardInterrupt("injected frame wait interruption")

        frame_interruption_boundary = FrameInterruptionBoundary(root)
        try:
            frame_interruption_boundary.capture_frame(
                capture.WindowObservation(1, 1, 0, 0, 1, 1),
                root,
                "lone-pad.png",
                capture.time.monotonic_ns() // 1000 + 1_000_000,
            )
        except KeyboardInterrupt:
            pass
        else:
            raise AssertionError("Frame interruption was swallowed.")
        require(
            frame_interruption_boundary.frame_child.poll() == -signal.SIGKILL,
            "Interrupted internal frame child was not forcibly reaped.",
        )
        late_recovery_root = root / "late-recovery-live-regression"
        late_recovery_root.mkdir(mode=0o700)
        late_recovery_source = FakeSource(late_recovery_root)
        late_recovery_boundary = FakeBoundary(
            late_recovery_root,
            late_recovery_source,
        )
        late_recovery_boundary.late_recovery_after_respawn_marker = True
        late_recovery_coordinator = capture.CaptureCoordinator(
            late_recovery_boundary,
            source_guard=late_recovery_source,
            lease_factory=contextlib.nullcontext,
            run_id_factory=lambda sample: (
                "id65v3-20260810t231000z-fedcba9876543210"
            ),
        )
        expect_refusal(
            lambda: late_recovery_coordinator.capture_session(
                capture.SESSION_IDS[0]
            ),
            "continuous recovery before the respawn marker",
        )
        late_recovery_session = (
            late_recovery_root
            / capture.CAPTURE_STAGING_PREFIX
            / "id65v3-20260810t231000z-fedcba9876543210"
            / f"00-{capture.SESSION_IDS[0]}"
        )
        late_recovery_live = (
            late_recovery_session / "capture-events-live.ndjson"
        ).read_bytes()
        require(
            b'"eventType":"respawn-marked"' not in late_recovery_live
            and b'"eventType":"capture-aborted"' in late_recovery_live
            and not (late_recovery_session / "edge-after.png").exists()
            and all(child.signals == [2] and child.poll() == 0
                    for child in late_recovery_boundary.children),
            "Late live recovery was journaled/framed or leaked owned children.",
        )
        timed_deadline_root = root / "timed-deadline-regression"
        timed_deadline_root.mkdir(mode=0o700)
        timed_source = FakeSource(timed_deadline_root)
        timed_boundary = FakeBoundary(timed_deadline_root, timed_source)
        started = timed_boundary.clock()
        expect_refusal(
            lambda: timed_boundary.wait_for_fresh_return_after(
                started,
                1_000_000,
                "synthetic eligible prompt",
                started.monotonic_microseconds + 999_999,
            ),
            "30-minute deadline",
        )

        require(
            not ACTUAL_DUCKSTATION_CALLS,
            "Offline smoke launched the production DuckStation executable.",
        )

        print(
            "ID65_V3_CAPTURE_COORDINATOR_OFFLINE_SMOKE_OK "
            f"tamperRejects={tamper_count} "
            f"legacyV2NamespaceRejects={legacy_namespace_rejects} "
            f"legacyVersionDomainRejects={legacy_version_domain_rejects} "
            f"fullPinClosureRejects={full_pin_closure_rejects} "
            f"sourceClosureRejects={source_closure_rejects} "
            f"sourceTopologyRejects={source_topology_rejects} "
            f"verifierBindingRejects={verifier_binding_rejects} "
            f"kernHomeRejects={kern_home_rejects} "
            f"snapshotV2Rejects={snapshot_v2_rejects} "
            "prelaunchRepeatRejects=1 "
            f"prelaunchCalls={source.prelaunch_calls - prelaunch_calls_before_run} "
            f"profileSnapshotCalls={source.snapshot_calls - snapshot_calls_before_run} "
            f"staticValidationCalls={source.validate_calls - validate_calls_before_run} "
            f"sharedAuthorityRejects={shared_authority_rejects} "
            f"macBoundaryRejects={boundary_refusals} "
            f"bootBoundaryRejects={boot_boundary_refusals} "
            f"liveWatcherRejects={live_watcher_refusals} "
            f"recordingDurationRejects={duration_refusals} "
            f"assistedFullRunRejects={assisted_full_run_rejects} "
            "nativeCountdownEarlyIgnored=1 nativeCountdownFreshAccepted=1 "
            "terminalCommitOrder=1 "
            "absoluteDeadlineAbort=1 timedDeadlineReject=1 lateRecoveryLiveReject=1 "
            f"sealFaultRejects={seal_fault_rejects} "
            f"postReadbackMutationRejects={post_readback_mutation_rejects} "
            f"resolverDurabilityRows={resolver_durability_rows} "
            f"resolvedCommits={resolved_commit_count} postCommitLeaseExit={postcommit_lease_count} "
            f"spawnHandoffReaps={spawn_handoff_reaps} cliPostCommitStdout=1 closeFaultMatrix=3 "
            "watcherDeadlineAbort=1 frameDeadlineAbort=1 "
            "recorderStartupOwnershipAbort=1 resistantChildReap=1 "
            f"ownershipHandoffAborts={handoff_abort_count} "
            f"reapFaultRejects={reap_fault_rejects} stopInitialPollReject=1 frameInterruptReap=1 "
            "boundedStreamGrowthReject=1 "
            "nativeBootIdentity=1 nativeWatcherBootstrap=1 "
            "nativeWatcherEarlyExitDiagnostic=1 nativeOpenFileLifecycle=1 "
            "nativeKernProcargs2=INJECTED nativeCache=1 "
            "nativeExtraDiscRejects=2 "
            f"actualDuckStationCalls={len(ACTUAL_DUCKSTATION_CALLS)} "
            f"nativeAquaWindowServerMediaGate={native_aqua_media_gate} "
            f"nativeScreencaptureStill={native_screencapture_count} "
            f"nativeScreencaptureVideo={native_screencapture_count} "
            "realListenerCalls=0"
        )


if __name__ == "__main__":
    if sys.argv[1:] == ["--offline-only"]:
        run(offline_only=True)
    elif sys.argv[1:]:
        raise SystemExit(
            "Usage: Test-Capture-SpyroId65V3RuntimeSession.py [--offline-only]"
        )
    else:
        run()
