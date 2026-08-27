#!/usr/bin/env python3
"""Deep offline adversarial test for the exact ID65 aggregate capture contract.

The test imports the coordinator in-process after installing an audit hook that
forbids every process-launch primitive.  All constructive and mutation tests
use temporary roots or in-memory fixture documents.  Persistent production
boundaries are fingerprinted read-only before and after the test.
"""

from __future__ import annotations

import ast
import builtins
import contextlib
import copy
import fcntl
import hashlib
import importlib.util
import io
import json
import os
import pathlib
import stat
import sys
import tempfile
from collections.abc import Callable, Iterator, Mapping, Sequence
from typing import Any


TEST_PATH = pathlib.Path(os.path.abspath(__file__))
COORDINATOR_PATH = TEST_PATH.with_name(
    "Capture-SpyroId65AuthoringAggregateExactV1RuntimeSession.py"
)

PROCESS_EVENTS: list[tuple[str, tuple[Any, ...]]] = []


def _forbid_process_launch(event: str, args: tuple[Any, ...]) -> None:
    if event in {
        "subprocess.Popen",
        "os.exec",
        "os.fork",
        "os.forkpty",
        "os.kill",
        "os.killpg",
        "os.posix_spawn",
        "os.spawn",
        "os.system",
        "pty.spawn",
    }:
        PROCESS_EVENTS.append((event, args))
        raise AssertionError(f"offline test forbids process launch: {event}")


sys.addaudithook(_forbid_process_launch)

SPEC = importlib.util.spec_from_file_location(
    "id65_authoring_aggregate_exact_v1_capture", COORDINATOR_PATH
)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("Cannot load the exact aggregate capture coordinator.")
capture = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = capture
SPEC.loader.exec_module(capture)


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, condition: object, label: str) -> None:
        if not condition:
            raise AssertionError(label)
        self.count += 1

    def refuses(
        self,
        label: str,
        action: Callable[[], object],
        *,
        contains: str | None = None,
    ) -> str:
        try:
            action()
        except capture.ExactCaptureRefusal as exc:
            message = str(exc)
            if contains is not None and contains not in message:
                raise AssertionError(
                    f"{label} refused for the wrong reason: {message!r}"
                ) from exc
            self.count += 1
            return message
        raise AssertionError(f"expected exact-capture refusal: {label}")


def _sha256_path(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while True:
            chunk = stream.read(1024 * 1024)
            if not chunk:
                return digest.hexdigest()
            digest.update(chunk)


def _canonical_sha256(value: object) -> str:
    encoded = json.dumps(
        value,
        allow_nan=False,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def _tree_fingerprint(path: pathlib.Path) -> tuple[object, ...]:
    """Return a content-aware boundary fingerprint without following links."""

    try:
        root_info = os.lstat(path)
    except FileNotFoundError:
        return ("absent",)
    if stat.S_ISLNK(root_info.st_mode):
        return ("symlink", os.readlink(path), root_info.st_mtime_ns)
    if stat.S_ISREG(root_info.st_mode):
        return (
            "file",
            stat.S_IMODE(root_info.st_mode),
            root_info.st_uid,
            root_info.st_gid,
            root_info.st_nlink,
            root_info.st_size,
            root_info.st_mtime_ns,
            root_info.st_ctime_ns,
            _sha256_path(path),
        )
    if not stat.S_ISDIR(root_info.st_mode):
        return ("other", root_info.st_mode, root_info.st_rdev)

    records: list[tuple[object, ...]] = []
    stack = [path]
    while stack:
        directory = stack.pop()
        with os.scandir(directory) as iterator:
            entries = sorted(iterator, key=lambda item: os.fsencode(item.name))
        for entry in entries:
            item = pathlib.Path(entry.path)
            info = entry.stat(follow_symlinks=False)
            relative = item.relative_to(path).as_posix()
            common = (
                relative,
                stat.S_IMODE(info.st_mode),
                info.st_uid,
                info.st_gid,
                info.st_nlink,
                info.st_size,
                info.st_mtime_ns,
                info.st_ctime_ns,
            )
            if stat.S_ISLNK(info.st_mode):
                records.append(("symlink", *common, os.readlink(item)))
            elif stat.S_ISDIR(info.st_mode):
                records.append(("directory", *common))
                stack.append(item)
            elif stat.S_ISREG(info.st_mode):
                # Card/evidence data is always hashed.  Large immutable package
                # binaries remain covered by exact metadata so this smoke stays
                # fast and does not become a second package verifier.
                content_hash = (
                    _sha256_path(item)
                    if info.st_size <= 2 * 1024 * 1024
                    else "metadata-only-large-file"
                )
                records.append(("file", *common, content_hash))
            else:
                records.append(("other", *common, info.st_rdev))
    return (
        "directory",
        stat.S_IMODE(root_info.st_mode),
        root_info.st_uid,
        root_info.st_gid,
        root_info.st_nlink,
        root_info.st_mtime_ns,
        root_info.st_ctime_ns,
        tuple(sorted(records, key=lambda item: os.fsencode(str(item[1])))),
    )


def _production_boundaries() -> tuple[pathlib.Path, ...]:
    root = capture.PRODUCTION_REPOSITORY_ROOT
    memcards = root / capture.PROFILE_MEMCARDS_RELATIVE_PATH
    return (
        root / capture.EXACT_STAGING_PREFIX,
        root / capture.FOCUSED_EVIDENCE_RELATIVE_PATH,
        root / capture.PROFILE_RELATIVE_PATH,
        memcards,
        memcards / capture.CARD_LEAF,
        capture.HOST_SETTINGS_SENTINEL_PATH.parent / "memcards",
        capture.HOST_SETTINGS_SENTINEL_PATH.parent / "savestates",
    )


def _snapshot_production() -> dict[str, tuple[object, ...]]:
    return {
        os.fspath(path): _tree_fingerprint(path)
        for path in _production_boundaries()
    }


def _clone(value: object) -> Any:
    return copy.deepcopy(value)


class InjectedEngineFailure(RuntimeError):
    pass


class LeaseState:
    def __init__(self) -> None:
        self.held = False
        self.events: list[tuple[str, ...]] = []


class GuardLease:
    """Side-effect-free authority-held marker for exhaustive engine cuts."""

    def __init__(self, state: LeaseState) -> None:
        self.state = state

    def __enter__(self) -> "GuardLease":
        if self.state.held:
            raise AssertionError("test lease was entered twice")
        self.state.held = True
        self.state.events.append(("lease-enter",))
        return self

    def __exit__(self, exc_type: object, exc: object, trace: object) -> None:
        self.state.events.append(("lease-exit",))
        self.state.held = False


class ActualLeaseProbe:
    """Expose held state only after the hardened temp-root lease succeeds."""

    def __init__(
        self,
        state: LeaseState,
        root: pathlib.Path,
    ) -> None:
        self.state = state
        self.inner = capture.AuthorityFirstExactLease(
            root,
            authority_directory=pathlib.PurePosixPath("authority"),
            staging_prefix=pathlib.PurePosixPath("staging"),
        )

    def __enter__(self) -> "ActualLeaseProbe":
        self.inner.__enter__()
        self.state.held = True
        self.state.events.append(("actual-authority-lease-enter",))
        return self

    def __exit__(self, exc_type: object, exc: object, trace: object) -> None:
        try:
            self.inner.__exit__(exc_type, exc, trace)
        finally:
            self.state.events.append(("actual-authority-lease-exit",))
            self.state.held = False


class FakeExactRuntimeBackend:
    """Deterministic adapter that never launches or controls a process."""

    def __init__(
        self,
        lease_state: LeaseState,
        *,
        receipt_mode: str | None = None,
        snapshot_mode: str | None = None,
        begin_mode: str | None = None,
    ) -> None:
        self.lease_state = lease_state
        self.receipt_mode = receipt_mode
        self.snapshot_mode = snapshot_mode
        self.begin_mode = begin_mode
        self.events: list[tuple[str, ...]] = []
        self.handles: list[dict[str, object]] = []
        self.abort_count = 0
        self.pending_abort_count = 0
        self._duplicate_receipt: capture.ExactEvidenceReceipt | None = None

    def _guard(self) -> None:
        if not self.lease_state.held:
            raise AssertionError("runtime adapter was called without authority lease")

    def preflight(
        self,
        request: capture.ExactEngineRequest,
    ) -> capture.ExactSimulationPreflight:
        self._guard()
        self.events.append(("preflight", request.run_id))
        return capture.ExactSimulationPreflight(
            simulation_only=True,
            profile_pins_verified=False,
            save_profile_preflight_run=False,
            runtime_observed=False,
            production_capture_enabled=False,
        )

    def capture_prelaunch_empty_card(
        self,
        request: capture.ExactEngineRequest,
    ) -> Mapping[str, object]:
        self._guard()
        self.events.append(("prelaunch-card", request.run_id))
        return capture._synthetic_snapshot(
            "prelaunch-empty",
            "preflight",
            empty=True,
            run_id=request.run_id,
            captured_at_utc="1970-01-01T00:00:00.000Z",
        )

    def begin_session(
        self,
        spec: capture.ExactSessionSpec,
        prior_snapshot: Mapping[str, object],
    ) -> object:
        self._guard()
        handle: dict[str, object] = {
            "spec": spec,
            "active": True,
            "finished": False,
        }
        self.handles.append(handle)
        self.events.append(("begin", spec.session_id))
        if self.begin_mode == "activate-then-raise" and spec.ordinal == 0:
            raise InjectedEngineFailure("partial begin activation")
        return handle

    def abort_pending_session(self, spec: capture.ExactSessionSpec) -> None:
        self._guard()
        matches = [
            handle for handle in self.handles
            if handle["spec"] == spec and bool(handle["active"])
        ]
        if len(matches) != 1:
            raise AssertionError("pending begin cleanup did not own one activation")
        matches[0]["active"] = False
        self.pending_abort_count += 1
        self.events.append(("abort-pending", spec.session_id))

    def _mutate_snapshot(
        self,
        snapshot: dict[str, object],
        phase: str,
        session_id: str,
    ) -> None:
        if self.snapshot_mode == "foreign-session" and (
            session_id == capture.SESSION_IDS[0] and phase == "post-session"
        ):
            snapshot["sessionId"] = capture.SESSION_IDS[1]
        elif self.snapshot_mode == "changed-adjacent-card" and (
            session_id == capture.SESSION_IDS[1] and phase == "pre-session"
        ):
            entry = snapshot["entries"][0]
            entry["sha256"] = "f" * 64
            entry["archiveSha256"] = "f" * 64
            snapshot["sourceArchiveReadbackSha256"] = "f" * 64
        elif self.snapshot_mode == "unclean-exit" and (
            session_id == capture.SESSION_IDS[1] and phase == "post-session"
        ):
            termination = snapshot["processFingerprint"]["termination"]
            termination.update({
                "observed": False,
                "capturedAtUtc": None,
                "exitCode": None,
            })
        elif self.snapshot_mode == "reused-process" and (
            session_id == capture.SESSION_IDS[1]
        ):
            fingerprint = capture._synthetic_process_fingerprint(
                phase, capture.SESSION_IDS[0]
            )
            snapshot["processFingerprint"] = fingerprint
            snapshot["processInstanceId"] = (
                capture.process_instance_id_from_fingerprint(fingerprint)
            )

    def capture_card(
        self,
        handle: object,
        phase: str,
        prior_snapshot: Mapping[str, object],
    ) -> Mapping[str, object]:
        self._guard()
        if not isinstance(handle, dict):
            raise AssertionError("fake handle type changed")
        spec = handle["spec"]
        if not isinstance(spec, capture.ExactSessionSpec):
            raise AssertionError("fake session spec type changed")
        if phase == "pre-session" and handle["finished"]:
            raise AssertionError("pre-card captured after clean finish")
        if phase == "post-session" and not handle["finished"]:
            raise AssertionError("post-card captured before clean finish")
        ordinal = spec.ordinal
        timestamps = {
            (1, "pre-session"): "1970-01-01T00:00:00.020Z",
            (2, "pre-session"): "1970-01-01T00:00:00.040Z",
            (0, "post-session"): "1970-01-01T00:00:00.010Z",
            (1, "post-session"): "1970-01-01T00:00:00.030Z",
            (2, "post-session"): "1970-01-01T00:00:00.050Z",
        }
        card_hashes = {
            (1, "pre-session"): "1" * 64,
            (2, "pre-session"): "2" * 64,
            (0, "post-session"): "1" * 64,
            (1, "post-session"): "2" * 64,
            (2, "post-session"): "3" * 64,
        }
        snapshot = capture._synthetic_snapshot(
            phase,
            spec.session_id,
            prior=prior_snapshot,
            card_sha256=card_hashes[(ordinal, phase)],
            run_id=str(prior_snapshot["runId"]),
            captured_at_utc=timestamps[(ordinal, phase)],
        )
        self._mutate_snapshot(snapshot, phase, spec.session_id)
        self.events.append(("card", spec.session_id, phase))
        return snapshot

    def checkpoint(
        self,
        handle: object,
        checkpoint_id: str,
        ordinal: int,
    ) -> Sequence[capture.ExactEvidenceReceipt]:
        self._guard()
        if not isinstance(handle, dict) or not handle["active"]:
            raise AssertionError("checkpoint used an inactive fake handle")
        spec = handle["spec"]
        if not isinstance(spec, capture.ExactSessionSpec):
            raise AssertionError("fake session spec type changed")
        roles = [
            role for role in spec.backend_evidence_roles
            if capture.EVIDENCE_ROLE_CHECKPOINT_OWNERSHIP[
                spec.session_id
            ][role] == checkpoint_id
        ]
        if self.receipt_mode == "missing" and spec.ordinal == 0:
            roles = [role for role in roles if role != spec.backend_evidence_roles[0]]
        receipts: list[capture.ExactEvidenceReceipt] = []
        for role_id in roles:
            artifact_id = (
                f"evidence.{capture.OFFLINE_FIXTURE_RUN_ID}."
                f"{spec.session_id}.{role_id}"
            )
            material = f"{spec.session_id}:{checkpoint_id}:{role_id}".encode()
            receipt = capture.ExactEvidenceReceipt(
                run_id=capture.OFFLINE_FIXTURE_RUN_ID,
                session_id=spec.session_id,
                checkpoint_id=checkpoint_id,
                role_id=role_id,
                artifact_id=artifact_id,
                repo_relative_path=(
                    capture.EXACT_STAGING_PREFIX.as_posix()
                    + f"/{capture.OFFLINE_FIXTURE_RUN_ID}/sessions/"
                    + f"{spec.session_id}/evidence/{artifact_id}.json"
                ),
                kind="simulation-declaration",
                byte_length=len(material),
                sha256=hashlib.sha256(material).hexdigest(),
                captured_at_utc={
                    capture.SESSION_IDS[0]: "1970-01-01T00:00:00.005Z",
                    capture.SESSION_IDS[1]: "1970-01-01T00:00:00.020Z",
                    capture.SESSION_IDS[2]: "1970-01-01T00:00:00.040Z",
                }[spec.session_id],
            )
            if (
                self.receipt_mode == "foreign-role"
                and spec.ordinal == 0 and not receipts and ordinal == 0
            ):
                receipt = capture.dataclasses.replace(
                    receipt, role_id="foreign-role"
                )
            elif (
                self.receipt_mode == "wrong-checkpoint"
                and spec.ordinal == 0 and not receipts and ordinal == 0
            ):
                receipt = capture.dataclasses.replace(
                    receipt, checkpoint_id="foreign-checkpoint"
                )
            receipts.append(receipt)
            if self._duplicate_receipt is None:
                self._duplicate_receipt = receipt
        if (
            self.receipt_mode == "duplicate"
            and spec.ordinal == 0
            and ordinal == 0
            and self._duplicate_receipt is not None
        ):
            receipts.append(self._duplicate_receipt)
        self.events.append(("checkpoint", spec.session_id, checkpoint_id))
        return tuple(receipts)

    def finish_session_cleanly(self, handle: object) -> None:
        self._guard()
        if not isinstance(handle, dict) or not handle["active"]:
            raise AssertionError("fake clean finish used an inactive handle")
        spec = handle["spec"]
        handle["active"] = False
        handle["finished"] = True
        self.events.append(("finish", spec.session_id))

    def abort_active_session(self, handle: object) -> None:
        self._guard()
        if not isinstance(handle, dict) or not handle["active"]:
            raise AssertionError("fake abort was duplicated or not active")
        spec = handle["spec"]
        handle["active"] = False
        self.abort_count += 1
        self.events.append(("abort", spec.session_id))

    def assert_quiescent(self) -> None:
        self._guard()
        if any(bool(handle["active"]) for handle in self.handles):
            raise capture.ExactCaptureRefusal("fake runtime backend is not quiescent")
        self.events.append(("quiescent",))


class RecordingExactRunStore:
    """In-memory exact/idempotent store with uncertain-commit injection."""

    def __init__(
        self,
        lease_state: LeaseState,
        *,
        fail_boundary: str | None = None,
        fail_commit_cut: tuple[str, int] | None = None,
        corrupt_readback: str | None = None,
    ) -> None:
        self.lease_state = lease_state
        self.fail_boundary = fail_boundary
        self.fail_commit_cut = fail_commit_cut
        self.corrupt_readback = corrupt_readback
        self.origin: dict[str, object] | None = None
        self.snapshots: list[dict[str, object]] = []
        self.receipts: list[capture.ExactEvidenceReceipt] = []
        self.journal_prefixes: dict[str, dict[str, object]] = {}
        self.journals: list[dict[str, object]] = []
        self.active: dict[str, object] | None = None
        self.quarantined = False
        self.bundle: dict[str, object] | None = None
        self.events: list[tuple[str, ...]] = []
        self.commit_index = 0
        self.commit_labels: list[str] = []

    def _guard(self) -> None:
        if not self.lease_state.held:
            raise AssertionError("run store was called without authority lease")

    def _fail(self, boundary: str) -> None:
        if self.fail_boundary == boundary:
            raise InjectedEngineFailure(boundary)

    def _before_commit(self, label: str) -> int:
        index = self.commit_index
        if self.fail_commit_cut == ("before", index):
            raise InjectedEngineFailure(f"before-commit:{index}:{label}")
        return index

    def _after_commit(self, index: int, label: str) -> None:
        if index != self.commit_index:
            raise AssertionError("fake commit index changed during one write")
        self.commit_labels.append(label)
        self.commit_index += 1
        if self.fail_commit_cut == ("after", index):
            raise InjectedEngineFailure(f"after-commit:{index}:{label}")

    def bind_lease(self, lease: object) -> None:
        self._guard()
        self.events.append(("bind-lease",))

    def inspect_resume(
        self,
        run_id: str,
    ) -> capture.ExactResumeState | None:
        self._guard()
        self.events.append(("inspect", run_id))
        if self.origin is None:
            return None
        return capture.ExactResumeState(
            origin=_clone(self.origin),
            card_snapshots=tuple(_clone(self.snapshots)),
            sealed_journals=tuple(_clone(self.journals)),
            evidence_receipts=tuple(_clone(self.receipts)),
            active_session_id=(
                None if self.active is None else str(self.active["sessionId"])
            ),
            quarantined=self.quarantined,
            final_bundle=None if self.bundle is None else _clone(self.bundle),
        )

    def begin_run(
        self,
        origin: Mapping[str, object],
    ) -> Mapping[str, object]:
        self._guard()
        commit_index = self._before_commit("run-origin")
        value = _clone(dict(origin))
        if self.origin is None:
            self.origin = value
        elif self.origin != value:
            raise capture.ExactCaptureRefusal("run origin exact identity conflict")
        self.events.append(("begin-run", str(origin["runId"])))
        observed = _clone(self.origin)
        if self.corrupt_readback == "origin":
            observed["runtimeObserved"] = True
        self._after_commit(commit_index, "run-origin")
        return observed

    def commit_card_snapshot(
        self,
        snapshot: Mapping[str, object],
    ) -> Mapping[str, object]:
        self._guard()
        label = f"card:{snapshot['snapshotId']}"
        commit_index = self._before_commit(label)
        value = _clone(dict(snapshot))
        existing = next(
            (
                item for item in self.snapshots
                if item["snapshotId"] == value["snapshotId"]
            ),
            None,
        )
        if existing is None:
            self.snapshots.append(value)
            existing = value
        elif existing != value:
            raise capture.ExactCaptureRefusal("card snapshot exact identity conflict")
        self.events.append(("card", str(value["snapshotId"])))
        self._after_commit(commit_index, label)
        return _clone(existing)

    def begin_session(
        self,
        spec: capture.ExactSessionSpec,
        prior_snapshot_sha256: str,
    ) -> Mapping[str, object]:
        self._guard()
        label = f"session-marker:{spec.session_id}"
        commit_index = self._before_commit(label)
        marker = {
            "runId": str(self.origin["runId"]),
            "sessionId": spec.session_id,
            "sessionOrdinal": spec.ordinal,
            "priorSnapshotSha256": prior_snapshot_sha256,
        }
        if self.active is None:
            self.active = marker
        elif self.active != marker:
            raise capture.ExactCaptureRefusal("active session exact identity conflict")
        self.events.append(("begin-session", spec.session_id))
        self._after_commit(commit_index, label)
        return _clone(self.active)

    def commit_evidence(
        self,
        receipts: Sequence[capture.ExactEvidenceReceipt],
    ) -> Sequence[capture.ExactEvidenceReceipt]:
        self._guard()
        label = "evidence:" + ",".join(receipt.role_id for receipt in receipts)
        commit_index = self._before_commit(label)
        observed: list[capture.ExactEvidenceReceipt] = []
        for receipt in receipts:
            existing = next(
                (
                    item for item in self.receipts
                    if item.artifact_id == receipt.artifact_id
                ),
                None,
            )
            if existing is None:
                self.receipts.append(_clone(receipt))
                existing = receipt
            elif existing != receipt:
                raise capture.ExactCaptureRefusal("evidence exact identity conflict")
            observed.append(_clone(existing))
        required_receipt_order = {
            (spec.session_id, role_id): ordinal
            for ordinal, (spec, role_id) in enumerate(
                (spec, role_id)
                for spec in capture.exact_session_specs()
                for role_id in spec.required_evidence_roles
            )
        }
        self.receipts.sort(
            key=lambda item: required_receipt_order[
                (item.session_id, item.role_id)
            ]
        )
        self.events.append(("evidence", str(len(receipts))))
        self._after_commit(commit_index, label)
        return tuple(observed)

    def commit_journal_prefix(
        self,
        journal: Mapping[str, object],
    ) -> Mapping[str, object]:
        self._guard()
        value = _clone(dict(journal))
        session_id = str(value["sessionId"])
        label = f"journal-prefix:{session_id}:{len(value['events'])}"
        commit_index = self._before_commit(label)
        if self.active is None or self.active["sessionId"] != session_id:
            raise capture.ExactCaptureRefusal("journal prefix lacks active session")
        prior = self.journal_prefixes.get(session_id)
        if prior is not None and prior != value:
            prior_events = prior["events"]
            new_events = value["events"]
            if len(new_events) <= len(prior_events) or (
                new_events[:len(prior_events)] != prior_events
            ):
                raise capture.ExactCaptureRefusal("journal prefix is not append-only")
        self.journal_prefixes[session_id] = value
        self.events.append(("journal-prefix", session_id, str(len(value["events"]))))
        self._after_commit(commit_index, label)
        return _clone(value)

    def seal_session(
        self,
        journal: Mapping[str, object],
    ) -> Mapping[str, object]:
        self._guard()
        session_id = str(journal["sessionId"])
        self._fail(f"before:seal:{session_id}")
        label = f"session-seal:{session_id}"
        commit_index = self._before_commit(label)
        value = _clone(dict(journal))
        capture.validate_journal(value)
        prefix = self.journal_prefixes.get(session_id)
        if prefix is None or value["events"][:len(prefix["events"])] != prefix["events"]:
            raise capture.ExactCaptureRefusal("sealed journal lost its durable prefix")
        ordinal = int(value["sessionOrdinal"])
        if len(self.journals) == ordinal:
            self.journals.append(value)
        elif len(self.journals) > ordinal and self.journals[ordinal] == value:
            pass
        else:
            raise capture.ExactCaptureRefusal("sealed journal identity conflict")
        self.active = None
        self.events.append(("seal", session_id))
        self._after_commit(commit_index, label)
        self._fail(f"after:seal:{session_id}")
        return _clone(value)

    def commit_run_bundle(
        self,
        bundle: Mapping[str, object],
    ) -> Mapping[str, object]:
        self._guard()
        self._fail("before:bundle")
        commit_index = self._before_commit("run-bundle")
        value = _clone(dict(bundle))
        capture.validate_run_bundle(value)
        if self.bundle is None:
            self.bundle = value
        elif self.bundle != value:
            raise capture.ExactCaptureRefusal("final bundle exact identity conflict")
        self.events.append(("bundle",))
        self._after_commit(commit_index, "run-bundle")
        self._fail("after:bundle")
        return _clone(self.bundle)

    def quarantine(self, run_id: str, reason: str) -> None:
        self._guard()
        self.quarantined = True
        self.events.append(("quarantine", run_id, reason))


def _expected_checks() -> tuple[str, ...]:
    return (
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


def _expected_role_checkpoint_ownership(
) -> dict[str, dict[str, str | None]]:
    sessions = capture.SESSION_IDS
    return {
        sessions[0]: {
            "candidate-load-armed": "resume-fresh-candidate",
            "candidate-spawn-frame": "resume-fresh-candidate",
            "landing-watcher": "resume-fresh-candidate",
            "authored-face-view-frame": "face-texture-complete",
            "grass-index-88-frame": "grass-index-88-framed",
            "grass-index-107-frame": "grass-index-107-framed",
            "authored-movement-interval": "movement-group-complete",
            "movement-safe-watcher-suffix": "movement-group-complete",
            "jump-start-frame": "movement-group-complete",
            "stable-post-landing-frame": "movement-group-complete",
            "camera-occlusion-interval": "camera-group-complete",
            "camera-stable-endpoint": "camera-group-complete",
            "music-observation-120500ms": "camera-group-complete",
            "live-music-operator-attestation": "camera-group-complete",
            "in-level-total-200-frame": "in-level-total-framed",
            "pause-zero-egg-token": "pause-total-zero-egg-framed",
            "pause-total-200-frame": "pause-total-zero-egg-framed",
            "ordinary-save-completed-frame": "ordinary-save-complete",
            "save-progress-token": "ordinary-save-complete",
            "session0-post-card": None,
        },
        sessions[1]: {
            "candidate-reentry-watcher": "resume-clean-restart",
            "ordinary-load-completed-frame": "loaded-save-confirmed",
            "load-progress-token": "loaded-save-confirmed",
            "normal-exit-started": "exit-destination-confirmed",
            "destination-65-frame": "exit-destination-confirmed",
            "fall-authorized-frame": "fall-authorized",
            "fall-respawn-watcher": "fall-authorized",
            "post-respawn-frame": "fall-authorized",
            "session1-pre-card": None,
            "distinct-process-fingerprints": None,
        },
        sessions[2]: {
            "town-square-terrain-visible-frame": "resume-fresh-town-square",
            "town-square-collision-route-60500ms": "retail-route-complete",
            "town-square-collision-watcher": "retail-route-complete",
            "town-square-moby-visibility-frame": "retail-route-complete",
            "town-square-music-observation-60500ms": "retail-route-complete",
            "town-square-live-music-operator-attestation": (
                "retail-route-complete"
            ),
            "town-square-totals-frame": "retail-totals-framed",
            "town-square-save-transition": "retail-save-complete",
            "town-square-exit-frame": "retail-exit-complete",
        },
    }


def _test_exact_plan(checks: Checks) -> None:
    expected_sessions = (
        "candidate-authoring-save",
        "candidate-clean-restart",
        "retail-town-square-control",
    )
    expected_checks = _expected_checks()
    checks.require(capture.SESSION_IDS == expected_sessions, "session order changed")
    checks.require(len(capture.SESSION_IDS) == 3, "session count is not exactly three")
    checks.require(
        capture.EVENTUAL_CHECK_IDS == expected_checks,
        "the exact ordered 17-check list changed",
    )
    checks.require(
        len(capture.EVENTUAL_CHECK_IDS)
        == len(set(capture.EVENTUAL_CHECK_IDS))
        == 17,
        "check IDs are not 17 unique bindings",
    )
    checks.require(
        tuple(binding.check_id for binding in capture.CHECK_BINDINGS)
        == expected_checks,
        "binding order no longer matches the checklist",
    )
    checks.require(
        len(capture.CHECK_BINDINGS) == 17,
        "there must be one binding object per check",
    )

    expected_session_bindings = {
        check_id: (expected_sessions[0],) for check_id in expected_checks
    }
    expected_session_bindings["candidate.fall-respawn-stable"] = (
        expected_sessions[1],
    )
    expected_session_bindings["candidate.exit-destination-65"] = (
        expected_sessions[1],
    )
    expected_session_bindings["candidate.save-persistence"] = expected_sessions[:2]
    expected_session_bindings["retail.town-square-preserved"] = (
        expected_sessions[2],
    )
    for binding in capture.CHECK_BINDINGS:
        checks.require(
            binding.session_ids == expected_session_bindings[binding.check_id],
            f"session binding changed for {binding.check_id}",
        )
        checks.require(
            bool(binding.evidence_roles)
            and len(binding.evidence_roles) == len(set(binding.evidence_roles)),
            f"evidence roles are empty or duplicated for {binding.check_id}",
        )
        checks.require(
            all(
                isinstance(role, capture.ExactEvidenceRoleBinding)
                and bool(role.role_id)
                and role.session_id in binding.session_ids
                for role in binding.evidence_roles
            ),
            f"evidence role ownership escaped {binding.check_id}",
        )

    jump = next(
        binding
        for binding in capture.CHECK_BINDINGS
        if binding.check_id == "candidate.jump-land-stable"
    )
    checks.require(
        tuple(role.role_id for role in jump.evidence_roles)
        == (
            "jump-start-frame",
            "landing-watcher",
            "stable-post-landing-frame",
        )
        and {role.session_id for role in jump.evidence_roles}
        == {expected_sessions[0]},
        "jump/landing role coverage or ownership changed",
    )
    town_square = next(
        binding
        for binding in capture.CHECK_BINDINGS
        if binding.check_id == "retail.town-square-preserved"
    )
    checks.require(
        tuple(role.role_id for role in town_square.evidence_roles)
        == (
            "town-square-terrain-visible-frame",
            "town-square-collision-route-60500ms",
            "town-square-collision-watcher",
            "town-square-moby-visibility-frame",
            "town-square-totals-frame",
            "town-square-exit-frame",
            "town-square-save-transition",
            "town-square-music-observation-60500ms",
            "town-square-live-music-operator-attestation",
        )
        and {role.session_id for role in town_square.evidence_roles}
        == {expected_sessions[2]},
        "Town Square role coverage or ownership changed",
    )
    save = next(
        binding
        for binding in capture.CHECK_BINDINGS
        if binding.check_id == "candidate.save-persistence"
    )
    checks.require(
        {role.role_id: role.session_id for role in save.evidence_roles}
        == {
            "ordinary-save-completed-frame": expected_sessions[0],
            "save-progress-token": expected_sessions[0],
            "session0-post-card": expected_sessions[0],
            "session1-pre-card": expected_sessions[1],
            "ordinary-load-completed-frame": expected_sessions[1],
            "load-progress-token": expected_sessions[1],
            "distinct-process-fingerprints": expected_sessions[1],
        },
        "cross-session save evidence ownership changed",
    )

    checks.require(
        tuple(capture.OPERATOR_CHECKPOINTS) == expected_sessions,
        "operator checkpoint order changed",
    )
    checks.require(
        tuple(len(capture.OPERATOR_CHECKPOINTS[item]) for item in expected_sessions)
        == (9, 4, 5),
        "the exact 18-checkpoint distribution changed",
    )
    expected_role_owners = _expected_role_checkpoint_ownership()
    observed_role_owners = {
        session: dict(capture.EVIDENCE_ROLE_CHECKPOINT_OWNERSHIP[session])
        for session in expected_sessions
    }
    checks.require(
        observed_role_owners == expected_role_owners,
        "semantic evidence role/checkpoint ownership changed",
    )
    checks.require(
        all(
            set(observed_role_owners[session])
            == set(capture._session_evidence_role_ids(session))
            and len(observed_role_owners[session])
            == len(capture._session_evidence_role_ids(session))
            for session in expected_sessions
        ),
        "evidence role/checkpoint map does not own every role exactly once",
    )
    capture._validate_plan()
    checks.require(True, "exact plan validation failed")
    checks.require(
        capture.EXACT_PLAN_SHA256
        == _canonical_sha256(capture._plan_payload()),
        "plan closure is not canonical",
    )
    checks.require(
        capture.EXACT_CHECKLIST_SHA256
        == _canonical_sha256(
            {
                "checkIds": list(capture.EVENTUAL_CHECK_IDS),
                "bindings": capture._binding_payload(),
            }
        ),
        "checklist closure is not canonical",
    )
    checks.require(
        capture.OPERATIONAL_TOOL_PINS_SHA256
        == _canonical_sha256(capture._tool_pin_payload()),
        "operational tool closure is not canonical",
    )
    checks.require(
        capture.PROFILE_PIN_SET_SHA256
        == _canonical_sha256(capture._profile_pin_payload()),
        "profile pin closure is not canonical",
    )
    checks.require(
        capture.SCHEMA_CLOSURE_SHA256
        == _canonical_sha256(capture._schema_closure_payload()),
        "schema closure is not canonical",
    )
    checks.require(
        capture.SOURCE_DECLARATION_CLOSURE_SHA256
        == _canonical_sha256(
            capture._source_declaration_closure_payload(
                capture.OPERATIONAL_TOOL_PINS_SHA256,
                capture.PROFILE_PIN_SET_SHA256,
            )
        ),
        "source declaration closure is not canonical",
    )
    checks.require(
        capture.WORKFLOW_DECLARATION_CLOSURE_SHA256
        == _canonical_sha256(
            capture._workflow_declaration_closure_payload(
                exact_plan_sha256=capture.EXACT_PLAN_SHA256,
                exact_checklist_sha256=capture.EXACT_CHECKLIST_SHA256,
                schema_closure_sha256=capture.SCHEMA_CLOSURE_SHA256,
                source_declaration_closure_sha256=(
                    capture.SOURCE_DECLARATION_CLOSURE_SHA256
                ),
                profile_pin_set_sha256=capture.PROFILE_PIN_SET_SHA256,
            )
        ),
        "workflow declaration closure is not canonical",
    )
    checks.require(
        capture.CARD_LEAF == "Spyro the Dragon (USA)_1.mcd"
        and capture.CARD_SIZE == 131_072
        and capture.CARD_MODE == 0o600
        and capture.MEMCARDS_MODE == 0o700,
        "card leaf/size/mode convention changed",
    )
    checks.require(
        capture.CARD_LEAF_SIZE_PROVENANCE
        == "observed-host-duckstation-per-game-title-convention"
        and capture.CARD_BYTES_AUTHORITY == "isolated-run-derived-only",
        "card provenance or isolated bytes authority changed",
    )

    operational = json.dumps(
        {
            "sessions": capture.SESSION_IDS,
            "bindings": capture._binding_payload(),
            "checkpoints": capture.OPERATOR_CHECKPOINTS,
            "tools": capture._tool_pin_payload(),
        },
        sort_keys=True,
    )
    for token in capture.FORBIDDEN_LEGACY_TOKENS:
        checks.require(token not in operational, f"legacy token is operational: {token}")
    for token in ("gnastys-loot", "sunny-flight", "lone-pad", '"E0"', '"E1"', '"E2"'):
        checks.require(token.lower() not in operational.lower(), f"legacy scope leaked: {token}")
    checks.require(
        all(
            "Automate-SpyroId65" not in pin.relative_path
            and "AggregateV1RuntimeSession" not in pin.relative_path
            for pin in capture.OPERATIONAL_TOOL_PINS
        ),
        "automation or the legacy aggregate wrapper entered operational pins",
    )

    source = COORDINATOR_PATH.read_text(encoding="utf-8")
    syntax = ast.parse(source, filename=os.fspath(COORDINATOR_PATH))
    imported = {
        alias.name
        for node in ast.walk(syntax)
        if isinstance(node, (ast.Import, ast.ImportFrom))
        for alias in node.names
    }
    checks.require(
        not any(
            "Automate" in name or "Capture-SpyroId65AuthoringAggregateV1" in name
            for name in imported
        ),
        "legacy capture or automation module is imported",
    )
    checks.require(
        "subprocess" not in imported and "multiprocessing" not in imported,
        "coordinator imports a process-launch facility",
    )


def _test_profile_pin_declaration_guards(checks: Checks) -> None:
    capture._validate_profile_pin_declarations()
    checks.require(True, "valid static profile declarations refused")

    def drift(
        label: str,
        name: str,
        replacement: object,
        validator: Callable[[], object] = capture._validate_profile_pin_declarations,
    ) -> None:
        original = getattr(capture, name)
        setattr(capture, name, replacement)
        try:
            checks.refuses(label, validator)
        finally:
            setattr(capture, name, original)

    drift("malformed profile hash", "EXPECTED_SETTINGS_SHA256", "not-a-hash")
    drift("zero profile size", "EXPECTED_SETTINGS_SIZE", 0)
    drift(
        "profile path escape",
        "PROFILE_SETTINGS_RELATIVE_PATH",
        pathlib.PurePosixPath("../escaped-settings.ini"),
    )
    drift("card leaf convention drift", "CARD_LEAF", "foreign-card.mcd")
    drift("card byte length drift", "CARD_SIZE", 0)
    drift("card mode drift", "CARD_MODE", 0o644)
    drift("memcards mode drift", "MEMCARDS_MODE", 0o755)
    drift(
        "card provenance drift",
        "CARD_LEAF_SIZE_PROVENANCE",
        "unverified-convention",
    )
    drift("host-seeded card authority", "CARD_BYTES_AUTHORITY", "host-seeded")

    for closure_name in (
        "PROFILE_PIN_SET_SHA256",
        "SCHEMA_CLOSURE_SHA256",
        "SOURCE_DECLARATION_CLOSURE_SHA256",
        "WORKFLOW_DECLARATION_CLOSURE_SHA256",
    ):
        original = getattr(capture, closure_name)
        replacement = "f" * 64 if original != "f" * 64 else "e" * 64
        drift(
            f"well-formed but stale {closure_name}",
            closure_name,
            replacement,
            capture._validate_plan,
        )

    original_bindings = capture.CHECK_BINDINGS
    save_index = next(
        index
        for index, binding in enumerate(original_bindings)
        if binding.check_id == "candidate.save-persistence"
    )
    save = original_bindings[save_index]
    swapped_roles = tuple(
        capture.ExactEvidenceRoleBinding(
            role.role_id,
            (
                capture.SESSION_IDS[1]
                if role.role_id == "session0-post-card"
                else capture.SESSION_IDS[0]
                if role.role_id == "session1-pre-card"
                else role.session_id
            ),
        )
        for role in save.evidence_roles
    )
    forged = capture.ExactCheckBinding(
        save.check_id,
        save.session_ids,
        swapped_roles,
    )
    changed_bindings = list(original_bindings)
    changed_bindings[save_index] = forged
    capture.CHECK_BINDINGS = tuple(changed_bindings)
    try:
        checks.refuses(
            "cross-session evidence-role owner swap",
            capture._validate_plan,
        )
    finally:
        capture.CHECK_BINDINGS = original_bindings


class StagingRecorder:
    """Observe staging filesystem operations and global authority flock order."""

    def __init__(self, root: pathlib.Path, staging: pathlib.PurePosixPath) -> None:
        self.root = root
        self.staging = pathlib.Path(os.path.normpath(os.fspath(root / staging)))
        self.events: list[tuple[str, bool, str]] = []
        self.flock_events: list[tuple[str, str]] = []
        self.fd_paths: dict[int, pathlib.Path] = {}
        self.authority_locked = False
        self.originals: dict[str, Any] = {}

    def _absolute(self, path: object, dir_fd: int | None = None) -> pathlib.Path | None:
        if not isinstance(path, (str, bytes, os.PathLike)):
            return None
        try:
            decoded = os.fsdecode(path)
        except (TypeError, ValueError):
            return None
        candidate = pathlib.Path(decoded)
        if not candidate.is_absolute():
            if dir_fd is not None and dir_fd in self.fd_paths:
                candidate = self.fd_paths[dir_fd] / candidate
            else:
                candidate = pathlib.Path.cwd() / candidate
        return pathlib.Path(os.path.normpath(os.fspath(candidate)))

    def _observe(self, operation: str, path: object, dir_fd: int | None = None) -> None:
        candidate = self._absolute(path, dir_fd)
        if candidate is not None and (
            candidate == self.staging or self.staging in candidate.parents
        ):
            self.events.append((operation, self.authority_locked, os.fspath(candidate)))

    def __enter__(self) -> "StagingRecorder":
        self.originals = {
            "open": os.open,
            "close": os.close,
            "dup": os.dup,
            "mkdir": os.mkdir,
            "listdir": os.listdir,
            "scandir": os.scandir,
            "stat": os.stat,
            "lstat": os.lstat,
            "builtins_open": builtins.open,
            "io_open": io.open,
            "flock": fcntl.flock,
        }

        def observed_open(path: object, *args: Any, **kwargs: Any) -> int:
            dir_fd = kwargs.get("dir_fd")
            self._observe("open", path, dir_fd)
            fd = self.originals["open"](path, *args, **kwargs)
            resolved = self._absolute(path, dir_fd)
            if resolved is not None:
                self.fd_paths[fd] = resolved
            return fd

        def observed_close(fd: int) -> None:
            try:
                self.originals["close"](fd)
            finally:
                self.fd_paths.pop(fd, None)

        def observed_dup(fd: int) -> int:
            duplicated = self.originals["dup"](fd)
            if fd in self.fd_paths:
                self.fd_paths[duplicated] = self.fd_paths[fd]
            return duplicated

        def observed_mkdir(path: object, *args: Any, **kwargs: Any) -> None:
            self._observe("mkdir", path, kwargs.get("dir_fd"))
            self.originals["mkdir"](path, *args, **kwargs)

        def observed_listdir(path: object = ".") -> list[Any]:
            self._observe("list", path)
            return self.originals["listdir"](path)

        def observed_scandir(path: object = ".") -> Any:
            self._observe("list", path)
            return self.originals["scandir"](path)

        def observed_stat(path: object, *args: Any, **kwargs: Any) -> os.stat_result:
            self._observe("stat", path, kwargs.get("dir_fd"))
            return self.originals["stat"](path, *args, **kwargs)

        def observed_lstat(path: object, *args: Any, **kwargs: Any) -> os.stat_result:
            self._observe("lstat", path, kwargs.get("dir_fd"))
            return self.originals["lstat"](path, *args, **kwargs)

        def observed_builtin_open(path: object, *args: Any, **kwargs: Any) -> Any:
            self._observe("open", path)
            return self.originals["builtins_open"](path, *args, **kwargs)

        def observed_io_open(path: object, *args: Any, **kwargs: Any) -> Any:
            self._observe("open", path)
            return self.originals["io_open"](path, *args, **kwargs)

        def observed_flock(fd: int, operation: int) -> Any:
            path = self.fd_paths.get(fd)
            result = self.originals["flock"](fd, operation)
            if operation & fcntl.LOCK_EX:
                label = "unknown" if path is None else os.fspath(path)
                self.flock_events.append(("exclusive", label))
                if path is not None and path.name == capture.GLOBAL_AUTHORITY_LOCK_LEAF:
                    self.authority_locked = True
            return result

        os.open = observed_open
        os.close = observed_close
        os.dup = observed_dup
        os.mkdir = observed_mkdir
        os.listdir = observed_listdir
        os.scandir = observed_scandir
        os.stat = observed_stat
        os.lstat = observed_lstat
        builtins.open = observed_builtin_open
        io.open = observed_io_open
        fcntl.flock = observed_flock
        return self

    def __exit__(self, exc_type: object, exc: object, traceback: object) -> None:
        os.open = self.originals["open"]
        os.close = self.originals["close"]
        os.dup = self.originals["dup"]
        os.mkdir = self.originals["mkdir"]
        os.listdir = self.originals["listdir"]
        os.scandir = self.originals["scandir"]
        os.stat = self.originals["stat"]
        os.lstat = self.originals["lstat"]
        builtins.open = self.originals["builtins_open"]
        io.open = self.originals["io_open"]
        fcntl.flock = self.originals["flock"]


def _test_authority_order(checks: Checks) -> None:
    temporary_parent = pathlib.Path(tempfile.gettempdir()).resolve(strict=True)
    with tempfile.TemporaryDirectory(
        prefix="id65-exact-authority-test-", dir=temporary_parent
    ) as temporary:
        root = pathlib.Path(temporary)
        root.chmod(0o700)
        authority_relative = pathlib.PurePosixPath("authority")
        staging_relative = pathlib.PurePosixPath("aggregate/capture-staging")
        authority = root / authority_relative
        authority.mkdir(mode=0o700)

        with StagingRecorder(root, staging_relative) as recorder:
            lease = capture.AuthorityFirstExactLease(
                root,
                authority_directory=authority_relative,
                staging_prefix=staging_relative,
            )
            checks.require(not recorder.events, "lease constructor accessed staging")
            lease.__enter__()
            try:
                checks.require(bool(recorder.events), "lease never accessed exact staging")
                checks.require(
                    all(locked for _, locked, _ in recorder.events),
                    f"staging access preceded authority flock: {recorder.events!r}",
                )
                checks.require(
                    len(recorder.flock_events) == 2
                    and recorder.flock_events[0][1].endswith(
                        "/" + capture.GLOBAL_AUTHORITY_LOCK_LEAF
                    )
                    and recorder.flock_events[1][1].endswith(
                        "/" + capture.EXACT_STAGING_LOCK_LEAF
                    ),
                    f"exclusive lock order changed: {recorder.flock_events!r}",
                )
            finally:
                lease.__exit__(None, None, None)
            checks.require(
                all(
                    getattr(lease, name) == -1
                    for name in (
                        "root_fd",
                        "authority_fd",
                        "authority_lock_fd",
                        "staging_fd",
                        "staging_lock_fd",
                    )
                ),
                "fd-relative lease leaked a descriptor",
            )

        # Hold the global lock independently.  A contending lease must refuse
        # before even evaluating the staging prefix.
        authority_fd = os.open(
            authority,
            os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
        )
        lock_fd = os.open(
            capture.GLOBAL_AUTHORITY_LOCK_LEAF,
            os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW,
            0o600,
            dir_fd=authority_fd,
        )
        fcntl.flock(lock_fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        try:
            with StagingRecorder(root, staging_relative) as contention:
                contender = capture.AuthorityFirstExactLease(
                    root,
                    authority_directory=authority_relative,
                    staging_prefix=staging_relative,
                )
                checks.refuses(
                    "global authority contention",
                    contender.__enter__,
                    contains="global authority",
                )
                checks.require(
                    not contention.events,
                    f"contention touched staging: {contention.events!r}",
                )
        finally:
            os.close(lock_fd)
            os.close(authority_fd)

        fresh_staging = pathlib.PurePosixPath("preflight/capture-staging")
        with StagingRecorder(root, fresh_staging) as refused:
            checks.refuses(
                "preflight fails before staging",
                lambda: capture._refuse_live_operation("preflight"),
                contains=capture.EXACT_CAPTURE_ENGINE_BLOCKER_ID,
            )
            checks.require(
                not refused.events,
                "failed preflight produced a staging filesystem event",
            )
        checks.require(
            not (root / fresh_staging).exists(),
            "failed preflight created a staging directory",
        )


@contextlib.contextmanager
def _temporary_lease_root(prefix: str) -> Iterator[pathlib.Path]:
    temporary_parent = pathlib.Path(tempfile.gettempdir()).resolve(strict=True)
    with tempfile.TemporaryDirectory(prefix=prefix, dir=temporary_parent) as temporary:
        root = pathlib.Path(temporary)
        root.chmod(0o700)
        yield root


def _prepare_lease_root(
    root: pathlib.Path,
    authority_relative: pathlib.PurePosixPath = pathlib.PurePosixPath("authority"),
) -> None:
    authority = root / authority_relative
    authority.mkdir(mode=0o700, parents=True)


def _test_hardened_lease_adversaries(checks: Checks) -> None:
    production_before = _snapshot_production()
    production_lease = capture.AuthorityFirstExactLease(
        capture.PRODUCTION_REPOSITORY_ROOT
    )
    checks.refuses(
        "production lease API disabled",
        production_lease.__enter__,
        contains=capture.EXACT_CAPTURE_ENGINE_BLOCKER_ID,
    )
    checks.require(
        _snapshot_production() == production_before,
        "disabled production lease mutated a persistent boundary",
    )

    with _temporary_lease_root("id65-exact-lease-paths-") as root:
        safe_authority = pathlib.PurePosixPath("authority")
        safe_staging = pathlib.PurePosixPath("staging")
        for label, authority, staging in (
            (
                "authority traversal",
                pathlib.PurePosixPath("../escaped"),
                safe_staging,
            ),
            (
                "staging traversal",
                safe_authority,
                pathlib.PurePosixPath("nested/../../escaped"),
            ),
            (
                "absolute authority",
                pathlib.PurePosixPath("/absolute/authority"),
                safe_staging,
            ),
            (
                "absolute staging",
                safe_authority,
                pathlib.PurePosixPath("/absolute/staging"),
            ),
        ):
            checks.refuses(
                label,
                lambda authority=authority, staging=staging: (
                    capture.AuthorityFirstExactLease(
                        root,
                        authority_directory=authority,
                        staging_prefix=staging,
                    )
                ),
                contains="safe relative",
            )

        for label, authority, staging in (
            ("identical lease paths", safe_authority, safe_authority),
            (
                "authority contains staging",
                safe_authority,
                pathlib.PurePosixPath("authority/staging"),
            ),
            (
                "staging contains authority",
                pathlib.PurePosixPath("staging/authority"),
                safe_staging,
            ),
        ):
            checks.refuses(
                label,
                lambda authority=authority, staging=staging: (
                    capture.AuthorityFirstExactLease(
                        root,
                        authority_directory=authority,
                        staging_prefix=staging,
                    )
                ),
                contains="overlap",
            )

        checks.refuses(
            "partially replaced production lease path",
            lambda: capture.AuthorityFirstExactLease(
                root,
                authority_directory=capture.GLOBAL_AUTHORITY_DIRECTORY,
                staging_prefix=safe_staging,
            ),
            contains="partially replaced",
        )
        checks.refuses(
            "production root with custom lease paths",
            lambda: capture.AuthorityFirstExactLease(
                capture.PRODUCTION_REPOSITORY_ROOT,
                authority_directory=safe_authority,
                staging_prefix=safe_staging,
            ).__enter__(),
            contains="audited production root",
        )

    temporary_parent = pathlib.Path(tempfile.gettempdir()).resolve(strict=True)
    checks.refuses(
        "temporary parent used as lease root",
        lambda: capture.AuthorityFirstExactLease(
            temporary_parent,
            authority_directory=pathlib.PurePosixPath("authority"),
            staging_prefix=pathlib.PurePosixPath("staging"),
        ).__enter__(),
        contains="isolated temp child",
    )

    with _temporary_lease_root("id65-exact-root-target-") as target:
        link = target.parent / f"{target.name}-link"
        link.symlink_to(target.name)
        try:
            checks.refuses(
                "symlink lease root",
                lambda: capture.AuthorityFirstExactLease(
                    link,
                    authority_directory=pathlib.PurePosixPath("authority"),
                    staging_prefix=pathlib.PurePosixPath("staging"),
                ).__enter__(),
                contains="symbolic link",
            )
        finally:
            link.unlink()

    with _temporary_lease_root("id65-exact-root-mode-") as root:
        root.chmod(0o755)
        checks.refuses(
            "non-private lease root",
            lambda: capture.AuthorityFirstExactLease(
                root,
                authority_directory=pathlib.PurePosixPath("authority"),
                staging_prefix=pathlib.PurePosixPath("staging"),
            ).__enter__(),
            contains="mode 0700",
        )

    with _temporary_lease_root("id65-exact-root-normal-") as root:
        non_normal = root / ".." / root.name
        checks.refuses(
            "non-normalized lease root",
            lambda: capture.AuthorityFirstExactLease(
                non_normal,
                authority_directory=pathlib.PurePosixPath("authority"),
                staging_prefix=pathlib.PurePosixPath("staging"),
            ).__enter__(),
            contains="normalized",
        )

    def symlink_chain_case(
        label: str,
        *,
        authority_relative: pathlib.PurePosixPath,
        staging_relative: pathlib.PurePosixPath,
        setup: Callable[[pathlib.Path], None],
    ) -> None:
        with _temporary_lease_root(f"id65-{label.replace(' ', '-')}-") as root:
            setup(root)
            checks.refuses(
                label,
                lambda: capture.AuthorityFirstExactLease(
                    root,
                    authority_directory=authority_relative,
                    staging_prefix=staging_relative,
                ).__enter__(),
            )

    def setup_authority_parent_symlink(root: pathlib.Path) -> None:
        target = root / "authority-target"
        (target / "leaf").mkdir(mode=0o700, parents=True)
        (root / "authority-link").symlink_to(target.name)

    symlink_chain_case(
        "symlink authority parent",
        authority_relative=pathlib.PurePosixPath("authority-link/leaf"),
        staging_relative=pathlib.PurePosixPath("staging"),
        setup=setup_authority_parent_symlink,
    )

    def setup_staging_parent_symlink(root: pathlib.Path) -> None:
        _prepare_lease_root(root)
        target = root / "staging-target"
        target.mkdir(mode=0o700)
        (root / "staging-link").symlink_to(target.name)

    symlink_chain_case(
        "symlink staging parent",
        authority_relative=pathlib.PurePosixPath("authority"),
        staging_relative=pathlib.PurePosixPath("staging-link/capture"),
        setup=setup_staging_parent_symlink,
    )

    def setup_staging_leaf_symlink(root: pathlib.Path) -> None:
        _prepare_lease_root(root)
        target = root / "staging-target"
        target.mkdir(mode=0o700)
        (root / "staging").symlink_to(target.name)

    symlink_chain_case(
        "symlink staging leaf",
        authority_relative=pathlib.PurePosixPath("authority"),
        staging_relative=pathlib.PurePosixPath("staging"),
        setup=setup_staging_leaf_symlink,
    )

    def setup_authority_lock_symlink(root: pathlib.Path) -> None:
        _prepare_lease_root(root)
        target = root / "authority-lock-target"
        target.write_bytes(b"")
        target.chmod(0o600)
        (root / "authority" / capture.GLOBAL_AUTHORITY_LOCK_LEAF).symlink_to(
            target
        )

    symlink_chain_case(
        "symlink authority lock leaf",
        authority_relative=pathlib.PurePosixPath("authority"),
        staging_relative=pathlib.PurePosixPath("staging"),
        setup=setup_authority_lock_symlink,
    )

    def setup_staging_lock_symlink(root: pathlib.Path) -> None:
        _prepare_lease_root(root)
        staging = root / "staging"
        staging.mkdir(mode=0o700)
        target = root / "staging-lock-target"
        target.write_bytes(b"")
        target.chmod(0o600)
        (staging / capture.EXACT_STAGING_LOCK_LEAF).symlink_to(target)

    symlink_chain_case(
        "symlink staging lock leaf",
        authority_relative=pathlib.PurePosixPath("authority"),
        staging_relative=pathlib.PurePosixPath("staging"),
        setup=setup_staging_lock_symlink,
    )

    for lock_kind in ("authority", "staging"):
        with _temporary_lease_root(f"id65-{lock_kind}-hardlink-") as root:
            _prepare_lease_root(root)
            directory = root / "authority"
            leaf = capture.GLOBAL_AUTHORITY_LOCK_LEAF
            if lock_kind == "staging":
                directory = root / "staging"
                directory.mkdir(mode=0o700)
                leaf = capture.EXACT_STAGING_LOCK_LEAF
            lock_path = directory / leaf
            lock_path.write_bytes(b"")
            lock_path.chmod(0o600)
            os.link(lock_path, directory / "split-lock-alias")
            checks.refuses(
                f"hard-linked {lock_kind} lock",
                lambda root=root: capture.AuthorityFirstExactLease(
                    root,
                    authority_directory=pathlib.PurePosixPath("authority"),
                    staging_prefix=pathlib.PurePosixPath("staging"),
                ).__enter__(),
                contains="metadata",
            )

    def run_flock_race(label: str, trigger: int, action: Callable[[pathlib.Path], None]) -> None:
        with _temporary_lease_root(f"id65-{label.replace(' ', '-')}-") as root:
            _prepare_lease_root(root)
            original_flock = fcntl.flock
            ex_count = 0

            def raced_flock(fd: int, operation: int) -> object:
                nonlocal ex_count
                result = original_flock(fd, operation)
                if operation & fcntl.LOCK_EX:
                    ex_count += 1
                    if ex_count == trigger:
                        action(root)
                return result

            fcntl.flock = raced_flock
            try:
                checks.refuses(
                    label,
                    lambda: capture.AuthorityFirstExactLease(
                        root,
                        authority_directory=pathlib.PurePosixPath("authority"),
                        staging_prefix=pathlib.PurePosixPath("staging"),
                    ).__enter__(),
                )
            finally:
                fcntl.flock = original_flock

    def rebind_authority(root: pathlib.Path) -> None:
        authority = root / "authority"
        authority.rename(root / "authority-rebound-old")
        authority.mkdir(mode=0o700)

    run_flock_race("authority directory rebind", 1, rebind_authority)

    def rebind_staging(root: pathlib.Path) -> None:
        staging = root / "staging"
        staging.rename(root / "staging-rebound-old")
        staging.mkdir(mode=0o700)

    run_flock_race("staging directory rebind", 2, rebind_staging)

    def replace_authority_lock(root: pathlib.Path) -> None:
        lock_path = root / "authority" / capture.GLOBAL_AUTHORITY_LOCK_LEAF
        lock_path.unlink()
        fd = os.open(lock_path, os.O_RDWR | os.O_CREAT | os.O_EXCL, 0o600)
        os.close(fd)

    run_flock_race("authority lock replacement after flock", 1, replace_authority_lock)

    def replace_staging_lock(root: pathlib.Path) -> None:
        lock_path = root / "staging" / capture.EXACT_STAGING_LOCK_LEAF
        lock_path.unlink()
        fd = os.open(lock_path, os.O_RDWR | os.O_CREAT | os.O_EXCL, 0o600)
        os.close(fd)

    run_flock_race("staging lock replacement after flock", 2, replace_staging_lock)

    def weaken_root_mode(root: pathlib.Path) -> None:
        root.chmod(0o777)

    run_flock_race("lease root mode weakened after authority flock", 1, weaken_root_mode)


def _snapshot(
    phase: str,
    session_id: str,
    *,
    prior: Mapping[str, object] | None = None,
    empty: bool = False,
) -> dict[str, object]:
    return capture._synthetic_snapshot(
        phase,
        session_id,
        prior=prior,
        empty=empty,
    )


def _validate(
    value: object,
    phase: str,
    session_id: str,
    prior: Mapping[str, object] | None = None,
) -> None:
    capture.validate_card_snapshot(
        value,
        expected_phase=phase,
        expected_session_id=session_id,
        prior_snapshot=prior,
    )


def _test_card_schema_and_chain(checks: Checks) -> None:
    fixture = capture.build_offline_fixture_bundle()
    empty, post0, pre1, post1, pre2, post2 = fixture["cardSnapshots"]
    for value, phase, session, prior in (
        (empty, "prelaunch-empty", "preflight", None),
        (post0, "post-session", capture.SESSION_IDS[0], empty),
        (pre1, "pre-session", capture.SESSION_IDS[1], post0),
        (post1, "post-session", capture.SESSION_IDS[1], pre1),
        (pre2, "pre-session", capture.SESSION_IDS[2], post1),
        (post2, "post-session", capture.SESSION_IDS[2], pre2),
    ):
        _validate(value, phase, session, prior)
        checks.require(True, f"valid card snapshot refused: {session}/{phase}")

    def mutation(
        label: str,
        source: Mapping[str, object],
        change: Callable[[dict[str, object]], None],
        phase: str,
        session: str,
        prior: Mapping[str, object] | None = None,
        contains: str | None = None,
    ) -> None:
        changed = _clone(source)
        change(changed)
        checks.refuses(
            label,
            lambda: _validate(changed, phase, session, prior),
            contains=contains,
        )

    mutation(
        "root schema extension",
        post0,
        lambda value: value.__setitem__("extra", True),
        "post-session",
        capture.SESSION_IDS[0],
        empty,
        "schema",
    )
    mutation(
        "entry schema extension",
        post0,
        lambda value: value["entries"][0].__setitem__("extra", True),
        "post-session",
        capture.SESSION_IDS[0],
        empty,
        "schema",
    )
    mutation(
        "wrong card leaf",
        post0,
        lambda value: value["entries"][0].__setitem__("leaf", "other.mcd"),
        "post-session",
        capture.SESSION_IDS[0],
        empty,
        "leaf",
    )
    mutation(
        "wrong card byte length",
        post0,
        lambda value: value["entries"][0].__setitem__("byteLength", 0),
        "post-session",
        capture.SESSION_IDS[0],
        empty,
        "byte length",
    )
    mutation(
        "wrong card mode",
        post0,
        lambda value: value["entries"][0].__setitem__("mode", "0644"),
        "post-session",
        capture.SESSION_IDS[0],
        empty,
        "mode",
    )
    mutation(
        "hard-linked card",
        post0,
        lambda value: value["entries"][0].__setitem__("nlink", 2),
        "post-session",
        capture.SESSION_IDS[0],
        empty,
        "link count",
    )
    mutation(
        "extra memory card",
        post0,
        lambda value: value["entries"].append(_clone(value["entries"][0])),
        "post-session",
        capture.SESSION_IDS[0],
        empty,
        "exactly one card",
    )
    mutation(
        "card archive readback mismatch",
        post0,
        lambda value: value["entries"][0].__setitem__("archiveSha256", "2" * 64),
        "post-session",
        capture.SESSION_IDS[0],
        empty,
        "readback",
    )
    mutation(
        "snapshot readback mismatch",
        post0,
        lambda value: value.__setitem__("sourceArchiveReadbackSha256", "2" * 64),
        "post-session",
        capture.SESSION_IDS[0],
        empty,
        "readback",
    )
    mutation(
        "historical host card seed",
        post0,
        lambda value: (
            value["entries"][0].__setitem__(
                "sha256", capture.HISTORICAL_HOST_CARD_SHA256_FORBIDDEN
            ),
            value["entries"][0].__setitem__(
                "archiveSha256", capture.HISTORICAL_HOST_CARD_SHA256_FORBIDDEN
            ),
            value.__setitem__(
                "sourceArchiveReadbackSha256",
                capture.HISTORICAL_HOST_CARD_SHA256_FORBIDDEN,
            ),
        ),
        "post-session",
        capture.SESSION_IDS[0],
        empty,
        "historical host",
    )
    mutation(
        "archive path traversal",
        post0,
        lambda value: value["entries"][0].__setitem__(
            "archiveRepoRelativePath", "../escaped.mcd"
        ),
        "post-session",
        capture.SESSION_IDS[0],
        empty,
        "escaped",
    )
    mutation(
        "bad adjacent chain hash",
        pre1,
        lambda value: value.__setitem__("priorSnapshotSha256", "0" * 64),
        "pre-session",
        capture.SESSION_IDS[1],
        post0,
        "chain hash",
    )

    def rebind(value: dict[str, object]) -> None:
        entry = value["entries"][0]
        entry["sha256"] = "2" * 64
        entry["archiveSha256"] = "2" * 64
        value["sourceArchiveReadbackSha256"] = "2" * 64

    mutation(
        "card rebind between adjacent snapshots",
        pre1,
        rebind,
        "pre-session",
        capture.SESSION_IDS[1],
        post0,
        "adjacent",
    )
    mutation(
        "savestate claim in card snapshot",
        post0,
        lambda value: value.__setitem__("saveStateRelativePath", "savestates/a.sav"),
        "post-session",
        capture.SESSION_IDS[0],
        empty,
        "schema",
    )
    mutation(
        "host seed claim in card snapshot",
        post0,
        lambda value: value.__setitem__("hostSeedSha256", "3" * 64),
        "post-session",
        capture.SESSION_IDS[0],
        empty,
        "schema",
    )
    mutation(
        "empty prelaunch with card",
        empty,
        lambda value: value["entries"].append(_clone(post0["entries"][0])),
        "prelaunch-empty",
        "preflight",
        None,
        "not empty",
    )


def _test_process_fingerprints_and_time(checks: Checks) -> None:
    bundle = capture.build_offline_fixture_bundle()
    snapshots = bundle["cardSnapshots"]
    empty, post0, pre1, post1, pre2, post2 = snapshots

    for snapshot in (post0, pre1, post1, pre2, post2):
        fingerprint = snapshot["processFingerprint"]
        derived = capture.process_instance_id_from_fingerprint(fingerprint)
        checks.require(
            snapshot["processInstanceId"] == derived
            and capture.PROCESS_INSTANCE_ID_RE.fullmatch(derived) is not None,
            "fixture process instance ID is not fingerprint-derived",
        )
    checks.require(
        pre1["processFingerprint"]["identity"]
        == post1["processFingerprint"]["identity"]
        and pre2["processFingerprint"]["identity"]
        == post2["processFingerprint"]["identity"],
        "fixture pre/post process identities differ within a session",
    )
    checks.require(
        pre1["processFingerprint"]["termination"]
        == {
            "observed": False,
            "capturedAtUtc": None,
            "exitCode": None,
            "signal": None,
        }
        and post1["processFingerprint"]["termination"]["observed"] is True
        and post1["processFingerprint"]["termination"]["exitCode"] == 0,
        "fixture process lifecycle is not running-pre/clean-exit-post",
    )

    def rejects_snapshot(
        label: str,
        source: Mapping[str, object],
        prior: Mapping[str, object],
        change: Callable[[dict[str, object]], None],
        *,
        contains: str | None = None,
    ) -> None:
        changed = _clone(source)
        change(changed)
        checks.refuses(
            label,
            lambda: capture.validate_card_snapshot(
                changed,
                expected_phase=changed["phase"],
                expected_session_id=changed["sessionId"],
                expected_run_id=changed["runId"],
                prior_snapshot=prior,
            ),
            contains=contains,
        )

    rejects_snapshot(
        "process fingerprint root extension",
        post0,
        empty,
        lambda value: value["processFingerprint"].__setitem__("extra", True),
        contains="root schema",
    )
    rejects_snapshot(
        "process fingerprint format",
        post0,
        empty,
        lambda value: value["processFingerprint"].__setitem__("format", "wrong"),
        contains="format",
    )
    rejects_snapshot(
        "process fingerprint version",
        post0,
        empty,
        lambda value: value["processFingerprint"].__setitem__("formatVersion", 2),
        contains="format",
    )
    rejects_snapshot(
        "process identity extension",
        post0,
        empty,
        lambda value: value["processFingerprint"]["identity"].__setitem__(
            "extra", True
        ),
        contains="identity schema",
    )
    rejects_snapshot(
        "zero process PID",
        post0,
        empty,
        lambda value: value["processFingerprint"]["identity"].__setitem__("pid", 0),
        contains="process.pid",
    )
    rejects_snapshot(
        "boolean process PID",
        post0,
        empty,
        lambda value: value["processFingerprint"]["identity"].__setitem__(
            "pid", True
        ),
        contains="process.pid",
    )
    rejects_snapshot(
        "absolute process executable path",
        post0,
        empty,
        lambda value: value["processFingerprint"]["identity"].__setitem__(
            "executableProfileRelativePath", "/tmp/DuckStation"
        ),
        contains="executable",
    )
    rejects_snapshot(
        "traversing process executable path",
        post0,
        empty,
        lambda value: value["processFingerprint"]["identity"].__setitem__(
            "executableProfileRelativePath", "../DuckStation"
        ),
        contains="executable",
    )
    rejects_snapshot(
        "wrong process executable hash",
        post0,
        empty,
        lambda value: value["processFingerprint"]["identity"].__setitem__(
            "executableSha256", "f" * 64
        ),
        contains="executable",
    )
    rejects_snapshot(
        "malformed launch nonce",
        post0,
        empty,
        lambda value: value["processFingerprint"]["identity"].__setitem__(
            "launchNonce", "not-a-nonce"
        ),
        contains="launch identity",
    )
    rejects_snapshot(
        "forged derived process instance ID",
        post0,
        empty,
        lambda value: value.__setitem__("processInstanceId", "dsproc-" + "f" * 64),
        contains="fingerprint-derived",
    )
    rejects_snapshot(
        "pre-session claims termination",
        pre1,
        post0,
        lambda value: value["processFingerprint"].__setitem__(
            "termination",
            {
                "observed": True,
                "capturedAtUtc": "1970-01-01T00:00:00.019Z",
                "exitCode": 0,
                "signal": None,
            },
        ),
        contains="claims termination",
    )
    for label, change in (
        (
            "post-session termination unobserved",
            lambda value: value["processFingerprint"]["termination"].__setitem__(
                "observed", False
            ),
        ),
        (
            "post-session nonzero exit",
            lambda value: value["processFingerprint"]["termination"].__setitem__(
                "exitCode", 1
            ),
        ),
        (
            "post-session signal termination",
            lambda value: value["processFingerprint"]["termination"].__setitem__(
                "signal", 9
            ),
        ),
    ):
        rejects_snapshot(label, post0, empty, change, contains="clean exit")
    rejects_snapshot(
        "termination before process start",
        post0,
        empty,
        lambda value: value["processFingerprint"]["termination"].__setitem__(
            "capturedAtUtc", "1970-01-01T00:00:00.000Z"
        ),
        contains="chronology",
    )
    rejects_snapshot(
        "termination after card snapshot",
        post0,
        empty,
        lambda value: value["processFingerprint"]["termination"].__setitem__(
            "capturedAtUtc", "1970-01-01T00:00:00.011Z"
        ),
        contains="chronology",
    )
    rejects_snapshot(
        "process starts at card snapshot",
        post0,
        empty,
        lambda value: value["processFingerprint"]["identity"].__setitem__(
            "processStartUtc", value["capturedAtUtc"]
        ),
        contains="not before",
    )

    parsed = capture._parse_utc_milliseconds(
        "2024-02-29T23:59:59.999Z", "valid leap UTC"
    )
    checks.require(parsed.year == 2024 and parsed.day == 29, "valid leap UTC refused")
    for invalid in (
        "1970-02-30T00:00:00.000Z",
        "2023-02-29T00:00:00.000Z",
        "1970-13-01T00:00:00.000Z",
        "1970-01-01T24:00:00.000Z",
    ):
        checks.refuses(
            f"nonexistent UTC instant {invalid}",
            lambda invalid=invalid: capture._parse_utc_milliseconds(
                invalid, "adversarial UTC"
            ),
            contains="real UTC",
        )
    rejects_snapshot(
        "nonexistent card snapshot UTC",
        post0,
        empty,
        lambda value: value.__setitem__(
            "capturedAtUtc", "1970-02-30T00:00:00.010Z"
        ),
        contains="real UTC",
    )
    rejects_snapshot(
        "nonexistent process start UTC",
        post0,
        empty,
        lambda value: value["processFingerprint"]["identity"].__setitem__(
            "processStartUtc", "1970-02-30T00:00:00.001Z"
        ),
        contains="real UTC",
    )
    rejects_snapshot(
        "nonexistent termination UTC",
        post0,
        empty,
        lambda value: value["processFingerprint"]["termination"].__setitem__(
            "capturedAtUtc", "1970-02-30T00:00:00.009Z"
        ),
        contains="real UTC",
    )

    def relink(values: list[dict[str, object]]) -> None:
        for index in range(1, len(values)):
            values[index]["priorSnapshotSha256"] = capture.card_snapshot_sha256(
                values[index - 1]
            )

    def rejects_chain(
        label: str,
        change: Callable[[list[dict[str, object]]], None],
    ) -> None:
        changed = _clone(snapshots)
        change(changed)
        relink(changed)
        checks.refuses(label, lambda: capture.validate_card_chain(changed))

    def change_session_post_pid(values: list[dict[str, object]]) -> None:
        post = values[3]
        post["processFingerprint"]["identity"]["pid"] += 1
        post["processInstanceId"] = capture.process_instance_id_from_fingerprint(
            post["processFingerprint"]
        )

    rejects_chain("within-session process PID mismatch", change_session_post_pid)

    def change_session_post_start(values: list[dict[str, object]]) -> None:
        post = values[3]
        post["processFingerprint"]["identity"]["processStartUtc"] = (
            "1970-01-01T00:00:00.012Z"
        )
        post["processInstanceId"] = capture.process_instance_id_from_fingerprint(
            post["processFingerprint"]
        )

    rejects_chain("within-session process start mismatch", change_session_post_start)

    def start_before_prior_exit(
        values: list[dict[str, object]],
        pre_index: int,
        post_index: int,
        start: str,
    ) -> None:
        for index in (pre_index, post_index):
            fingerprint = values[index]["processFingerprint"]
            fingerprint["identity"]["processStartUtc"] = start
            values[index]["processInstanceId"] = (
                capture.process_instance_id_from_fingerprint(fingerprint)
            )

    rejects_chain(
        "session1 starts before session0 clean termination",
        lambda values: start_before_prior_exit(values, 2, 3, "1970-01-01T00:00:00.008Z"),
    )
    rejects_chain(
        "session2 starts before session1 clean termination",
        lambda values: start_before_prior_exit(values, 4, 5, "1970-01-01T00:00:00.028Z"),
    )

    def reuse_pid_and_start_with_new_nonces(values: list[dict[str, object]]) -> None:
        common_pid = values[1]["processFingerprint"]["identity"]["pid"]
        common_start = values[1]["processFingerprint"]["identity"][
            "processStartUtc"
        ]
        for index in (1, 2, 3, 4, 5):
            fingerprint = values[index]["processFingerprint"]
            fingerprint["identity"]["pid"] = common_pid
            fingerprint["identity"]["processStartUtc"] = common_start
            values[index]["processInstanceId"] = (
                capture.process_instance_id_from_fingerprint(fingerprint)
            )

    rejects_chain(
        "cross-session PID/start reuse hidden by launch nonce",
        reuse_pid_and_start_with_new_nonces,
    )

    def duplicate_timestamp(values: list[dict[str, object]]) -> None:
        values[4]["capturedAtUtc"] = values[3]["capturedAtUtc"]

    rejects_chain("non-increasing semantic card chronology", duplicate_timestamp)


def _invoke_cli(arguments: Sequence[str]) -> tuple[int, str, str]:
    stdout = io.StringIO()
    stderr = io.StringIO()
    with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
        code = capture.main(tuple(arguments))
    return code, stdout.getvalue(), stderr.getvalue()


def _test_simulation_three_session_engine(checks: Checks) -> None:
    """Drive every pure-engine boundary without a process or production write."""

    required_api = (
        "ExactEngineRequest",
        "ExactSimulationPreflight",
        "ExactEvidenceReceipt",
        "ExactResumeState",
        "ExactEngineResult",
        "ExactRuntimeBackend",
        "ExactRunStore",
        "ExactThreeSessionEngine",
        "exact_session_specs",
        "exact_engine_fault_points",
    )
    checks.require(
        all(hasattr(capture, name) for name in required_api),
        "simulation-only engine API is incomplete",
    )
    specs = capture.exact_session_specs()
    checks.require(
        tuple(spec.session_id for spec in specs) == capture.SESSION_IDS
        and tuple(len(spec.checkpoints) for spec in specs) == (9, 4, 5),
        "simulation session projection changed",
    )
    checks.require(
        tuple(len(spec.required_evidence_roles) for spec in specs) == (20, 10, 9)
        and sum(len(spec.required_evidence_roles) for spec in specs) == 39,
        "simulation evidence-role union changed",
    )
    checks.require(
        capture.ENGINE_DERIVED_EVIDENCE_ROLES
        == {
            capture.SESSION_IDS[0]: ("session0-post-card",),
            capture.SESSION_IDS[1]: (
                "session1-pre-card",
                "distinct-process-fingerprints",
            ),
            capture.SESSION_IDS[2]: (),
        },
        "engine-derived evidence role boundary changed",
    )
    original_derived_roles = capture.ENGINE_DERIVED_EVIDENCE_ROLES
    capture.ENGINE_DERIVED_EVIDENCE_ROLES = {
        **dict(original_derived_roles),
        capture.SESSION_IDS[2]: (
            "town-square-terrain-visible-frame",
        ),
    }
    try:
        checks.refuses(
            "forged engine-derived role boundary",
            capture.exact_session_specs,
            contains="evidence role/checkpoint",
        )
    finally:
        capture.ENGINE_DERIVED_EVIDENCE_ROLES = original_derived_roles

    # One happy path uses the real hardened lease with custom temporary paths.
    with tempfile.TemporaryDirectory(prefix="id65-exact-engine-") as temporary:
        root = pathlib.Path(temporary).resolve(strict=True)
        os.chmod(root, 0o700)
        (root / "authority").mkdir(mode=0o700)
        lease_state = LeaseState()
        backend = FakeExactRuntimeBackend(lease_state)
        store = RecordingExactRunStore(lease_state)
        hook_trace: list[str] = []
        engine = capture.ExactThreeSessionEngine(
            backend,
            store,
            lambda run_id: ActualLeaseProbe(lease_state, root),
            hook_trace.append,
        )
        result = engine.run(capture.ExactEngineRequest(
            capture.OFFLINE_FIXTURE_RUN_ID
        ))
        checks.require(
            result.simulation_only is True
            and result.runtime_observed is False
            and result.production_capture_enabled is False
            and result.evidence_accepted is False
            and result.create_bin_authorized is False
            and result.release_authorized is False
            and result.publishable is False,
            "simulation result overstated runtime/evidence/release acceptance",
        )
        capture.validate_run_bundle(_clone(result.bundle))
        checks.require(True, "simulation engine produced an invalid exact bundle")
        checks.require(
            len(result.bundle["journals"]) == 3
            and len(result.bundle["cardSnapshots"]) == 6
            and len(result.evidence_receipts) == 39,
            "simulation engine output counts changed",
        )
        bound_checks = [
            event["payload"]["checkId"]
            for journal in result.bundle["journals"]
            for event in journal["events"]
            if event["eventType"] == "check-bound"
        ]
        checks.require(
            len(bound_checks) == len(set(bound_checks)) == 17
            and set(bound_checks) == set(capture.EVENTUAL_CHECK_IDS),
            "simulation engine did not bind 17 checks exactly once",
        )
        checks.require(
            sum(event[0] == "begin" for event in backend.events) == 3
            and sum(event[0] == "checkpoint" for event in backend.events) == 18
            and sum(event[0] == "finish" for event in backend.events) == 3
            and backend.abort_count == 0,
            "simulation backend trace is not 3 sessions / 18 checkpoints",
        )
        checks.require(
            hook_trace == list(capture.exact_engine_fault_points()),
            "happy engine trace skipped or reordered a failure boundary",
        )
        checks.require(
            lease_state.events[0] == ("actual-authority-lease-enter",)
            and store.events[0] == ("bind-lease",)
            and lease_state.events[-1] == ("actual-authority-lease-exit",)
            and not lease_state.held,
            "engine store access escaped the entered authority-first lease",
        )
        checks.require(
            (root / "staging").is_dir()
            and not any(root.glob("**/*.mcd"))
            and not any(root.glob("**/*evidence*.json")),
            "simulation engine wrote card/evidence artifacts instead of receipts",
        )
        happy_commit_labels = tuple(store.commit_labels)
        checks.require(
            len(happy_commit_labels) > len(capture.exact_engine_fault_points())
            and happy_commit_labels[-1] == "run-bundle",
            "fake store did not observe every exact durable commit",
        )

        # Completed-run resume is an idempotent readback, not a replay.
        resume_backend = FakeExactRuntimeBackend(lease_state)
        resumed = capture.ExactThreeSessionEngine(
            resume_backend,
            store,
            lambda run_id: ActualLeaseProbe(lease_state, root),
        ).run(capture.ExactEngineRequest(
            capture.OFFLINE_FIXTURE_RUN_ID,
            resume=True,
        ))
        checks.require(
            resumed.resumed_session_count == 3
            and resumed.bundle == result.bundle
            and not any(event[0] == "begin" for event in resume_backend.events),
            "completed exact resume replayed a session or changed the bundle",
        )
        with ActualLeaseProbe(lease_state, root) as lease:
            store.bind_lease(lease)
            repeated_origin = store.begin_run(result.bundle["origin"])
            repeated_card = store.commit_card_snapshot(
                result.bundle["cardSnapshots"][0]
            )
            repeated_bundle = store.commit_run_bundle(result.bundle)
            checks.require(
                repeated_origin == result.bundle["origin"]
                and repeated_card == result.bundle["cardSnapshots"][0]
                and repeated_bundle == result.bundle,
                "same-byte durable retry was not idempotent",
            )
            conflicting_card = _clone(result.bundle["cardSnapshots"][0])
            conflicting_card["capturedAtUtc"] = "1970-01-01T00:00:00.001Z"
            checks.refuses(
                "different bytes for an existing snapshot identity",
                lambda: store.commit_card_snapshot(conflicting_card),
                contains="identity conflict",
            )

    # Invalid request/preflight claims must fail before any simulated session.
    lease_state = LeaseState()
    backend = FakeExactRuntimeBackend(lease_state)
    store = RecordingExactRunStore(lease_state)
    engine = capture.ExactThreeSessionEngine(
        backend, store, lambda run_id: GuardLease(lease_state)
    )
    checks.refuses(
        "simulation-only false request",
        lambda: engine.run(capture.ExactEngineRequest(
            capture.OFFLINE_FIXTURE_RUN_ID,
            simulation_only=False,
        )),
        contains="simulation request",
    )
    checks.require(
        not backend.events and store.origin is None,
        "invalid simulation request reached an adapter or store",
    )

    class OverstatingBackend(FakeExactRuntimeBackend):
        def preflight(
            self,
            request: capture.ExactEngineRequest,
        ) -> capture.ExactSimulationPreflight:
            value = super().preflight(request)
            return capture.dataclasses.replace(value, runtime_observed=True)

    lease_state = LeaseState()
    backend = OverstatingBackend(lease_state)
    store = RecordingExactRunStore(lease_state)
    checks.refuses(
        "backend runtime claim",
        lambda: capture.ExactThreeSessionEngine(
            backend, store, lambda run_id: GuardLease(lease_state)
        ).run(capture.ExactEngineRequest(capture.OFFLINE_FIXTURE_RUN_ID)),
        contains="overstated",
    )
    checks.require(
        store.origin is None,
        "overstating backend reached run-origin persistence",
    )

    lease_state = LeaseState()
    backend = FakeExactRuntimeBackend(
        lease_state, begin_mode="activate-then-raise"
    )
    store = RecordingExactRunStore(lease_state)
    try:
        capture.ExactThreeSessionEngine(
            backend,
            store,
            lambda run_id: GuardLease(lease_state),
        ).run(capture.ExactEngineRequest(capture.OFFLINE_FIXTURE_RUN_ID))
    except InjectedEngineFailure as exc:
        if str(exc) != "partial begin activation":
            raise
    else:
        raise AssertionError("partial begin activation did not fail")
    checks.require(
        backend.pending_abort_count == 1
        and backend.abort_count == 0
        and not any(bool(handle["active"]) for handle in backend.handles)
        and backend.events[-2][0] == "abort-pending"
        and backend.events[-1] == ("quiescent",)
        and store.quarantined,
        "partial begin activation escaped pending abort/quiescence/quarantine",
    )

    # Evidence and snapshot mutations fail closed at their narrowest boundary.
    for mode, fragment in (
        ("missing", "evidence-role coverage"),
        ("foreign-role", "receipt identity"),
        ("wrong-checkpoint", "receipt identity"),
        ("duplicate", "committed twice"),
    ):
        lease_state = LeaseState()
        backend = FakeExactRuntimeBackend(lease_state, receipt_mode=mode)
        store = RecordingExactRunStore(lease_state)
        checks.refuses(
            f"simulation receipt adversary {mode}",
            lambda backend=backend, store=store, lease_state=lease_state: (
                capture.ExactThreeSessionEngine(
                    backend,
                    store,
                    lambda run_id: GuardLease(lease_state),
                ).run(capture.ExactEngineRequest(capture.OFFLINE_FIXTURE_RUN_ID))
            ),
            contains=fragment,
        )
        checks.require(
            store.quarantined
            and not any(bool(handle["active"]) for handle in backend.handles),
            f"receipt adversary {mode} left an unsafe resumable session",
        )

    for mode in (
        "foreign-session",
        "changed-adjacent-card",
        "unclean-exit",
        "reused-process",
    ):
        lease_state = LeaseState()
        backend = FakeExactRuntimeBackend(lease_state, snapshot_mode=mode)
        store = RecordingExactRunStore(lease_state)
        checks.refuses(
            f"simulation snapshot adversary {mode}",
            lambda backend=backend, store=store, lease_state=lease_state: (
                capture.ExactThreeSessionEngine(
                    backend,
                    store,
                    lambda run_id: GuardLease(lease_state),
                ).run(capture.ExactEngineRequest(capture.OFFLINE_FIXTURE_RUN_ID))
            ),
        )
        checks.require(
            not any(bool(handle["active"]) for handle in backend.handles)
            and (store.quarantined or len(store.journals) == 3),
            f"snapshot adversary {mode} left unsafe runtime/store state",
        )

    # Every declared post-boundary hook must stop forward runtime progress.
    fault_points = capture.exact_engine_fault_points()
    checks.require(
        len(fault_points) == len(set(fault_points)) and len(fault_points) > 60,
        "engine failure boundary registry is incomplete or duplicated",
    )
    for target in fault_points:
        lease_state = LeaseState()
        backend = FakeExactRuntimeBackend(lease_state)
        store = RecordingExactRunStore(lease_state)
        backend_count_at_cut: list[int] = []

        def inject(boundary: str, *, target: str = target) -> None:
            if boundary == target:
                backend_count_at_cut.append(len(backend.events))
                raise InjectedEngineFailure(boundary)

        try:
            capture.ExactThreeSessionEngine(
                backend,
                store,
                lambda run_id: GuardLease(lease_state),
                inject,
            ).run(capture.ExactEngineRequest(capture.OFFLINE_FIXTURE_RUN_ID))
        except InjectedEngineFailure as exc:
            if str(exc) != target:
                raise AssertionError(
                    f"fault hook raised at wrong boundary: {target}: {exc}"
                ) from exc
        else:
            raise AssertionError(f"fault hook did not fire: {target}")
        later_runtime = backend.events[backend_count_at_cut[0]:]
        checks.require(
            all(
                event[0] in {"abort", "abort-pending", "quiescent"}
                for event in later_runtime
            )
            and len(later_runtime) <= 2
            and (
                not later_runtime or later_runtime[-1][0] == "quiescent"
            )
            and not any(bool(handle["active"]) for handle in backend.handles)
            and (store.active is None or store.quarantined),
            f"fault {target} allowed forward runtime or unsafe active state",
        )

    # Only exact sealed-session commits are resumable; no checkpoint is replayed.
    for completed_ordinal in (0, 1):
        target = (
            "after-session-logical-commit:"
            + capture.SESSION_IDS[completed_ordinal]
        )
        lease_state = LeaseState()
        first_backend = FakeExactRuntimeBackend(lease_state)
        store = RecordingExactRunStore(lease_state)

        def stop_after_seal(boundary: str, *, target: str = target) -> None:
            if boundary == target:
                raise InjectedEngineFailure(boundary)

        try:
            capture.ExactThreeSessionEngine(
                first_backend,
                store,
                lambda run_id: GuardLease(lease_state),
                stop_after_seal,
            ).run(capture.ExactEngineRequest(capture.OFFLINE_FIXTURE_RUN_ID))
        except InjectedEngineFailure:
            pass
        else:
            raise AssertionError("sealed-prefix injection did not fire")
        checks.require(
            len(store.journals) == completed_ordinal + 1
            and store.active is None
            and not store.quarantined,
            "logical session commit was not a safe sealed prefix",
        )
        resumed_backend = FakeExactRuntimeBackend(lease_state)
        resumed = capture.ExactThreeSessionEngine(
            resumed_backend,
            store,
            lambda run_id: GuardLease(lease_state),
        ).run(capture.ExactEngineRequest(
            capture.OFFLINE_FIXTURE_RUN_ID,
            resume=True,
        ))
        replayed_sessions = {
            event[1] for event in resumed_backend.events if event[0] == "checkpoint"
        }
        checks.require(
            resumed.resumed_session_count == completed_ordinal + 1
            and replayed_sessions.isdisjoint(
                capture.SESSION_IDS[:completed_ordinal + 1]
            ),
            "sealed-prefix resume replayed completed checkpoints",
        )

    for field, replacement in (
        ("checkpoint_id", "face-texture-complete"),
        ("sha256", "f" * 64),
        ("byte_length", 99_999),
        ("captured_at_utc", "1970-01-01T00:00:00.006Z"),
    ):
        lease_state = LeaseState()
        first_backend = FakeExactRuntimeBackend(lease_state)
        store = RecordingExactRunStore(lease_state)
        target = (
            "after-session-logical-commit:"
            + capture.SESSION_IDS[0]
        )

        def stop_receipt_prefix(boundary: str, *, target: str = target) -> None:
            if boundary == target:
                raise InjectedEngineFailure(boundary)

        try:
            capture.ExactThreeSessionEngine(
                first_backend,
                store,
                lambda run_id: GuardLease(lease_state),
                stop_receipt_prefix,
            ).run(capture.ExactEngineRequest(capture.OFFLINE_FIXTURE_RUN_ID))
        except InjectedEngineFailure:
            pass
        else:
            raise AssertionError("receipt-prefix injection did not fire")
        store.receipts[0] = capture.dataclasses.replace(
            store.receipts[0], **{field: replacement}
        )
        resume_backend = FakeExactRuntimeBackend(lease_state)
        checks.refuses(
            f"resume substituted sealed receipt {field}",
            lambda resume_backend=resume_backend, store=store, lease_state=lease_state: (
                capture.ExactThreeSessionEngine(
                    resume_backend,
                    store,
                    lambda run_id: GuardLease(lease_state),
                ).run(capture.ExactEngineRequest(
                    capture.OFFLINE_FIXTURE_RUN_ID,
                    resume=True,
                ))
            ),
        )
        checks.require(
            not any(event[0] == "begin" for event in resume_backend.events),
            f"substituted resume receipt {field} reached a new session",
        )

    lease_state = LeaseState()
    first_backend = FakeExactRuntimeBackend(lease_state)
    store = RecordingExactRunStore(lease_state)
    target = "after-session-logical-commit:" + capture.SESSION_IDS[0]

    def stop_ordered_receipt_prefix(boundary: str) -> None:
        if boundary == target:
            raise InjectedEngineFailure(boundary)

    try:
        capture.ExactThreeSessionEngine(
            first_backend,
            store,
            lambda run_id: GuardLease(lease_state),
            stop_ordered_receipt_prefix,
        ).run(capture.ExactEngineRequest(capture.OFFLINE_FIXTURE_RUN_ID))
    except InjectedEngineFailure:
        pass
    else:
        raise AssertionError("ordered receipt-prefix injection did not fire")
    store.receipts[:] = reversed(store.receipts)
    resume_backend = FakeExactRuntimeBackend(lease_state)
    checks.refuses(
        "resume reordered sealed receipt prefix",
        lambda: capture.ExactThreeSessionEngine(
            resume_backend,
            store,
            lambda run_id: GuardLease(lease_state),
        ).run(capture.ExactEngineRequest(
            capture.OFFLINE_FIXTURE_RUN_ID,
            resume=True,
        )),
    )
    checks.require(
        not any(event[0] == "begin" for event in resume_backend.events),
        "reordered resume receipt prefix reached a new session",
    )

    lease_state = LeaseState()
    backend = FakeExactRuntimeBackend(lease_state)
    store = RecordingExactRunStore(lease_state)
    partial_target = (
        f"after-checkpoint-commit:{capture.SESSION_IDS[0]}:"
        f"{capture.OPERATOR_CHECKPOINTS[capture.SESSION_IDS[0]][0]}"
    )

    def stop_partial(boundary: str) -> None:
        if boundary == partial_target:
            raise InjectedEngineFailure(boundary)

    try:
        capture.ExactThreeSessionEngine(
            backend,
            store,
            lambda run_id: GuardLease(lease_state),
            stop_partial,
        ).run(capture.ExactEngineRequest(capture.OFFLINE_FIXTURE_RUN_ID))
    except InjectedEngineFailure:
        pass
    else:
        raise AssertionError("partial-session injection did not fire")
    checks.require(
        store.quarantined and store.active is not None and backend.abort_count == 1,
        "partial session was exposed as resumable",
    )
    resume_backend = FakeExactRuntimeBackend(lease_state)
    checks.refuses(
        "automatic resume of quarantined partial session",
        lambda: capture.ExactThreeSessionEngine(
            resume_backend,
            store,
            lambda run_id: GuardLease(lease_state),
        ).run(capture.ExactEngineRequest(
            capture.OFFLINE_FIXTURE_RUN_ID,
            resume=True,
        )),
        contains="quarantined",
    )
    checks.require(
        not any(event[0] == "begin" for event in resume_backend.events),
        "partial resume began a runtime session",
    )

    # Store failures distinguish pre-commit quarantine from uncertain readback.
    for boundary, safe_sealed_count in (
        (f"before:seal:{capture.SESSION_IDS[0]}", None),
        (f"after:seal:{capture.SESSION_IDS[0]}", 1),
        ("before:bundle", 3),
        ("after:bundle", 3),
    ):
        lease_state = LeaseState()
        backend = FakeExactRuntimeBackend(lease_state)
        store = RecordingExactRunStore(
            lease_state, fail_boundary=boundary
        )
        try:
            capture.ExactThreeSessionEngine(
                backend,
                store,
                lambda run_id: GuardLease(lease_state),
            ).run(capture.ExactEngineRequest(capture.OFFLINE_FIXTURE_RUN_ID))
        except InjectedEngineFailure as exc:
            if str(exc) != boundary:
                raise
        else:
            raise AssertionError(f"store failure did not fire: {boundary}")
        if safe_sealed_count is None:
            checks.require(
                store.quarantined and store.active is not None,
                "pre-seal failure was not quarantined",
            )
            continue
        checks.require(
            len(store.journals) == safe_sealed_count
            and store.active is None
            and not store.quarantined,
            f"uncertain commit {boundary} did not expose an exact sealed prefix",
        )
        store.fail_boundary = None
        resumed_backend = FakeExactRuntimeBackend(lease_state)
        resumed = capture.ExactThreeSessionEngine(
            resumed_backend,
            store,
            lambda run_id: GuardLease(lease_state),
        ).run(capture.ExactEngineRequest(
            capture.OFFLINE_FIXTURE_RUN_ID,
            resume=True,
        ))
        checks.require(
            resumed.resumed_session_count == safe_sealed_count,
            f"uncertain commit {boundary} resumed from the wrong prefix",
        )

    for phase in ("before", "after"):
        for commit_index, commit_label in enumerate(happy_commit_labels):
            lease_state = LeaseState()
            backend = FakeExactRuntimeBackend(lease_state)
            store = RecordingExactRunStore(
                lease_state,
                fail_commit_cut=(phase, commit_index),
            )
            expected_failure = (
                f"{phase}-commit:{commit_index}:{commit_label}"
            )
            try:
                capture.ExactThreeSessionEngine(
                    backend,
                    store,
                    lambda run_id: GuardLease(lease_state),
                ).run(capture.ExactEngineRequest(
                    capture.OFFLINE_FIXTURE_RUN_ID
                ))
            except InjectedEngineFailure as exc:
                if str(exc) != expected_failure:
                    raise AssertionError(
                        "durable cut fired at a different commit: "
                        f"{expected_failure}: {exc}"
                    ) from exc
            else:
                raise AssertionError(
                    f"durable {phase} cut did not fire: {commit_index}:"
                    f"{commit_label}"
                )
            for journal in store.journals:
                capture.validate_journal(_clone(journal))
            if store.bundle is not None:
                capture.validate_run_bundle(_clone(store.bundle))
            checks.require(
                not any(bool(handle["active"]) for handle in backend.handles)
                and (store.active is None or store.quarantined),
                f"durable {phase} cut left unsafe state at {commit_label}",
            )

    lease_state = LeaseState()
    backend = FakeExactRuntimeBackend(lease_state)
    store = RecordingExactRunStore(lease_state, corrupt_readback="origin")
    checks.refuses(
        "corrupt durable origin readback",
        lambda: capture.ExactThreeSessionEngine(
            backend,
            store,
            lambda run_id: GuardLease(lease_state),
        ).run(capture.ExactEngineRequest(capture.OFFLINE_FIXTURE_RUN_ID)),
        contains="readback",
    )
    checks.require(
        not any(event[0] == "begin" for event in backend.events),
        "corrupt run-origin readback reached a simulated session",
    )

    prefix_bundle = capture.build_offline_fixture_bundle()
    valid_prefix = _clone(prefix_bundle["cardSnapshots"][:4])
    capture._validate_card_chain_prefix(
        valid_prefix, capture.OFFLINE_FIXTURE_RUN_ID
    )
    checks.require(True, "valid two-session card prefix was refused")
    changed_process = _clone(valid_prefix)
    changed_process[3]["processFingerprint"]["identity"]["pid"] += 100
    changed_process[3]["processInstanceId"] = (
        capture.process_instance_id_from_fingerprint(
            changed_process[3]["processFingerprint"]
        )
    )
    checks.refuses(
        "resume prefix within-session process substitution",
        lambda: capture._validate_card_chain_prefix(
            changed_process, capture.OFFLINE_FIXTURE_RUN_ID
        ),
        contains="within-session",
    )
    early_restart = _clone(valid_prefix)
    for index in (2, 3):
        fingerprint = early_restart[index]["processFingerprint"]
        fingerprint["identity"]["processStartUtc"] = (
            "1970-01-01T00:00:00.008Z"
        )
        early_restart[index]["processInstanceId"] = (
            capture.process_instance_id_from_fingerprint(fingerprint)
        )
    early_restart[3]["priorSnapshotSha256"] = capture.card_snapshot_sha256(
        early_restart[2]
    )
    checks.refuses(
        "resume prefix restart before prior termination",
        lambda: capture._validate_card_chain_prefix(
            early_restart, capture.OFFLINE_FIXTURE_RUN_ID
        ),
        contains="prior clean termination",
    )

    class NonQuiescentBackend(FakeExactRuntimeBackend):
        def assert_quiescent(self) -> None:
            super().assert_quiescent()
            raise capture.ExactCaptureRefusal(
                "simulated extra runtime child is not quiescent"
            )

    lease_state = LeaseState()
    backend = NonQuiescentBackend(lease_state)
    store = RecordingExactRunStore(lease_state)
    checks.refuses(
        "non-quiescent backend at completion",
        lambda: capture.ExactThreeSessionEngine(
            backend,
            store,
            lambda run_id: GuardLease(lease_state),
        ).run(capture.ExactEngineRequest(capture.OFFLINE_FIXTURE_RUN_ID)),
        contains="not quiescent",
    )
    checks.require(
        len(store.journals) == 3 and store.bundle is None,
        "non-quiescent backend reached final bundle acceptance",
    )

    source = COORDINATOR_PATH.read_text(encoding="utf-8")
    syntax = ast.parse(source, filename=os.fspath(COORDINATOR_PATH))
    forbidden_names = {
        "CaptureCoordinator",
        "CoordinatorLease",
        "AnchoredStaging",
        "SourceGuard",
        "JournalWriter",
        "RealMacBoundary",
    }
    operational_names = {
        node.id for node in ast.walk(syntax) if isinstance(node, ast.Name)
    } | {
        node.attr for node in ast.walk(syntax) if isinstance(node, ast.Attribute)
    }
    checks.require(
        operational_names.isdisjoint(forbidden_names),
        "simulation engine references a forbidden frozen high-level primitive",
    )


def _test_status_music_and_live_refusal(checks: Checks) -> None:
    status = capture._static_status()
    checks.require(status["sessionIds"] == list(capture.SESSION_IDS), "status session drift")
    checks.require(status["eventualCheckCount"] == 17, "status check count drift")
    checks.require(status["exactCaptureEngineImplemented"] is False, "engine overstated")
    checks.require(status["productionCaptureEnabled"] is False, "capture enabled early")
    checks.require(status["duckStationLaunched"] is False, "launch overstated")
    checks.require(status["runtimeObserved"] is False, "runtime observation overstated")
    checks.require(status["runtimeProven"] is False, "runtime proof overstated")
    checks.require(status["evidenceAccepted"] is False, "evidence overstated")
    checks.require(status["createBinAuthorized"] is False, "Create BIN authorized")
    checks.require(status["releaseAuthorized"] is False, "release authorized")
    checks.require(status["publishable"] is False, "publishable overstated")
    checks.require(
        status["profilePinsVerified"] is False
        and status["saveProfilePreflightRun"] is False,
        "status overstated profile verification or preflight",
    )
    checks.require(
        status["evidenceRoleArtifactsValidated"] is False,
        "status overstated evidence-role artifact validation",
    )
    checks.require(
        status["leaseSymlinkParentRaceHardeningImplemented"] is True
        and status["leaseFdRelativeNoFollowTraversalImplemented"] is True
        and status["leasePostFlockIdentityRevalidationImplemented"] is True
        and status["productionLeaseApiEnabled"] is False
        and status["lockedConsoleLiveProbeImplemented"] is False,
        "status misstated lease hardening, production lease, or console probe",
    )
    checks.require(
        status["structuredProcessFingerprintSchemaImplemented"] is True
        and status["processInstanceIdFingerprintDerived"] is True
        and status["cleanExitCardChainSemanticsImplemented"] is True
        and status["semanticUtcMillisecondsValidationImplemented"] is True
        and status["runtimeProcessFingerprintObserved"] is False,
        "status misstated process-fingerprint or UTC acceptance",
    )
    checks.require(
        status["evidenceRoleSessionOwnershipImplemented"] is True
        and status["candidateJumpLandingRoleCoverageDeclared"] is True
        and status[
            "retailTownSquareTerrainCollisionMobySaveExitCoverageDeclared"
        ] is True
        and status[
            "retailTownSquareMusic60500msLiveAttestationDeclared"
        ] is True,
        "status lost exact evidence-role coverage declarations",
    )
    checks.require(
        status["sourceDeclarationClosureSha256"]
        == capture.SOURCE_DECLARATION_CLOSURE_SHA256
        and status["workflowDeclarationClosureSha256"]
        == capture.WORKFLOW_DECLARATION_CLOSURE_SHA256
        and status["declarationClosureScope"]
        == "declared-plan-schema-profile-and-tool-pins-only"
        and status["coordinatorBytePinExternallyValidated"] is False,
        "status overstated declaration closure scope",
    )
    checks.require(
        status["musicAudioTrackValidated"] is False
        and status["musicRequiresLiveOperatorAttestation"] is True,
        "music limitation was weakened",
    )
    music = next(
        binding
        for binding in capture.CHECK_BINDINGS
        if binding.check_id == "candidate.music-long-play-stable"
    )
    music_role_ids = tuple(role.role_id for role in music.evidence_roles)
    checks.require(
        "music-observation-120500ms" in music_role_ids
        and "live-music-operator-attestation" in music_role_ids
        and {role.session_id for role in music.evidence_roles}
        == {capture.SESSION_IDS[0]},
        "music stability check lost duration/operator attestation",
    )

    code, stdout, stderr = _invoke_cli(("status",))
    parsed = json.loads(stdout)
    checks.require(code == 0 and not stderr, "status CLI failed")
    checks.require(parsed == status, "status CLI payload differs from static status")

    locked_marker = os.environ.get("CGSSessionScreenIsLocked")
    os.environ["CGSSessionScreenIsLocked"] = "Yes"
    try:
        live_operations = (
            ("preflight",),
            ("capture-run",),
            ("resume-run", "exact-v1-fixture"),
            ("verify-staging", "exact-v1-fixture"),
            ("quarantine-abort", "exact-v1-fixture"),
        )
        for operation in live_operations:
            before = _snapshot_production()
            code, stdout, stderr = _invoke_cli(operation)
            after = _snapshot_production()
            checks.require(
                code == 2
                and not stdout
                and capture.EXACT_CAPTURE_ENGINE_BLOCKER_ID in stderr
                and capture.EXACT_CAPTURE_ENGINE_REQUIRED_GATE in stderr,
                f"live CLI did not fail closed: {operation!r}",
            )
            checks.require(
                after == before,
                f"live CLI mutated a persistent boundary: {operation!r}",
            )
    finally:
        if locked_marker is None:
            os.environ.pop("CGSSessionScreenIsLocked", None)
        else:
            os.environ["CGSSessionScreenIsLocked"] = locked_marker


def _test_required_run_and_journal_surface(checks: Checks) -> None:
    """Exercise the complete fixture/journal API required by this contract."""

    required = (
        "build_offline_fixture_bundle",
        "validate_card_chain",
        "validate_journal",
        "validate_run_bundle",
        "card_snapshot_sha256",
    )
    missing = tuple(name for name in required if not callable(getattr(capture, name, None)))
    checks.require(
        not missing,
        "coordinator is missing required exact-run/journal APIs: " + ", ".join(missing),
    )

    bundle = capture.build_offline_fixture_bundle()
    capture.validate_run_bundle(_clone(bundle))
    checks.require(True, "complete offline run bundle refused")

    sessions = bundle["journals"]
    checks.require(
        tuple(item["sessionId"] for item in sessions) == capture.SESSION_IDS,
        "fixture run session order changed",
    )
    bound_checks = [
        event["payload"]["checkId"]
        for session in sessions
        for event in session["events"]
        if event["eventType"] == "check-bound"
    ]
    checks.require(
        set(bound_checks) == set(capture.EVENTUAL_CHECK_IDS)
        and len(bound_checks) == len(set(bound_checks)) == 17,
        "fixture does not bind all 17 checks exactly once",
    )

    card_chain = capture.validate_card_chain(_clone(bundle["cardSnapshots"]))
    checks.require(len(card_chain) == 6, "fixture card chain is not exact")
    checks.require(
        capture.card_snapshot_sha256(card_chain[1])
        == _canonical_sha256(card_chain[1]),
        "card snapshot closure is not canonical",
    )

    def rejects_bundle(label: str, change: Callable[[dict[str, object]], None]) -> None:
        changed = _clone(bundle)
        change(changed)
        checks.refuses(label, lambda: capture.validate_run_bundle(changed))

    rejects_bundle("reordered run sessions", lambda value: value["journals"].reverse())
    rejects_bundle("truncated run sessions", lambda value: value["journals"].pop())
    rejects_bundle(
        "duplicated run session",
        lambda value: value["journals"].append(_clone(value["journals"][-1])),
    )
    rejects_bundle(
        "extra card artifact",
        lambda value: value["cardSnapshots"].append(
            _clone(value["cardSnapshots"][-1])
        ),
    )
    rejects_bundle(
        "reordered run evidence receipts",
        lambda value: value["evidenceReceipts"].reverse(),
    )
    rejects_bundle(
        "truncated run evidence receipts",
        lambda value: value["evidenceReceipts"].pop(),
    )
    rejects_bundle(
        "duplicated run evidence receipt",
        lambda value: value["evidenceReceipts"].append(
            _clone(value["evidenceReceipts"][-1])
        ),
    )
    rejects_bundle(
        "savestate artifact",
        lambda value: value.__setitem__(
            "saveStateArtifacts",
            [{"role": "savestate", "relativePath": "savestates/forbidden.sav"}],
        ),
    )
    rejects_bundle(
        "host card seed",
        lambda value: value["origin"].__setitem__(
            "hostCardSeedSha256", capture.HISTORICAL_HOST_CARD_SHA256_FORBIDDEN
        ),
    )
    rejects_bundle(
        "origin falsely claims coordinator-byte closure",
        lambda value: value["origin"].__setitem__(
            "declarationClosuresBindCoordinatorBytes", True
        ),
    )
    rejects_bundle(
        "source declaration closure substitution",
        lambda value: value["origin"].__setitem__(
            "sourceDeclarationClosureSha256", "f" * 64
        ),
    )
    rejects_bundle(
        "workflow declaration closure substitution",
        lambda value: value["origin"].__setitem__(
            "workflowDeclarationClosureSha256", "f" * 64
        ),
    )

    def rehash_journal(journal: dict[str, object]) -> None:
        previous = capture.ZERO_SHA256
        for sequence, event in enumerate(journal["events"]):
            event["sequence"] = sequence
            event["previousEventSha256"] = previous
            material = {
                key: event[key]
                for key in (
                    "sequence",
                    "eventType",
                    "monotonicMicroseconds",
                    "previousEventSha256",
                    "payload",
                )
            }
            event["eventSha256"] = capture._sha256_canonical(material)
            previous = event["eventSha256"]
        journal["terminalEventSha256"] = previous

    def refresh_receipt_closure(
        value: dict[str, object],
        receipt_index: int,
    ) -> None:
        receipt = value["evidenceReceipts"][receipt_index]
        journal = next(
            item for item in value["journals"]
            if item["sessionId"] == receipt["sessionId"]
        )
        closure_event = next(
            event for event in journal["events"]
            if event["eventType"] == "evidence-receipts-bound"
        )
        closure_receipts = closure_event["payload"]["receipts"]
        closure_index = next(
            index for index, item in enumerate(closure_receipts)
            if item["roleId"] == receipt["roleId"]
        )
        closure_receipts[closure_index] = _clone(receipt)
        closure_event["payload"]["receiptCount"] = len(closure_receipts)
        closure_event["payload"]["receiptsSha256"] = (
            capture._sha256_canonical(closure_receipts)
        )
        sealed = journal["events"][-1]["payload"]
        sealed["evidenceReceiptCount"] = len(closure_receipts)
        sealed["evidenceReceiptsSha256"] = closure_event["payload"][
            "receiptsSha256"
        ]
        rehash_journal(journal)

    checks.require(
        len(bundle["evidenceReceipts"]) == 39
        and sum(
            event["eventType"] == "evidence-receipts-bound"
            for journal_item in bundle["journals"]
            for event in journal_item["events"]
        ) == 3,
        "fixture lacks the exact receipt/bound-session closure",
    )
    rejects_bundle(
        "valid receipt hash substituted outside sealed closure",
        lambda value: value["evidenceReceipts"][0].__setitem__(
            "sha256", "f" * 64
        ),
    )
    rejects_bundle(
        "valid receipt length substituted outside sealed closure",
        lambda value: value["evidenceReceipts"][0].__setitem__(
            "byteLength", value["evidenceReceipts"][0]["byteLength"] + 1
        ),
    )
    rejects_bundle(
        "valid in-process receipt time substituted outside sealed closure",
        lambda value: value["evidenceReceipts"][0].__setitem__(
            "capturedAtUtc", "1970-01-01T00:00:00.006Z"
        ),
    )

    def substitute_checkpoint_and_reclose(value: dict[str, object]) -> None:
        value["evidenceReceipts"][0]["checkpointId"] = (
            "face-texture-complete"
        )
        refresh_receipt_closure(value, 0)

    rejects_bundle(
        "semantically foreign valid checkpoint with recomputed closures",
        substitute_checkpoint_and_reclose,
    )

    def escape_process_interval_and_reclose(value: dict[str, object]) -> None:
        value["evidenceReceipts"][0]["capturedAtUtc"] = (
            "1970-01-01T00:00:00.000Z"
        )
        refresh_receipt_closure(value, 0)

    rejects_bundle(
        "receipt timestamp outside owning process with recomputed closures",
        escape_process_interval_and_reclose,
    )

    journal = bundle["journals"][0]
    capture.validate_journal(_clone(journal))
    checks.require(True, "valid append-only journal refused")

    def rejects_journal(
        label: str,
        change: Callable[[dict[str, object]], None],
        *,
        rehash: bool = False,
    ) -> None:
        changed = _clone(journal)
        change(changed)
        if rehash:
            rehash_journal(changed)
        checks.refuses(
            label,
            lambda: capture.validate_journal(changed),
        )

    rejects_journal("journal reorder", lambda value: value["events"].reverse())
    rejects_journal("journal truncation", lambda value: value["events"].pop())
    rejects_journal(
        "journal insertion",
        lambda value: value["events"].insert(1, _clone(value["events"][0])),
    )
    rejects_journal(
        "journal hash mutation",
        lambda value: value["events"][1].__setitem__("eventSha256", "0" * 64),
    )
    rejects_journal(
        "journal prior hash mutation",
        lambda value: value["events"][1].__setitem__(
            "previousEventSha256", "f" * 64
        ),
    )
    rejects_journal(
        "non-string journal event type",
        lambda value: value["events"][1].__setitem__("eventType", 7),
        rehash=True,
    )
    rejects_journal(
        "empty journal event type",
        lambda value: value["events"][1].__setitem__("eventType", ""),
        rehash=True,
    )
    rejects_journal(
        "non-mapping journal payload",
        lambda value: value["events"][0].__setitem__("payload", ["invalid"]),
        rehash=True,
    )
    rejects_journal(
        "interior duplicate session-open",
        lambda value: value["events"].insert(
            -1,
            {
                "sequence": 0,
                "eventType": "session-open",
                "monotonicMicroseconds": (
                    value["events"][-2]["monotonicMicroseconds"] + 1
                ),
                "previousEventSha256": capture.ZERO_SHA256,
                "payload": {"sessionId": value["sessionId"]},
                "eventSha256": capture.ZERO_SHA256,
            },
        ),
        rehash=True,
    )
    rejects_journal(
        "interior duplicate session-sealed",
        lambda value: value["events"].insert(
            -1,
            {
                "sequence": 0,
                "eventType": "session-sealed",
                "monotonicMicroseconds": (
                    value["events"][-2]["monotonicMicroseconds"] + 1
                ),
                "previousEventSha256": capture.ZERO_SHA256,
                "payload": {"fixtureOnly": True},
                "eventSha256": capture.ZERO_SHA256,
            },
        ),
        rehash=True,
    )

    writer = capture.AppendOnlyJournal(
        bundle["origin"]["runId"], capture.SESSION_IDS[0], 0
    )
    writer.append("session-open", {"sessionId": capture.SESSION_IDS[0]}, 1)
    checks.refuses(
        "journal append non-monotonic clock",
        lambda: writer.append("check-bound", {}, 1),
    )
    checks.refuses(
        "journal append non-string event",
        lambda: writer.append(7, {}, 2),
    )
    checks.refuses(
        "journal append non-mapping payload",
        lambda: writer.append("check-bound", [], 2),
    )
    writer.append("session-sealed", {"fixtureOnly": True}, 2)
    checks.refuses(
        "journal append after seal",
        lambda: writer.append("late-event", {}, 3),
    )

    def move_bound_check_to_wrong_session(value: dict[str, object]) -> None:
        source = value["journals"][0]
        destination = value["journals"][2]
        index = next(
            index
            for index, event in enumerate(source["events"])
            if event["eventType"] == "check-bound"
            and event["payload"]["checkId"] == "candidate.authored-terrain-visible"
        )
        moved = source["events"].pop(index)
        moved["monotonicMicroseconds"] = (
            destination["events"][-2]["monotonicMicroseconds"] + 1
        )
        destination["events"].insert(-1, moved)
        rehash_journal(source)
        rehash_journal(destination)

    rejects_bundle(
        "check-bound event in an unauthorized session",
        move_bound_check_to_wrong_session,
    )

    def swap_cross_session_evidence_role_owners(value: dict[str, object]) -> None:
        journal = value["journals"][1]
        event = next(
            event
            for event in journal["events"]
            if event["eventType"] == "check-bound"
            and event["payload"]["checkId"] == "candidate.save-persistence"
        )
        for role in event["payload"]["evidenceRoles"]:
            if role["roleId"] == "session0-post-card":
                role["sessionId"] = capture.SESSION_IDS[1]
            elif role["roleId"] == "session1-pre-card":
                role["sessionId"] = capture.SESSION_IDS[0]
        rehash_journal(journal)

    rejects_bundle(
        "cross-session evidence-role owner swap in journal",
        swap_cross_session_evidence_role_owners,
    )

    def mismatch_process_within_session(value: dict[str, object]) -> None:
        value["cardSnapshots"][3]["processInstanceId"] = "different-process"

    rejects_bundle(
        "session pre/post card process mismatch",
        mismatch_process_within_session,
    )

    def mutate_card_without_journal_rebind(value: dict[str, object]) -> None:
        snapshots = value["cardSnapshots"]
        for index in (3, 4):
            snapshots[index]["entries"][0]["sha256"] = "4" * 64
            snapshots[index]["entries"][0]["archiveSha256"] = "4" * 64
            snapshots[index]["sourceArchiveReadbackSha256"] = "4" * 64
        snapshots[4]["priorSnapshotSha256"] = capture.card_snapshot_sha256(
            snapshots[3]
        )
        snapshots[5]["priorSnapshotSha256"] = capture.card_snapshot_sha256(
            snapshots[4]
        )

    rejects_bundle(
        "card chain mutation without append-only journal rebind",
        mutate_card_without_journal_rebind,
    )

    def weak_snapshot_id(value: dict[str, object]) -> None:
        value["cardSnapshots"][2]["snapshotId"] = "x"

    rejects_bundle("weak card snapshot ID", weak_snapshot_id)

    def weak_timestamp(value: dict[str, object]) -> None:
        value["cardSnapshots"][2]["capturedAtUtc"] = "not-a-timestamp"

    rejects_bundle("weak card snapshot timestamp", weak_timestamp)

    def wrong_archive_phase(value: dict[str, object]) -> None:
        snapshot = value["cardSnapshots"][2]
        snapshot["entries"][0]["archiveArtifactId"] = (
            f"card.{capture.SESSION_IDS[2]}.post"
        )

    rejects_bundle("archive artifact phase/session mismatch", wrong_archive_phase)


def main() -> int:
    checks = Checks()
    initial_production = _snapshot_production()
    initial_exact_pyc = tuple(
        sorted(
            TEST_PATH.parent.glob(
                "__pycache__/*SpyroId65AuthoringAggregateExactV1RuntimeSession*.pyc"
            )
        )
    )
    try:
        root = capture.PRODUCTION_REPOSITORY_ROOT
        staging = os.fspath(root / capture.EXACT_STAGING_PREFIX)
        focused_evidence = os.fspath(
            root / capture.FOCUSED_EVIDENCE_RELATIVE_PATH
        )
        profile = os.fspath(root / capture.PROFILE_RELATIVE_PATH)
        memcards = os.fspath(root / capture.PROFILE_MEMCARDS_RELATIVE_PATH)
        card = os.fspath(
            root / capture.PROFILE_MEMCARDS_RELATIVE_PATH / capture.CARD_LEAF
        )
        checks.require(
            initial_production[staging] == ("absent",),
            "production exact staging already exists",
        )
        checks.require(
            initial_production[focused_evidence] == ("absent",),
            "focused production evidence already exists",
        )
        checks.require(
            initial_production[profile] != ("absent",),
            "pinned save-capable production profile is absent",
        )
        checks.require(
            initial_production[memcards] != ("absent",),
            "pinned production memcards directory is absent",
        )
        checks.require(
            initial_production[card] == ("absent",),
            "production save-profile card is not initially absent",
        )
        _test_exact_plan(checks)
        _test_profile_pin_declaration_guards(checks)
        _test_authority_order(checks)
        _test_card_schema_and_chain(checks)
        _test_simulation_three_session_engine(checks)
        _test_status_music_and_live_refusal(checks)
        _test_required_run_and_journal_surface(checks)
        checks.require(not PROCESS_EVENTS, f"process launch was attempted: {PROCESS_EVENTS!r}")
    finally:
        final_production = _snapshot_production()
        if final_production != initial_production:
            raise AssertionError("deep test changed a persistent production boundary")
        final_exact_pyc = tuple(
            sorted(
                TEST_PATH.parent.glob(
                    "__pycache__/*SpyroId65AuthoringAggregateExactV1RuntimeSession*.pyc"
                )
            )
        )
        if final_exact_pyc != initial_exact_pyc:
            raise AssertionError("deep test left an exact-capture bytecode artifact")

    coordinator_sha256 = _sha256_path(COORDINATOR_PATH)
    test_sha256 = _sha256_path(TEST_PATH)
    print(
        "ID65_AUTHORING_AGGREGATE_EXACT_V1_CAPTURE_DEEP_TEST_OK "
        f"checks={checks.count} coordinator={coordinator_sha256} test={test_sha256}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
