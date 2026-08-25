#!/usr/bin/env python3
"""Offline adversarial tests for the ID65 aggregate profile adapter.

The suite never builds or verifies the real DuckStation profile and never
launches an application.  Production aggregate metadata is opened read-only.
All tampering uses private temporary copies, and all publication-boundary
tests use an in-memory fake of the frozen builder.
"""

from __future__ import annotations

import ast
import contextlib
import dataclasses
import hashlib
import inspect
import io
import json
import os
import pathlib
import stat
import sys
import tempfile
import types
import unittest
from typing import Any
from unittest import mock


SCRIPT_PATH = pathlib.Path(os.path.abspath(__file__))
REPOSITORY_ROOT = SCRIPT_PATH.parent.parent
ADAPTER_PATH = SCRIPT_PATH.with_name(
    "Build-SpyroId65AuthoringAggregateV1Profile.py"
)
VERIFIER_PATH = SCRIPT_PATH.with_name(
    "Verify-SpyroId65AuthoringAggregateV1Profile.py"
)
EXPECTED_AGGREGATE_VERIFIER_SHA256 = (
    "ba1b461193c7a67fd8522c72fd9141e725ec3063791d44d2a82d96e0a01a344c"
)


class TestHarnessFailure(RuntimeError):
    """The test harness could not establish a stable adapter snapshot."""


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _stable_load_module(
    path: pathlib.Path,
    module_name: str,
) -> tuple[types.ModuleType, str, tuple[int, int, int, int, int, int, int, int]]:
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
    fd = os.open(path, flags)
    try:
        before = os.fstat(fd)
        chunks: list[bytes] = []
        while True:
            chunk = os.read(fd, 1024 * 1024)
            if not chunk:
                break
            chunks.append(chunk)
        after = os.fstat(fd)
    finally:
        os.close(fd)
    identity_fields = (
        "st_dev",
        "st_ino",
        "st_mode",
        "st_uid",
        "st_gid",
        "st_nlink",
        "st_size",
        "st_mtime_ns",
    )
    before_identity = tuple(getattr(before, name) for name in identity_fields)
    after_identity = tuple(getattr(after, name) for name in identity_fields)
    if before_identity != after_identity:
        raise TestHarnessFailure("aggregate adapter changed while being loaded.")
    source = b"".join(chunks)
    if len(source) != before.st_size:
        raise TestHarnessFailure("aggregate adapter produced an unstable read.")

    module = types.ModuleType(module_name)
    module.__file__ = os.fspath(path)
    module.__package__ = None
    sys.modules[module_name] = module
    try:
        exec(compile(source, os.fspath(path), "exec"), module.__dict__)
    except Exception:
        sys.modules.pop(module_name, None)
        raise
    return module, _sha256_bytes(source), before_identity


ADAPTER, ADAPTER_LOAD_SHA256, ADAPTER_LOAD_IDENTITY = _stable_load_module(
    ADAPTER_PATH,
    "_spyro_id65_authoring_aggregate_v1_adapter_under_test",
)
FROZEN_BUILDER = ADAPTER._load_frozen_builder(REPOSITORY_ROOT)
VERIFIER, VERIFIER_LOAD_SHA256, VERIFIER_LOAD_IDENTITY = _stable_load_module(
    VERIFIER_PATH,
    "_spyro_id65_authoring_aggregate_v1_verifier_under_test",
)
FROZEN_VERIFIER = VERIFIER._load_frozen_verifier(REPOSITORY_ROOT)
VERIFIER_BUILDER = VERIFIER._load_frozen_builder(
    FROZEN_VERIFIER,
    REPOSITORY_ROOT,
)
TEST_FILE_LOAD_SHA256 = _sha256_bytes(SCRIPT_PATH.read_bytes())


def _file_snapshot(path: pathlib.Path) -> tuple[object, ...]:
    before = os.lstat(path)
    raw = path.read_bytes()
    after = os.lstat(path)
    fields = (
        "st_dev",
        "st_ino",
        "st_mode",
        "st_uid",
        "st_gid",
        "st_nlink",
        "st_size",
        "st_mtime_ns",
        "st_ctime_ns",
    )
    before_values = tuple(getattr(before, name) for name in fields)
    after_values = tuple(getattr(after, name) for name in fields)
    if before_values != after_values:
        raise TestHarnessFailure(f"file changed while snapshotted: {path}")
    return before_values + (_sha256_bytes(raw),)


def _assert_adapter_still_loaded_snapshot() -> None:
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
    fd = os.open(ADAPTER_PATH, flags)
    try:
        current = os.fstat(fd)
        chunks: list[bytes] = []
        while True:
            chunk = os.read(fd, 1024 * 1024)
            if not chunk:
                break
            chunks.append(chunk)
    finally:
        os.close(fd)
    fields = (
        "st_dev",
        "st_ino",
        "st_mode",
        "st_uid",
        "st_gid",
        "st_nlink",
        "st_size",
        "st_mtime_ns",
    )
    if tuple(getattr(current, name) for name in fields) != ADAPTER_LOAD_IDENTITY:
        raise TestHarnessFailure("aggregate adapter identity changed during tests.")
    if _sha256_bytes(b"".join(chunks)) != ADAPTER_LOAD_SHA256:
        raise TestHarnessFailure("aggregate adapter bytes changed during tests.")


def _assert_source_still_loaded_snapshot(
    path: pathlib.Path,
    expected_identity: tuple[int, int, int, int, int, int, int, int],
    expected_sha256: str,
    label: str,
) -> None:
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
    fd = os.open(path, flags)
    try:
        current = os.fstat(fd)
        chunks: list[bytes] = []
        while True:
            chunk = os.read(fd, 1024 * 1024)
            if not chunk:
                break
            chunks.append(chunk)
    finally:
        os.close(fd)
    fields = (
        "st_dev",
        "st_ino",
        "st_mode",
        "st_uid",
        "st_gid",
        "st_nlink",
        "st_size",
        "st_mtime_ns",
    )
    if tuple(getattr(current, name) for name in fields) != expected_identity:
        raise TestHarnessFailure(f"{label} identity changed during tests.")
    if _sha256_bytes(b"".join(chunks)) != expected_sha256:
        raise TestHarnessFailure(f"{label} bytes changed during tests.")


def _metadata_identity(path: pathlib.Path) -> tuple[object, ...]:
    metadata = os.lstat(path)
    fields = (
        "st_dev",
        "st_ino",
        "st_mode",
        "st_uid",
        "st_gid",
        "st_nlink",
        "st_size",
        "st_mtime_ns",
        "st_ctime_ns",
    )
    values: tuple[object, ...] = tuple(getattr(metadata, name) for name in fields)
    if stat.S_ISLNK(metadata.st_mode):
        values += (os.readlink(path),)
    return values


def _tree_metadata_snapshot(root: pathlib.Path) -> tuple[tuple[object, ...], ...]:
    root = pathlib.Path(root)
    rows: list[tuple[object, ...]] = [(".",) + _metadata_identity(root)]
    pending: list[tuple[pathlib.Path, pathlib.PurePosixPath]] = [
        (root, pathlib.PurePosixPath("."))
    ]
    while pending:
        directory, relative = pending.pop()
        with os.scandir(directory) as entries:
            ordered = sorted(entries, key=lambda entry: os.fsencode(entry.name))
        for entry in ordered:
            path = pathlib.Path(entry.path)
            child_relative = (
                pathlib.PurePosixPath(entry.name)
                if relative == pathlib.PurePosixPath(".")
                else relative / entry.name
            )
            identity = _metadata_identity(path)
            rows.append((child_relative.as_posix(),) + identity)
            if stat.S_ISDIR(os.lstat(path).st_mode):
                pending.append((path, child_relative))
    return tuple(sorted(rows, key=lambda row: os.fsencode(str(row[0]))))


def _production_prelaunch_state_snapshot() -> dict[str, object]:
    destination, cue, binary, candidate_receipt, document = VERIFIER._aggregate_paths(
        REPOSITORY_ROOT
    )
    shared_lock = REPOSITORY_ROOT / VERIFIER.SHARED_PARENT_LOCK_RELATIVE_PATH
    state: dict[str, object] = {
        "profileTree": _tree_metadata_snapshot(destination),
        "sourceAppTree": _tree_metadata_snapshot(VERIFIER_BUILDER.SOURCE_APP_PATH),
        "cueMetadata": _metadata_identity(cue),
        "binaryMetadata": _metadata_identity(binary),
        "candidateReceiptMetadata": _metadata_identity(candidate_receipt),
        "documentMetadata": _metadata_identity(document),
        "biosMetadata": _metadata_identity(VERIFIER_BUILDER.BIOS_PATH),
        "verifierMetadata": _metadata_identity(VERIFIER_PATH),
        "frozenVerifierMetadata": _metadata_identity(
            REPOSITORY_ROOT / VERIFIER.FROZEN_VERIFIER_RELATIVE_PATH
        ),
        "frozenBuilderMetadata": _metadata_identity(
            REPOSITORY_ROOT / VERIFIER.FROZEN_BUILDER_RELATIVE_PATH
        ),
        "cueFile": _file_snapshot(cue),
        "candidateReceiptFile": _file_snapshot(candidate_receipt),
        "documentFile": _file_snapshot(document),
        "handoffReceiptFile": _file_snapshot(
            destination / FROZEN_VERIFIER.PRODUCTION_PINS.receipt_leaf
        ),
    }
    # flock(2) updates this coordination inode's ctime on macOS.  Its stable
    # structural identity is still covered, while ctime is intentionally not
    # conflated with mutation of the profile/candidate/document evidence.
    state["sharedLockStructuralMetadata"] = (
        _metadata_identity(shared_lock)[:7]
        if os.path.lexists(shared_lock)
        else None
    )
    return state


class MetadataCopies:
    """Private, exact-name copies used only for destructive refusal tests."""

    def __init__(self) -> None:
        local_root = REPOSITORY_ROOT / "_local"
        if not local_root.is_dir():
            raise TestHarnessFailure("expected repository-local scratch root is absent.")
        self._temporary = tempfile.TemporaryDirectory(
            prefix=".id65-aggregate-profile-tests-",
            dir=local_root,
        )
        self.root = pathlib.Path(self._temporary.name)
        self.root.chmod(0o700)
        _, _, _, production_receipt, production_document = ADAPTER._paths(
            REPOSITORY_ROOT
        )
        self.receipt = self.root / production_receipt.name
        self.document = self.root / production_document.name
        self.receipt.write_bytes(production_receipt.read_bytes())
        self.document.write_bytes(production_document.read_bytes())
        self.receipt.chmod(0o600)
        self.document.chmod(0o600)

    def __enter__(self) -> "MetadataCopies":
        return self

    def __exit__(self, exc_type: object, exc: object, traceback: object) -> None:
        self._temporary.cleanup()


class VerifierDescriptorFixture:
    """Tiny exact-name inputs for descriptor ownership and closure tests."""

    def __init__(self) -> None:
        self._temporary = tempfile.TemporaryDirectory(
            prefix=".id65-aggregate-verifier-descriptors-",
            dir=REPOSITORY_ROOT / "_local",
        )
        self.root = pathlib.Path(self._temporary.name)
        self.root.chmod(0o700)
        self.binary = self.root / f"{VERIFIER.CANDIDATE_STEM}.bin"
        self.cue = self.root / f"{VERIFIER.CANDIDATE_STEM}.cue"
        self.candidate_receipt = (
            self.root / f"{VERIFIER.CANDIDATE_STEM}.receipt.json"
        )
        self.document = self.root / VERIFIER.DOCUMENT_RELATIVE_PATH.name
        self.binary.write_bytes(b"tiny aggregate BIN fixture; never executed\n")
        self.cue.write_bytes(
            (
                f'FILE "{self.binary.name}" BINARY\n'
                "  TRACK 01 MODE2/2352\n"
                "    INDEX 01 00:00:00\n"
            ).encode("ascii")
        )
        receipt_payload = {
            "format": (
                "spyro-editor-id65-authoring-editor-aggregate-candidate-receipt"
            ),
            "schemaVersion": 1,
            "documentEnvelopeSha256": VERIFIER.EXPECTED_DOCUMENT_ENVELOPE_SHA256,
            "documentCanonicalSha256": (
                VERIFIER.EXPECTED_DOCUMENT_CANONICAL_SHA256
            ),
            "aggregatePlanSha256": VERIFIER.EXPECTED_AGGREGATE_PLAN_SHA256,
            "outputRow80Sha256": VERIFIER.EXPECTED_OUTPUT_ROW80_SHA256,
            "outputImageSha256": VERIFIER.EXPECTED_CANDIDATE_BIN_SHA256,
            "candidateCueSha256": VERIFIER.EXPECTED_CANDIDATE_CUE_SHA256,
            "receiptContentSha256": (
                VERIFIER.EXPECTED_CANDIDATE_RECEIPT_CONTENT_SHA256
            ),
            "runtimeCandidateAuthorized": False,
            "runtimeProven": False,
            "createBinEnabled": False,
            "normalCreateBinEnabled": False,
            "releaseAuthorized": False,
            "publishable": False,
        }
        document_payload = {
            "format": "spyro-editor-id65-authoring-editor-document-envelope",
            "schemaVersion": 1,
            "documentCanonicalSha256": (
                VERIFIER.EXPECTED_DOCUMENT_CANONICAL_SHA256
            ),
            "compositeManifestSha256": (
                VERIFIER.EXPECTED_COMPOSITE_MANIFEST_SHA256
            ),
            "compositeOutputRow80Sha256": VERIFIER.EXPECTED_OUTPUT_ROW80_SHA256,
            "document": {
                "contentSha256": VERIFIER.EXPECTED_DOCUMENT_CONTENT_SHA256,
            },
        }
        self.receipt_bytes = VERIFIER._canonical_json(receipt_payload).encode("utf-8")
        self.document_bytes = VERIFIER._canonical_json(document_payload).encode("utf-8")
        self.candidate_receipt.write_bytes(self.receipt_bytes)
        self.document.write_bytes(self.document_bytes)
        for path in (
            self.binary,
            self.cue,
            self.candidate_receipt,
            self.document,
        ):
            path.chmod(0o600)

    def __enter__(self) -> "VerifierDescriptorFixture":
        return self

    def __exit__(self, exc_type: object, exc: object, traceback: object) -> None:
        self._temporary.cleanup()

    def paths(
        self, repository_root: pathlib.Path
    ) -> tuple[pathlib.Path, pathlib.Path, pathlib.Path, pathlib.Path, pathlib.Path]:
        del repository_root
        return (
            self.root / VERIFIER.PROFILE_ID,
            self.cue,
            self.binary,
            self.candidate_receipt,
            self.document,
        )


class DescriptorBuilderDouble:
    """Opens real tiny descriptors but performs no hashing or profile work."""

    def __init__(self, *, fail_repin_call: int | None = None) -> None:
        self.fail_repin_call = fail_repin_call
        self.opened: list[Any] = []
        self.repins: list[tuple[Any, str]] = []
        self.closes: list[Any] = []
        self.cue_validations = 0

    def _open_pinned_file(
        self,
        path: pathlib.Path,
        label: str,
        *,
        expected_name: str,
        expected_size: int,
        expected_sha256: str,
    ) -> Any:
        del expected_size, expected_sha256
        if path.name != expected_name:
            raise AssertionError(f"noncanonical fake input name for {label}")
        handle = types.SimpleNamespace(
            path=path,
            label=label,
            fd=os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)),
        )
        self.opened.append(handle)
        return handle

    def _repin_held_file(self, handle: Any, label: str) -> None:
        self.repins.append((handle, label))
        os.fstat(handle.fd)
        if len(self.repins) == self.fail_repin_call:
            raise RuntimeError("fixture descriptor repin refusal")

    def _close_pinned_file(self, handle: Any) -> None:
        self.closes.append(handle)
        os.close(handle.fd)

    def _validate_cue_pair(self, cue: Any, binary: Any) -> None:
        os.fstat(cue.fd)
        os.fstat(binary.fd)
        self.cue_validations += 1


class FakeBuilder:
    """No-filesystem builder double for wrapper boundary classification."""

    BuildPins = FROZEN_BUILDER.BuildPins
    PRODUCTION_PINS = FROZEN_BUILDER.PRODUCTION_PINS
    SOURCE_APP_PATH = pathlib.Path("/mock/DuckStation.app")
    BIOS_PATH = pathlib.Path("/mock/bios.bin")
    COMMON_LOCK_LEAF = FROZEN_BUILDER.COMMON_LOCK_LEAF

    ProfileRefusal = FROZEN_BUILDER.ProfileRefusal
    PublishedProfileQuarantined = FROZEN_BUILDER.PublishedProfileQuarantined
    PublishedProfileDurabilityUnconfirmed = (
        FROZEN_BUILDER.PublishedProfileDurabilityUnconfirmed
    )
    PublishedProfileQuarantineFailed = (
        FROZEN_BUILDER.PublishedProfileQuarantineFailed
    )
    PublishedProfileQuarantineDurabilityUnconfirmed = (
        FROZEN_BUILDER.PublishedProfileQuarantineDurabilityUnconfirmed
    )
    PrivateStageCleanupFailed = FROZEN_BUILDER.PrivateStageCleanupFailed
    PrivateStageCleanupDurabilityUnconfirmed = (
        FROZEN_BUILDER.PrivateStageCleanupDurabilityUnconfirmed
    )

    def __init__(
        self,
        *,
        fail_repin_call: int | None = None,
        quarantine_after_publish: bool = False,
        checkpoints: tuple[str, ...] = ADAPTER.BUILD_HOOK_CHECKPOINTS,
    ) -> None:
        self.fail_repin_call = fail_repin_call
        self.quarantine_after_publish = quarantine_after_publish
        self.checkpoints = checkpoints
        self.repin_calls: list[tuple[object, str]] = []
        self.close_calls: list[object] = []
        self.hook_calls: list[str] = []
        self.build_calls = 0

    @staticmethod
    def _open_pinned_file(*args: object, **kwargs: object) -> object:
        raise AssertionError("fake builder must not open production metadata")

    def _repin_held_file(self, handle: object, label: str) -> None:
        self.repin_calls.append((handle, label))
        if len(self.repin_calls) == self.fail_repin_call:
            raise self.ProfileRefusal("mock aggregate metadata changed")

    def _close_pinned_file(self, handle: object) -> None:
        self.close_calls.append(handle)

    def build_profile(
        self,
        source_app_path: pathlib.Path,
        destination: pathlib.Path,
        candidate_cue_path: pathlib.Path,
        candidate_bin_path: pathlib.Path,
        bios_path: pathlib.Path,
        pins: Any,
        *,
        hook: Any = None,
    ) -> Any:
        del source_app_path, candidate_cue_path, candidate_bin_path, bios_path, pins
        self.build_calls += 1
        for checkpoint in self.checkpoints:
            self.hook_calls.append(checkpoint)
            context = types.SimpleNamespace(checkpoint=checkpoint)
            try:
                if hook is not None:
                    hook(context)
            except Exception as exc:
                if checkpoint == "after-publish" and self.quarantine_after_publish:
                    raise self.PublishedProfileQuarantined(
                        str(exc),
                        ".mock-quarantined-profile",
                    ) from exc
                raise
        return FROZEN_BUILDER.BuildResult(
            destination=destination,
            receipt_sha256="1" * 64,
            receipt_schema="mock-handoff-receipt@1",
            app_manifest_sha256="2" * 64,
            settings_sha256="3" * 64,
            launch_gate=ADAPTER.LAUNCH_GATE,
        )

    def verify_profile(self, *args: object, **kwargs: object) -> object:
        raise AssertionError("fake build tests must not call verify_profile")


class AggregateAdapterTests(unittest.TestCase):
    maxDiff = None

    @staticmethod
    def _expect_refusal(
        call: Any,
        exception_types: tuple[type[BaseException], ...],
        contains: str | None = None,
    ) -> BaseException:
        try:
            call()
        except exception_types as exc:
            if contains is not None and contains not in str(exc):
                raise AssertionError(
                    f"refusal did not contain {contains!r}: {str(exc)!r}"
                ) from exc
            return exc
        except Exception as exc:
            raise AssertionError(
                f"wrong refusal type {type(exc).__name__}: {exc}"
            ) from exc
        raise AssertionError("operation unexpectedly succeeded.")

    def _run_fake_build(
        self,
        fake: FakeBuilder,
        *,
        through_main: bool = False,
    ) -> tuple[object, str, str]:
        receipt_handle = object()
        document_handle = object()
        with (
            mock.patch.object(ADAPTER, "_load_frozen_builder", return_value=fake),
            mock.patch.object(
                ADAPTER,
                "_require_aggregate_metadata",
                return_value=(receipt_handle, document_handle),
            ),
            contextlib.redirect_stdout(io.StringIO()) as stdout,
            contextlib.redirect_stderr(io.StringIO()) as stderr,
        ):
            if through_main:
                result: object = ADAPTER.main(
                    [
                        "build",
                        "--repository-root",
                        os.fspath(REPOSITORY_ROOT),
                    ]
                )
            else:
                result = ADAPTER._run_profile_operation("build", REPOSITORY_ROOT)
        self.assertEqual(fake.close_calls, [document_handle, receipt_handle])
        return result, stdout.getvalue(), stderr.getvalue()

    def test_01_frozen_builder_hash_api_and_aggregate_pins(self) -> None:
        frozen_path = REPOSITORY_ROOT / ADAPTER.FROZEN_BUILDER_RELATIVE_PATH
        self.assertEqual(
            _sha256_bytes(frozen_path.read_bytes()),
            ADAPTER.EXPECTED_FROZEN_BUILDER_SHA256,
        )
        ADAPTER._require_builder_contract(FROZEN_BUILDER)
        self.assertEqual(
            tuple(field.name for field in dataclasses.fields(FROZEN_BUILDER.BuildPins)),
            (
                "app_version",
                "executable_sha256",
                "executable_size",
                "app_manifest_sha256",
                "app_manifest_entry_count",
                "candidate_cue_name",
                "candidate_cue_sha256",
                "candidate_cue_size",
                "candidate_bin_name",
                "candidate_bin_sha256",
                "candidate_bin_size",
                "bios_name",
                "bios_sha256",
                "bios_size",
                "settings_sha256",
                "settings_size",
            ),
        )
        self.assertEqual(
            tuple(inspect.signature(FROZEN_BUILDER.build_profile).parameters),
            (
                "source_app_path",
                "destination",
                "candidate_cue_path",
                "candidate_bin_path",
                "bios_path",
                "pins",
                "codesign_verifier",
                "hook",
            ),
        )
        self.assertEqual(
            tuple(inspect.signature(FROZEN_BUILDER.verify_profile).parameters),
            (
                "source_app_path",
                "destination",
                "candidate_cue_path",
                "candidate_bin_path",
                "bios_path",
                "pins",
                "codesign_verifier",
            ),
        )
        self.assertEqual(
            tuple(inspect.signature(FROZEN_BUILDER._open_pinned_file).parameters),
            (
                "path",
                "label",
                "expected_name",
                "expected_size",
                "expected_sha256",
            ),
        )
        self.assertEqual(
            tuple(inspect.signature(FROZEN_BUILDER._repin_held_file).parameters),
            ("handle", "label"),
        )
        self.assertTrue(
            issubclass(
                FROZEN_BUILDER.PublishedProfileQuarantined,
                FROZEN_BUILDER.ProfileRefusal,
            )
        )

        pins = ADAPTER._aggregate_pins(FROZEN_BUILDER)
        production = dataclasses.asdict(FROZEN_BUILDER.PRODUCTION_PINS)
        aggregate = dataclasses.asdict(pins)
        expected_candidate = {
            "candidate_cue_name": f"{ADAPTER.CANDIDATE_STEM}.cue",
            "candidate_cue_sha256": ADAPTER.EXPECTED_CANDIDATE_CUE_SHA256,
            "candidate_cue_size": ADAPTER.EXPECTED_CANDIDATE_CUE_SIZE,
            "candidate_bin_name": f"{ADAPTER.CANDIDATE_STEM}.bin",
            "candidate_bin_sha256": ADAPTER.EXPECTED_CANDIDATE_BIN_SHA256,
            "candidate_bin_size": ADAPTER.EXPECTED_CANDIDATE_BIN_SIZE,
        }
        for name, expected in expected_candidate.items():
            self.assertEqual(aggregate.pop(name), expected)
            production.pop(name)
        self.assertEqual(aggregate, production)
        ADAPTER._self_test(REPOSITORY_ROOT)

        frozen_bytes = frozen_path.read_bytes()
        with mock.patch.object(pathlib.Path, "read_bytes", return_value=frozen_bytes + b"x"):
            self._expect_refusal(
                lambda: ADAPTER._load_frozen_builder(REPOSITORY_ROOT),
                (ADAPTER.AggregateProfileRefusal,),
                "frozen profile builder hash changed",
            )

    def test_02_repository_root_and_fixed_path_identities(self) -> None:
        self.assertEqual(
            ADAPTER._require_repository_root(REPOSITORY_ROOT),
            REPOSITORY_ROOT,
        )
        self._expect_refusal(
            lambda: ADAPTER._require_repository_root(pathlib.Path("relative-root")),
            (ADAPTER.AggregateProfileRefusal,),
            "must be absolute",
        )
        lexical_alias = pathlib.Path(
            os.fspath(
                REPOSITORY_ROOT.parent
                / "nonexistent-placeholder"
                / ".."
                / REPOSITORY_ROOT.name
            )
        )
        self._expect_refusal(
            lambda: ADAPTER._require_repository_root(lexical_alias),
            (ADAPTER.AggregateProfileRefusal,),
            "lexically normalized",
        )
        self._expect_refusal(
            lambda: ADAPTER._require_repository_root(REPOSITORY_ROOT.parent),
            (ADAPTER.AggregateProfileRefusal,),
            "real checkout",
        )
        local_root = REPOSITORY_ROOT / "_local"
        with tempfile.TemporaryDirectory(
            prefix=".id65-root-alias-test-", dir=local_root
        ) as temporary:
            alias = pathlib.Path(temporary) / "checkout-alias"
            alias.symlink_to(REPOSITORY_ROOT, target_is_directory=True)
            self._expect_refusal(
                lambda: ADAPTER._require_repository_root(alias),
                (ADAPTER.AggregateProfileRefusal,),
                "real checkout",
            )

        destination, cue, binary, receipt, document = ADAPTER._paths(REPOSITORY_ROOT)
        self.assertEqual(
            (destination, cue, binary, receipt, document),
            (
                REPOSITORY_ROOT / ADAPTER.PROFILE_DESTINATION_RELATIVE_PATH,
                REPOSITORY_ROOT / ADAPTER.CANDIDATE_CUE_RELATIVE_PATH,
                REPOSITORY_ROOT / ADAPTER.CANDIDATE_BIN_RELATIVE_PATH,
                REPOSITORY_ROOT / ADAPTER.CANDIDATE_RECEIPT_RELATIVE_PATH,
                REPOSITORY_ROOT / ADAPTER.DOCUMENT_RELATIVE_PATH,
            ),
        )
        self.assertEqual(destination.name, ADAPTER.PROFILE_ID)
        self.assertEqual(cue.parent, binary.parent)
        self.assertEqual(receipt.parent, binary.parent)
        with mock.patch.object(ADAPTER, "PROFILE_ID", "wrong-profile-id"):
            self._expect_refusal(
                lambda: ADAPTER._paths(REPOSITORY_ROOT),
                (ADAPTER.AggregateProfileRefusal,),
                "destination identity changed",
            )
        wrong_parent = pathlib.PurePosixPath("_local/wrong-parent") / ADAPTER.PROFILE_ID
        with mock.patch.object(
            ADAPTER,
            "PROFILE_DESTINATION_RELATIVE_PATH",
            wrong_parent,
        ):
            self._expect_refusal(
                lambda: ADAPTER._paths(REPOSITORY_ROOT),
                (ADAPTER.AggregateProfileRefusal,),
                "destination identity changed",
            )

    def test_03_exact_production_metadata_is_read_only_and_accepted(self) -> None:
        _, _, _, receipt_path, document_path = ADAPTER._paths(REPOSITORY_ROOT)
        before = {
            receipt_path: _file_snapshot(receipt_path),
            document_path: _file_snapshot(document_path),
        }
        receipt_handle, document_handle = ADAPTER._require_aggregate_metadata(
            FROZEN_BUILDER,
            receipt_path,
            document_path,
        )
        try:
            self.assertEqual(receipt_handle.absolute_path, receipt_path)
            self.assertEqual(document_handle.absolute_path, document_path)
        finally:
            FROZEN_BUILDER._close_pinned_file(document_handle)
            FROZEN_BUILDER._close_pinned_file(receipt_handle)
        for handle in (receipt_handle, document_handle):
            with self.assertRaises(OSError):
                os.fstat(handle.fd)
        self.assertEqual(
            {
                receipt_path: _file_snapshot(receipt_path),
                document_path: _file_snapshot(document_path),
            },
            before,
        )

    def test_04_temporary_envelope_and_semantic_tampering_refuses(self) -> None:
        refusal_types = (
            ADAPTER.AggregateProfileRefusal,
            FROZEN_BUILDER.ProfileRefusal,
        )
        with MetadataCopies() as copies:
            receipt_raw = bytearray(copies.receipt.read_bytes())
            receipt_raw[len(receipt_raw) // 2] ^= 1
            copies.receipt.write_bytes(receipt_raw)
            copies.receipt.chmod(0o600)
            self._expect_refusal(
                lambda: ADAPTER._require_aggregate_metadata(
                    FROZEN_BUILDER, copies.receipt, copies.document
                ),
                refusal_types,
                "frozen pin",
            )

        with MetadataCopies() as copies:
            document_raw = bytearray(copies.document.read_bytes())
            document_raw[len(document_raw) // 2] ^= 1
            copies.document.write_bytes(document_raw)
            copies.document.chmod(0o600)
            self._expect_refusal(
                lambda: ADAPTER._require_aggregate_metadata(
                    FROZEN_BUILDER, copies.receipt, copies.document
                ),
                refusal_types,
                "frozen pin",
            )

        with MetadataCopies() as copies:
            parsed = json.loads(copies.receipt.read_bytes())
            parsed["runtimeProven"] = True
            tampered = json.dumps(
                parsed,
                ensure_ascii=False,
                allow_nan=False,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
            copies.receipt.write_bytes(tampered)
            copies.receipt.chmod(0o600)
            with (
                mock.patch.object(
                    ADAPTER,
                    "EXPECTED_CANDIDATE_RECEIPT_FILE_SIZE",
                    len(tampered),
                ),
                mock.patch.object(
                    ADAPTER,
                    "EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256",
                    _sha256_bytes(tampered),
                ),
            ):
                self._expect_refusal(
                    lambda: ADAPTER._require_aggregate_metadata(
                        FROZEN_BUILDER, copies.receipt, copies.document
                    ),
                    (ADAPTER.AggregateProfileRefusal,),
                    "receipt field changed: runtimeProven",
                )

        with MetadataCopies() as copies:
            parsed = json.loads(copies.document.read_bytes())
            nested = parsed["document"]
            original = nested["contentSha256"]
            nested["contentSha256"] = ("0" if original[0] != "0" else "1") + original[1:]
            tampered = json.dumps(
                parsed,
                ensure_ascii=False,
                allow_nan=False,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
            copies.document.write_bytes(tampered)
            copies.document.chmod(0o600)
            tampered_envelope_sha256 = _sha256_bytes(tampered)
            receipt_json = json.loads(copies.receipt.read_bytes())
            receipt_json["documentEnvelopeSha256"] = tampered_envelope_sha256
            matching_receipt = json.dumps(
                receipt_json,
                ensure_ascii=False,
                allow_nan=False,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
            copies.receipt.write_bytes(matching_receipt)
            copies.receipt.chmod(0o600)
            with (
                mock.patch.object(
                    ADAPTER,
                    "EXPECTED_DOCUMENT_FILE_SIZE",
                    len(tampered),
                ),
                mock.patch.object(
                    ADAPTER,
                    "EXPECTED_DOCUMENT_ENVELOPE_SHA256",
                    tampered_envelope_sha256,
                ),
                mock.patch.object(
                    ADAPTER,
                    "EXPECTED_CANDIDATE_RECEIPT_FILE_SIZE",
                    len(matching_receipt),
                ),
                mock.patch.object(
                    ADAPTER,
                    "EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256",
                    _sha256_bytes(matching_receipt),
                ),
            ):
                self._expect_refusal(
                    lambda: ADAPTER._require_aggregate_metadata(
                        FROZEN_BUILDER, copies.receipt, copies.document
                    ),
                    (ADAPTER.AggregateProfileRefusal,),
                    "document content closure changed",
                )

    def test_05_all_hook_checkpoints_repin_and_close_metadata(self) -> None:
        fake = FakeBuilder()
        result, stdout, stderr = self._run_fake_build(fake)
        self.assertEqual(stdout, "")
        self.assertEqual(stderr, "")
        self.assertIsInstance(result, dict)
        self.assertEqual(fake.hook_calls, list(ADAPTER.BUILD_HOOK_CHECKPOINTS))
        self.assertEqual(len(fake.repin_calls), 10)
        self.assertEqual(
            [label for _, label in fake.repin_calls],
            [
                "aggregate candidate receipt",
                "aggregate authoring document",
            ]
            * 5,
        )
        self.assertEqual(result["operation"], "build")
        self.assertTrue(result["profileBuilt"])
        self.assertTrue(result["profileVerified"])
        self.assertFalse(result["duckStationLaunched"])
        self.assertFalse(result["runtimeProven"])
        self.assertFalse(result["releaseAuthorized"])
        self.assertFalse(result["publishable"])

    def test_06_after_publish_hook_failure_maps_to_quarantine_exit_3(self) -> None:
        # Calls 1-6 are the first three checkpoint pairs.  Call 7 is the
        # receipt repin inside after-publish, while the frozen builder still
        # owns exact-inode quarantine authority.
        fake = FakeBuilder(
            fail_repin_call=7,
            quarantine_after_publish=True,
        )
        result, stdout, stderr = self._run_fake_build(fake, through_main=True)
        self.assertEqual(result, 3)
        self.assertEqual(stdout, "")
        self.assertIn("REFUSED:", stderr)
        self.assertIn("quarantined as .mock-quarantined-profile", stderr)
        self.assertEqual(fake.hook_calls, list(ADAPTER.BUILD_HOOK_CHECKPOINTS))
        self.assertEqual(len(fake.repin_calls), 7)

    def test_07_post_return_repin_failure_is_binding_unconfirmed_exit_9(self) -> None:
        # Calls 1-8 are the four hook checkpoint pairs.  Call 9 occurs only
        # after build_profile returned, so quarantine must not be claimed.
        fake = FakeBuilder(fail_repin_call=9)
        result, stdout, stderr = self._run_fake_build(fake, through_main=True)
        self.assertEqual(result, 9)
        self.assertEqual(stdout, "")
        self.assertIn("REFUSED:", stderr)
        self.assertIn("profile published at", stderr)
        self.assertIn("binding could not be reconfirmed", stderr)
        self.assertNotIn("quarantined as", stderr)
        self.assertEqual(fake.hook_calls, list(ADAPTER.BUILD_HOOK_CHECKPOINTS))
        self.assertEqual(len(fake.repin_calls), 9)

    def test_08_wrong_or_incomplete_hook_closure_refuses(self) -> None:
        fake = FakeBuilder(
            checkpoints=(
                "after-initial-validation",
                "before-publish",
            )
        )
        with self.assertRaises(ADAPTER.AggregateProfileRefusal) as caught:
            self._run_fake_build(fake)
        self.assertIn("hook checkpoint changed", str(caught.exception))
        self.assertEqual(len(fake.repin_calls), 2)

    def test_09_final_verifier_hash_contract_and_static_no_launch_graph(self) -> None:
        self.assertEqual(
            VERIFIER_LOAD_SHA256,
            EXPECTED_AGGREGATE_VERIFIER_SHA256,
        )
        self.assertEqual(
            VERIFIER.EXPECTED_FROZEN_VERIFIER_SHA256,
            "74f4ed279a3e3f60af6c4c0a0b1fa8ffc0a67886c478d8cc7d1a288b093c3671",
        )
        self.assertEqual(
            VERIFIER.EXPECTED_FROZEN_BUILDER_SHA256,
            ADAPTER.EXPECTED_FROZEN_BUILDER_SHA256,
        )
        VERIFIER._require_frozen_verifier_contract(FROZEN_VERIFIER)
        VERIFIER._self_test(REPOSITORY_ROOT)

        source = VERIFIER_PATH.read_bytes()
        self.assertEqual(_sha256_bytes(source), EXPECTED_AGGREGATE_VERIFIER_SHA256)
        syntax = ast.parse(source, filename=os.fspath(VERIFIER_PATH))
        imports = {
            alias.name
            for node in ast.walk(syntax)
            if isinstance(node, (ast.Import, ast.ImportFrom))
            for alias in node.names
        }
        self.assertNotIn("subprocess", imports)
        all_called_attributes = [
            node.func.attr
            for node in ast.walk(syntax)
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute)
        ]
        self.assertNotIn("build_profile", all_called_attributes)
        for forbidden in (
            "Popen",
            "system",
            "startfile",
            "execl",
            "execle",
            "execlp",
            "execlpe",
            "execv",
            "execve",
            "execvp",
            "execvpe",
            "posix_spawn",
            "posix_spawnp",
            "spawnl",
            "spawnle",
            "spawnlp",
            "spawnlpe",
            "spawnv",
            "spawnve",
            "spawnvp",
            "spawnvpe",
        ):
            self.assertNotIn(forbidden, all_called_attributes)

        functions = {
            node.name: node
            for node in syntax.body
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef))
        }
        verify_calls = [
            node.func.attr
            for node in ast.walk(functions["verify_prelaunch"])
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute)
        ]
        self.assertEqual(verify_calls.count("verify_profile"), 1)
        self.assertNotIn("build_profile", verify_calls)
        snapshot_calls = [
            node.func.attr
            for node in ast.walk(functions["capture_runtime_snapshot"])
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute)
        ]
        self.assertNotIn("verify_profile", snapshot_calls)
        self.assertNotIn("build_profile", snapshot_calls)

    def test_10_aggregate_pins_replace_only_audited_old_profile_fields(self) -> None:
        aggregate_build = VERIFIER._aggregate_build_pins(VERIFIER_BUILDER)
        changed_build_fields = {
            field.name
            for field in dataclasses.fields(VERIFIER_BUILDER.BuildPins)
            if getattr(aggregate_build, field.name)
            != getattr(VERIFIER_BUILDER.PRODUCTION_PINS, field.name)
        }
        self.assertEqual(
            changed_build_fields,
            {
                "candidate_cue_name",
                "candidate_cue_sha256",
                "candidate_cue_size",
                "candidate_bin_name",
                "candidate_bin_sha256",
            },
        )
        self.assertEqual(
            aggregate_build.candidate_bin_size,
            VERIFIER_BUILDER.PRODUCTION_PINS.candidate_bin_size,
        )
        self.assertNotEqual(
            aggregate_build.candidate_bin_sha256,
            VERIFIER_BUILDER.PRODUCTION_PINS.candidate_bin_sha256,
        )
        self.assertNotEqual(
            aggregate_build.candidate_cue_sha256,
            VERIFIER_BUILDER.PRODUCTION_PINS.candidate_cue_sha256,
        )

        aggregate_profile = VERIFIER._profile_pins(
            FROZEN_VERIFIER,
            VERIFIER_BUILDER,
        )
        old_profile = FROZEN_VERIFIER.PRODUCTION_PINS
        changed_profile_fields = {
            field.name
            for field in dataclasses.fields(FROZEN_VERIFIER.ProfilePins)
            if getattr(aggregate_profile, field.name)
            != getattr(old_profile, field.name)
        }
        self.assertEqual(
            changed_profile_fields,
            {
                "profile_id",
                "package_relative_path",
                "receipt_size",
                "receipt_sha256",
                "receipt_closure_sha256",
                "package_envelope_sha256",
                "candidate_cue_relative_path",
                "candidate_cue_size",
                "candidate_cue_sha256",
                "candidate_bin_relative_path",
                "candidate_bin_sha256",
                "candidate_manifest_sha256",
            },
        )
        self.assertEqual(aggregate_profile.profile_id, VERIFIER.PROFILE_ID)
        self.assertEqual(
            aggregate_profile.package_relative_path,
            VERIFIER.PROFILE_DESTINATION_RELATIVE_PATH,
        )
        self.assertEqual(
            aggregate_profile.candidate_cue_relative_path,
            VERIFIER.CANDIDATE_CUE_RELATIVE_PATH,
        )
        self.assertEqual(
            aggregate_profile.candidate_bin_relative_path,
            VERIFIER.CANDIDATE_BIN_RELATIVE_PATH,
        )
        self.assertEqual(
            aggregate_profile.candidate_manifest_sha256,
            VERIFIER.EXPECTED_PROFILE_OUTPUT_PINS["candidateManifestSha256"],
        )
        for immutable in (
            "app_leaf",
            "app_version",
            "executable_sha256",
            "app_content_manifest_sha256",
            "settings_sha256",
            "bios_sha256",
            "source_app_path",
            "source_bios_path",
            "host_settings_path",
            "host_settings_sha256",
        ):
            self.assertEqual(
                getattr(aggregate_profile, immutable),
                getattr(old_profile, immutable),
            )

    def test_11_output_pin_set_is_strictly_all_or_none(self) -> None:
        complete = dict(VERIFIER.EXPECTED_PROFILE_OUTPUT_PINS)
        VERIFIER._validate_output_pin_set(allow_pending=False)
        self.assertEqual(VERIFIER._pending_output_pin_names(), [])

        all_pending = {name: VERIFIER.PENDING for name in complete}
        with mock.patch.object(
            VERIFIER,
            "EXPECTED_PROFILE_OUTPUT_PINS",
            all_pending,
        ):
            VERIFIER._validate_output_pin_set(allow_pending=True)
            self.assertEqual(
                VERIFIER._pending_output_pin_names(),
                sorted(complete),
            )
            self._expect_refusal(
                lambda: VERIFIER._validate_output_pin_set(allow_pending=False),
                (VERIFIER.AggregateProfileVerificationRefusal,),
                "output pins are PENDING",
            )

        partial = dict(complete)
        partial["receiptSha256"] = VERIFIER.PENDING
        with mock.patch.object(
            VERIFIER,
            "EXPECTED_PROFILE_OUTPUT_PINS",
            partial,
        ):
            self._expect_refusal(
                lambda: VERIFIER._validate_output_pin_set(allow_pending=True),
                (VERIFIER.AggregateProfileVerificationRefusal,),
                "partially populated",
            )

        missing = dict(complete)
        missing.pop("candidateManifestSha256")
        with mock.patch.object(
            VERIFIER,
            "EXPECTED_PROFILE_OUTPUT_PINS",
            missing,
        ):
            self._expect_refusal(
                lambda: VERIFIER._validate_output_pin_set(allow_pending=True),
                (VERIFIER.AggregateProfileVerificationRefusal,),
                "key closure changed",
            )

        invalid_integer = dict(complete)
        invalid_integer["receiptSize"] = True
        with mock.patch.object(
            VERIFIER,
            "EXPECTED_PROFILE_OUTPUT_PINS",
            invalid_integer,
        ):
            self._expect_refusal(
                lambda: VERIFIER._validate_output_pin_set(allow_pending=False),
                (VERIFIER.AggregateProfileVerificationRefusal,),
                "not a positive integer: receiptSize",
            )

        invalid_hash = dict(complete)
        invalid_hash["receiptSha256"] = "A" * 64
        with mock.patch.object(
            VERIFIER,
            "EXPECTED_PROFILE_OUTPUT_PINS",
            invalid_hash,
        ):
            self._expect_refusal(
                lambda: VERIFIER._validate_output_pin_set(allow_pending=False),
                (VERIFIER.AggregateProfileVerificationRefusal,),
                "not canonical SHA-256: receiptSha256",
            )

    def test_12_verifier_descriptor_repins_and_closes_on_success_or_refusal(self) -> None:
        with VerifierDescriptorFixture() as fixture:
            builder = DescriptorBuilderDouble()
            with (
                mock.patch.object(VERIFIER, "_aggregate_paths", side_effect=fixture.paths),
                mock.patch.object(
                    VERIFIER,
                    "EXPECTED_CANDIDATE_RECEIPT_FILE_SIZE",
                    len(fixture.receipt_bytes),
                ),
                mock.patch.object(
                    VERIFIER,
                    "EXPECTED_DOCUMENT_FILE_SIZE",
                    len(fixture.document_bytes),
                ),
            ):
                handles = VERIFIER._open_aggregate_inputs(builder, REPOSITORY_ROOT)
                self.assertEqual(
                    [label for _, label in builder.repins],
                    [
                        "aggregate candidate CUE",
                        "aggregate candidate BIN",
                        "aggregate candidate receipt",
                        "aggregate authoring document",
                    ],
                )
                self.assertEqual(builder.cue_validations, 2)
                VERIFIER._close_aggregate_inputs(builder, handles)
            self.assertEqual(
                builder.closes,
                [
                    handles.document,
                    handles.candidate_receipt,
                    handles.binary,
                    handles.cue,
                ],
            )
            for handle in builder.opened:
                with self.assertRaises(OSError):
                    os.fstat(handle.fd)

        with VerifierDescriptorFixture() as fixture:
            builder = DescriptorBuilderDouble(fail_repin_call=3)
            with (
                mock.patch.object(VERIFIER, "_aggregate_paths", side_effect=fixture.paths),
                mock.patch.object(
                    VERIFIER,
                    "EXPECTED_CANDIDATE_RECEIPT_FILE_SIZE",
                    len(fixture.receipt_bytes),
                ),
                mock.patch.object(
                    VERIFIER,
                    "EXPECTED_DOCUMENT_FILE_SIZE",
                    len(fixture.document_bytes),
                ),
            ):
                with self.assertRaisesRegex(
                    RuntimeError,
                    "fixture descriptor repin refusal",
                ):
                    VERIFIER._open_aggregate_inputs(builder, REPOSITORY_ROOT)
            self.assertEqual(
                builder.closes,
                list(reversed(builder.opened)),
            )
            for handle in builder.opened:
                with self.assertRaises(OSError):
                    os.fstat(handle.fd)

        handles = VERIFIER._AggregateHandles(object(), object(), object(), object())
        close_calls: list[object] = []
        repin_calls: list[object] = []
        with (
            mock.patch.object(VERIFIER, "_load_frozen_verifier", return_value=FROZEN_VERIFIER),
            mock.patch.object(VERIFIER, "_load_frozen_builder", return_value=VERIFIER_BUILDER),
            mock.patch.object(VERIFIER, "_profile_pins", return_value=object()),
            mock.patch.object(VERIFIER, "_open_aggregate_inputs", return_value=handles),
            mock.patch.object(
                FROZEN_VERIFIER,
                "_capture_runtime_snapshot_with_builder",
                return_value={"fixture": "snapshot"},
            ),
            mock.patch.object(
                VERIFIER,
                "_repin_aggregate_inputs",
                side_effect=lambda builder, value: repin_calls.append(value),
            ),
            mock.patch.object(
                VERIFIER,
                "_close_aggregate_inputs",
                side_effect=lambda builder, value: close_calls.append(value),
            ),
        ):
            snapshot = VERIFIER.capture_runtime_snapshot(
                REPOSITORY_ROOT,
                phase="fixture",
                session_id="fixture",
                process_instance_id="fixture",
            )
        self.assertEqual(snapshot, {"fixture": "snapshot"})
        self.assertEqual(repin_calls, [handles])
        self.assertEqual(close_calls, [handles])

        close_calls.clear()
        with (
            mock.patch.object(VERIFIER, "_load_frozen_verifier", return_value=FROZEN_VERIFIER),
            mock.patch.object(VERIFIER, "_load_frozen_builder", return_value=VERIFIER_BUILDER),
            mock.patch.object(VERIFIER, "_profile_pins", return_value=object()),
            mock.patch.object(VERIFIER, "_open_aggregate_inputs", return_value=handles),
            mock.patch.object(
                FROZEN_VERIFIER,
                "_capture_runtime_snapshot_with_builder",
                side_effect=FROZEN_VERIFIER.ProfileVerificationRefusal(
                    "fixture snapshot refusal"
                ),
            ),
            mock.patch.object(
                VERIFIER,
                "_close_aggregate_inputs",
                side_effect=lambda builder, value: close_calls.append(value),
            ),
        ):
            with self.assertRaisesRegex(
                VERIFIER.AggregateProfileVerificationRefusal,
                "fixture snapshot refusal",
            ):
                VERIFIER.capture_runtime_snapshot(
                    REPOSITORY_ROOT,
                    phase="fixture",
                    session_id="fixture",
                    process_instance_id="fixture",
                )
        self.assertEqual(close_calls, [handles])

    def test_13_verifier_library_and_cli_exception_mapping(self) -> None:
        aggregate = VERIFIER.AggregateProfileVerificationRefusal("aggregate refusal")
        translated = VERIFIER._translate_refusal(
            aggregate,
            FROZEN_VERIFIER,
            VERIFIER_BUILDER,
        )
        self.assertIsInstance(
            translated,
            VERIFIER.AggregateProfileVerificationRefusal,
        )
        self.assertEqual(str(translated), "aggregate refusal")
        self.assertIsNot(translated, aggregate)

        for source_exception in (
            FROZEN_VERIFIER.ProfileVerificationRefusal("verifier refusal"),
            VERIFIER_BUILDER.ProfileRefusal("builder refusal"),
        ):
            translated = VERIFIER._translate_refusal(
                source_exception,
                FROZEN_VERIFIER,
                VERIFIER_BUILDER,
            )
            self.assertIsInstance(
                translated,
                VERIFIER.AggregateProfileVerificationRefusal,
            )
            self.assertEqual(str(translated), str(source_exception))
        unexpected = VERIFIER._translate_refusal(
            ValueError("bad fixture"),
            FROZEN_VERIFIER,
            VERIFIER_BUILDER,
        )
        self.assertEqual(
            str(unexpected),
            "unexpected aggregate profile verification failure: "
            "ValueError: bad fixture",
        )

        with (
            mock.patch.object(
                VERIFIER,
                "verify_prelaunch",
                side_effect=VERIFIER.AggregateProfileVerificationRefusal(
                    "mapped CLI refusal"
                ),
            ),
            contextlib.redirect_stdout(io.StringIO()) as stdout,
            contextlib.redirect_stderr(io.StringIO()) as stderr,
        ):
            exit_code = VERIFIER.main(
                ["prelaunch", "--repository-root", os.fspath(REPOSITORY_ROOT)]
            )
        self.assertEqual(exit_code, 2)
        self.assertEqual(stdout.getvalue(), "")
        self.assertEqual(stderr.getvalue(), "REFUSED: mapped CLI refusal\n")

        with (
            mock.patch.object(
                VERIFIER,
                "verify_prelaunch",
                side_effect=ValueError("unexpected CLI fixture"),
            ),
            contextlib.redirect_stdout(io.StringIO()) as stdout,
            contextlib.redirect_stderr(io.StringIO()) as stderr,
        ):
            exit_code = VERIFIER.main(
                ["prelaunch", "--repository-root", os.fspath(REPOSITORY_ROOT)]
            )
        self.assertEqual(exit_code, 2)
        self.assertEqual(stdout.getvalue(), "")
        self.assertEqual(
            stderr.getvalue(),
            "REFUSED: unexpected aggregate profile verifier failure: "
            "ValueError: unexpected CLI fixture\n",
        )

    def test_14_verifier_status_words_prelaunch_without_runtime_claims(self) -> None:
        status = VERIFIER.verifier_status()
        self.assertEqual(
            status["format"],
            "spyro-id65-authoring-aggregate-profile-verifier-status",
        )
        self.assertEqual(status["pendingOutputPins"], [])
        self.assertTrue(status["prelaunchVerificationReady"])
        self.assertEqual(status["launchGate"], VERIFIER.LAUNCH_GATE)
        for name in (
            "profileBuiltByVerifier",
            "duckStationLaunched",
            "runtimeProven",
            "evidenceAccepted",
            "createBinAuthorized",
            "releaseAuthorized",
            "publishable",
        ):
            self.assertIs(status[name], False, name)
        for misleading_claim in (
            "profileVerified",
            "runtimeReady",
            "runtimeVerificationReady",
            "releaseReady",
        ):
            self.assertNotIn(misleading_claim, status)

        all_pending = {
            name: VERIFIER.PENDING
            for name in VERIFIER.EXPECTED_PROFILE_OUTPUT_PINS
        }
        with mock.patch.object(
            VERIFIER,
            "EXPECTED_PROFILE_OUTPUT_PINS",
            all_pending,
        ):
            pending_status = VERIFIER.verifier_status()
        self.assertFalse(pending_status["prelaunchVerificationReady"])
        self.assertEqual(
            pending_status["pendingOutputPins"],
            sorted(all_pending),
        )
        self.assertFalse(pending_status["runtimeProven"])
        self.assertFalse(pending_status["releaseAuthorized"])

        with (
            mock.patch.object(VERIFIER, "_self_test", return_value=None),
            contextlib.redirect_stdout(io.StringIO()) as stdout,
            contextlib.redirect_stderr(io.StringIO()) as stderr,
        ):
            exit_code = VERIFIER.main(
                ["status", "--repository-root", os.fspath(REPOSITORY_ROOT)]
            )
        self.assertEqual(exit_code, 0)
        self.assertEqual(stderr.getvalue(), "")
        cli_status = json.loads(stdout.getvalue())
        self.assertTrue(cli_status["prelaunchVerificationReady"])
        self.assertFalse(cli_status["duckStationLaunched"])
        self.assertFalse(cli_status["runtimeProven"])
        self.assertFalse(cli_status["publishable"])

    def test_15_full_current_prelaunch_is_read_only_and_never_builds(self) -> None:
        before = _production_prelaunch_state_snapshot()
        with (
            mock.patch.object(
                VERIFIER,
                "_load_frozen_verifier",
                return_value=FROZEN_VERIFIER,
            ),
            mock.patch.object(
                VERIFIER,
                "_load_frozen_builder",
                return_value=VERIFIER_BUILDER,
            ),
            mock.patch.object(
                VERIFIER_BUILDER,
                "build_profile",
                side_effect=AssertionError("production profile build is forbidden"),
            ) as forbidden_build,
        ):
            result = VERIFIER.verify_prelaunch(REPOSITORY_ROOT)
        forbidden_build.assert_not_called()
        after = _production_prelaunch_state_snapshot()
        self.assertEqual(after, before)
        self.assertEqual(result["format"], VERIFIER.PREFLIGHT_FORMAT)
        self.assertEqual(result["status"], "PREFLIGHT_READY")
        self.assertEqual(result["profileId"], VERIFIER.PROFILE_ID)
        self.assertEqual(result["launchGate"], VERIFIER.LAUNCH_GATE)
        self.assertEqual(
            result["candidateBinSha256"],
            VERIFIER.EXPECTED_CANDIDATE_BIN_SHA256,
        )
        self.assertEqual(
            result["candidateReceiptContentSha256"],
            VERIFIER.EXPECTED_CANDIDATE_RECEIPT_CONTENT_SHA256,
        )
        self.assertIsInstance(result["snapshot"], dict)
        for name in (
            "duckStationLaunched",
            "runtimeProven",
            "evidenceAccepted",
            "createBinAuthorized",
            "releaseAuthorized",
            "publishable",
        ):
            self.assertIs(result[name], False, name)


def main() -> int:
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(AggregateAdapterTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    try:
        _assert_adapter_still_loaded_snapshot()
        _assert_source_still_loaded_snapshot(
            VERIFIER_PATH,
            VERIFIER_LOAD_IDENTITY,
            VERIFIER_LOAD_SHA256,
            "aggregate verifier",
        )
        if _sha256_bytes(SCRIPT_PATH.read_bytes()) != TEST_FILE_LOAD_SHA256:
            raise TestHarnessFailure("test harness bytes changed during tests.")
    except Exception as exc:
        print(
            f"ADAPTER_SNAPSHOT_FAILURE: {type(exc).__name__}: {exc}",
            file=sys.stderr,
        )
        return 1
    if not result.wasSuccessful():
        return 1
    print(
        "ID65_AUTHORING_AGGREGATE_PROFILE_ADAPTER_TESTS_OK "
        f"tests={result.testsRun} adapter={ADAPTER_LOAD_SHA256} "
        f"verifier={VERIFIER_LOAD_SHA256} harness={TEST_FILE_LOAD_SHA256}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
