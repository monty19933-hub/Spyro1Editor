#!/usr/bin/env python3
"""Offline adversarial tests for the aggregate save-profile tools.

The suite never builds a profile, launches DuckStation, or accepts a runtime
memory card.  Destructive cases use private temporary metadata copies or an
in-memory fake builder.
"""

from __future__ import annotations

import ast
import contextlib
import dataclasses
import hashlib
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
BUILDER_PATH = SCRIPT_PATH.with_name(
    "Build-SpyroId65AuthoringAggregateSaveV1Profile.py"
)
VERIFIER_PATH = SCRIPT_PATH.with_name(
    "Verify-SpyroId65AuthoringAggregateSaveV1Profile.py"
)
EXPECTED_BUILDER_SHA256 = (
    "759ec6a561166fc3e879db58db56781f757735b0975a5db0768248f276aec297"
)
EXPECTED_VERIFIER_SHA256 = (
    "36913ca29f4d4bdbb2f7ae539df3f6033f5018f5a39a77affce2638c1ca4042b"
)


class TestHarnessFailure(RuntimeError):
    """The test harness could not establish a stable tool snapshot."""


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _stable_load(
    path: pathlib.Path,
    expected_sha256: str,
    module_name: str,
) -> types.ModuleType:
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
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
    fields = (
        "st_dev", "st_ino", "st_mode", "st_uid", "st_gid", "st_nlink",
        "st_size", "st_mtime_ns", "st_ctime_ns",
    )
    if any(getattr(before, name) != getattr(after, name) for name in fields):
        raise TestHarnessFailure(f"tool changed while read: {path.name}")
    source = b"".join(chunks)
    observed = _sha256_bytes(source)
    if observed != expected_sha256:
        raise TestHarnessFailure(
            f"tool hash changed for {path.name}: {observed}"
        )
    module = types.ModuleType(module_name)
    module.__file__ = os.fspath(path)
    module.__package__ = None
    sys.modules[module_name] = module
    try:
        exec(compile(source, os.fspath(path), "exec"), module.__dict__)
    except Exception:
        sys.modules.pop(module_name, None)
        raise
    return module


BUILDER = _stable_load(
    BUILDER_PATH,
    EXPECTED_BUILDER_SHA256,
    "_spyro_id65_aggregate_save_builder_under_test",
)
VERIFIER = _stable_load(
    VERIFIER_PATH,
    EXPECTED_VERIFIER_SHA256,
    "_spyro_id65_aggregate_save_verifier_under_test",
)


def _file_identity(path: pathlib.Path) -> tuple[object, ...]:
    before = os.lstat(path)
    raw = path.read_bytes()
    after = os.lstat(path)
    fields = (
        "st_dev", "st_ino", "st_mode", "st_uid", "st_gid", "st_nlink",
        "st_size", "st_mtime_ns", "st_ctime_ns",
    )
    first = tuple(getattr(before, name) for name in fields)
    second = tuple(getattr(after, name) for name in fields)
    if first != second:
        raise TestHarnessFailure(f"file changed while snapshotted: {path}")
    return first + (_sha256_bytes(raw),)


class MetadataCopies:
    def __init__(self) -> None:
        self._temporary = tempfile.TemporaryDirectory(
            prefix=".id65-aggregate-save-profile-test-",
            dir=REPOSITORY_ROOT / "_local",
        )
        self.root = pathlib.Path(self._temporary.name)
        self.root.chmod(0o700)
        _, _, _, receipt, document = BUILDER._paths(REPOSITORY_ROOT)
        self.receipt = self.root / receipt.name
        self.document = self.root / document.name
        self.receipt.write_bytes(receipt.read_bytes())
        self.document.write_bytes(document.read_bytes())
        self.receipt.chmod(0o600)
        self.document.chmod(0o600)

    def __enter__(self) -> "MetadataCopies":
        return self

    def __exit__(self, exc_type: object, exc: object, traceback: object) -> None:
        self._temporary.cleanup()


@dataclasses.dataclass
class _FakePins:
    settings_size: int = BUILDER.EXPECTED_SAVE_SETTINGS_SIZE
    settings_sha256: str = BUILDER.EXPECTED_SAVE_SETTINGS_SHA256


class _FakeProfileRefusal(RuntimeError):
    pass


class _FakePublishedProfileQuarantined(_FakeProfileRefusal):
    pass


class _FakePublishedProfileDurabilityUnconfirmed(_FakeProfileRefusal):
    pass


class _FakePublishedProfileQuarantineFailed(_FakeProfileRefusal):
    pass


class _FakePublishedProfileQuarantineDurabilityUnconfirmed(_FakeProfileRefusal):
    pass


class _FakePrivateStageCleanupFailed(_FakeProfileRefusal):
    pass


class _FakePrivateStageCleanupDurabilityUnconfirmed(_FakeProfileRefusal):
    pass


class FakeBuilder:
    SOURCE_APP_PATH = pathlib.Path("/private/fake/DuckStation.app")
    BIOS_PATH = pathlib.Path("/private/fake/bios.bin")
    CANONICAL_SETTINGS = b"save-settings"
    ProfileRefusal = _FakeProfileRefusal
    PublishedProfileQuarantined = _FakePublishedProfileQuarantined
    PublishedProfileDurabilityUnconfirmed = _FakePublishedProfileDurabilityUnconfirmed
    PublishedProfileQuarantineFailed = _FakePublishedProfileQuarantineFailed
    PublishedProfileQuarantineDurabilityUnconfirmed = (
        _FakePublishedProfileQuarantineDurabilityUnconfirmed
    )
    PrivateStageCleanupFailed = _FakePrivateStageCleanupFailed
    PrivateStageCleanupDurabilityUnconfirmed = (
        _FakePrivateStageCleanupDurabilityUnconfirmed
    )

    def __init__(self, behavior: str = "success") -> None:
        self.behavior = behavior
        self.repins: list[object] = []
        self.closed: list[object] = []
        self.validations = 0
        self.receipt = object()
        self.document = object()

    def _repin_held_file(self, handle: object, label: str) -> None:
        self.repins.append((handle, label))

    def _close_pinned_file(self, handle: object) -> None:
        self.closed.append(handle)

    def _validate_settings(self, raw: bytes, pins: object) -> None:
        self.validations += 1

    @staticmethod
    def _result(destination: pathlib.Path) -> object:
        return types.SimpleNamespace(
            destination=destination,
            receipt_sha256="1" * 64,
            receipt_schema="spyro-id65-unattended-external-home-handoff@1",
            app_manifest_sha256="2" * 64,
            settings_sha256=BUILDER.EXPECTED_SAVE_SETTINGS_SHA256,
            launch_gate=BUILDER.LAUNCH_GATE,
        )

    def build_profile(
        self,
        source: pathlib.Path,
        destination: pathlib.Path,
        cue: pathlib.Path,
        binary: pathlib.Path,
        bios: pathlib.Path,
        pins: object,
        *,
        hook: Any,
    ) -> object:
        if self.behavior == "quarantined":
            raise _FakePublishedProfileQuarantined("fake exact-inode quarantine")
        checkpoints = BUILDER.BUILD_HOOK_CHECKPOINTS
        if self.behavior == "incomplete":
            checkpoints = checkpoints[:-1]
        for checkpoint in checkpoints:
            hook(types.SimpleNamespace(checkpoint=checkpoint))
        return self._result(destination)

    def verify_profile(
        self,
        source: pathlib.Path,
        destination: pathlib.Path,
        cue: pathlib.Path,
        binary: pathlib.Path,
        bios: pathlib.Path,
        pins: object,
    ) -> object:
        return self._result(destination)


class AggregateSaveProfileTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.destination = (
            REPOSITORY_ROOT / BUILDER.PROFILE_DESTINATION_RELATIVE_PATH
        )
        cls.destination_existed = os.path.lexists(cls.destination)
        _, cls.cue, cls.binary, cls.receipt, cls.document = BUILDER._paths(
            REPOSITORY_ROOT
        )
        cls.production_snapshot = {
            "cue": _file_identity(cls.cue),
            "receipt": _file_identity(cls.receipt),
            "document": _file_identity(cls.document),
        }

    @classmethod
    def tearDownClass(cls) -> None:
        if os.path.lexists(cls.destination) != cls.destination_existed:
            raise TestHarnessFailure(
                "offline tests changed the real save-profile destination."
            )
        current = {
            "cue": _file_identity(cls.cue),
            "receipt": _file_identity(cls.receipt),
            "document": _file_identity(cls.document),
        }
        if current != cls.production_snapshot:
            raise TestHarnessFailure(
                "offline tests changed a production aggregate input."
            )

    def test_01_exact_namespace_and_source_pins(self) -> None:
        self.assertEqual(
            BUILDER.PROFILE_ID,
            "duckstation-id65-authoring-aggregate-save-v1",
        )
        self.assertEqual(
            BUILDER.EXPECTED_FROZEN_BUILDER_SHA256,
            _sha256_bytes((REPOSITORY_ROOT / BUILDER.FROZEN_BUILDER_RELATIVE_PATH).read_bytes()),
        )
        self.assertEqual(
            VERIFIER.EXPECTED_FROZEN_VERIFIER_SHA256,
            _sha256_bytes((REPOSITORY_ROOT / VERIFIER.FROZEN_VERIFIER_RELATIVE_PATH).read_bytes()),
        )
        self.assertEqual(
            VERIFIER.EXPECTED_SAVE_BUILDER_ADAPTER_SHA256,
            EXPECTED_BUILDER_SHA256,
        )

    def test_02_one_exact_settings_replacement_and_reverse_projection(self) -> None:
        frozen = BUILDER._load_frozen_builder(REPOSITORY_ROOT)
        original = frozen.CANONICAL_SETTINGS
        original_validator = frozen._validate_settings
        pins = BUILDER._save_build_pins(frozen)
        save = frozen.CANONICAL_SETTINGS
        self.assertEqual(original.count(BUILDER.NO_CARD_LINE), 1)
        self.assertEqual(save.count(BUILDER.SAVE_CARD_LINE), 1)
        self.assertEqual(save.count(BUILDER.NO_CARD_LINE), 0)
        self.assertEqual(
            save.replace(BUILDER.SAVE_CARD_LINE, BUILDER.NO_CARD_LINE, 1),
            original,
        )
        original_validator(original, dataclasses.replace(
            pins,
            settings_size=BUILDER.EXPECTED_NO_CARD_SETTINGS_SIZE,
            settings_sha256=BUILDER.EXPECTED_NO_CARD_SETTINGS_SHA256,
        ))
        self.assertEqual(
            dict(frozen._parse_ini(save)["MemoryCards"]),
            {
                "Card1Type": "PerGameTitle",
                "Card2Type": "None",
                "Directory": "memcards",
            },
        )

    def test_03_settings_reject_original_duplicate_and_unrelated_change(self) -> None:
        frozen = BUILDER._load_frozen_builder(REPOSITORY_ROOT)
        original = frozen.CANONICAL_SETTINGS
        pins = BUILDER._save_build_pins(frozen)
        cases = (
            original,
            frozen.CANONICAL_SETTINGS.replace(
                BUILDER.SAVE_CARD_LINE,
                BUILDER.SAVE_CARD_LINE + BUILDER.SAVE_CARD_LINE,
            ),
            frozen.CANONICAL_SETTINGS.replace(
                b"SaveStateOnExit = false", b"SaveStateOnExit = true"
            ),
            frozen.CANONICAL_SETTINGS.replace(
                b"EnableTextureReplacements = false",
                b"EnableTextureReplacements = true",
            ),
        )
        for raw in cases:
            with self.subTest(sha256=_sha256_bytes(raw)):
                with self.assertRaises(BUILDER.AggregateSaveProfileRefusal):
                    frozen._validate_settings(
                        raw,
                        dataclasses.replace(
                            pins,
                            settings_size=len(raw),
                            settings_sha256=_sha256_bytes(raw),
                        ),
                    )

    def test_04_root_and_hash_drift_fail_closed(self) -> None:
        with self.assertRaises(BUILDER.AggregateSaveProfileRefusal):
            BUILDER._require_repository_root(pathlib.Path("."))
        with self.assertRaises(BUILDER.AggregateSaveProfileRefusal):
            BUILDER._stable_read_source(
                REPOSITORY_ROOT / BUILDER.FROZEN_BUILDER_RELATIVE_PATH,
                "0" * 64,
                "frozen builder test",
            )

    def test_05_semantically_false_metadata_is_rejected_even_if_rehashed(self) -> None:
        frozen = BUILDER._load_frozen_builder(REPOSITORY_ROOT)
        with MetadataCopies() as copies:
            payload = json.loads(copies.receipt.read_bytes())
            payload["runtimeProven"] = True
            raw = json.dumps(payload, separators=(",", ":")).encode("utf-8")
            copies.receipt.write_bytes(raw)
            copies.receipt.chmod(0o600)
            with (
                mock.patch.object(BUILDER, "EXPECTED_CANDIDATE_RECEIPT_FILE_SIZE", len(raw)),
                mock.patch.object(
                    BUILDER,
                    "EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256",
                    _sha256_bytes(raw),
                ),
                self.assertRaisesRegex(
                    BUILDER.AggregateSaveProfileRefusal,
                    "runtimeProven",
                ),
            ):
                BUILDER._require_aggregate_metadata(
                    frozen, copies.receipt, copies.document
                )

    def _run_with_fake(self, fake: FakeBuilder) -> dict[str, object]:
        with (
            mock.patch.object(BUILDER, "_load_frozen_builder", return_value=fake),
            mock.patch.object(BUILDER, "_save_build_pins", return_value=_FakePins()),
            mock.patch.object(
                BUILDER,
                "_require_aggregate_metadata",
                return_value=(fake.receipt, fake.document),
            ),
        ):
            return BUILDER._run_profile_operation("build", REPOSITORY_ROOT)

    def test_06_build_hook_holds_inputs_at_all_publication_boundaries(self) -> None:
        fake = FakeBuilder()
        result = self._run_with_fake(fake)
        self.assertTrue(result["profileBuilt"])
        self.assertFalse(result["duckStationLaunched"])
        self.assertFalse(result["runtimeSaveProven"])
        self.assertEqual(len(fake.repins), 10)
        self.assertEqual(fake.closed, [fake.document, fake.receipt])
        self.assertEqual(fake.validations, 5)

    def test_07_incomplete_hook_closure_reports_published_binding_unconfirmed(self) -> None:
        with self.assertRaises(
            BUILDER.AggregateSaveProfilePublishedBindingUnconfirmed
        ) as caught:
            self._run_with_fake(FakeBuilder("incomplete"))
        self.assertEqual(caught.exception.exit_code, 9)

    def test_08_frozen_quarantine_semantics_preserve_distinct_exit(self) -> None:
        with self.assertRaises(BUILDER.AggregateSaveProfileOperationRefusal) as caught:
            self._run_with_fake(FakeBuilder("quarantined"))
        self.assertEqual(caught.exception.exit_code, 3)
        self.assertIn("quarantine", str(caught.exception))

    def test_09_verifier_pins_are_all_or_none_and_no_save_proof(self) -> None:
        VERIFIER._validate_output_pin_set(allow_pending=True)
        status = VERIFIER._status()
        self.assertTrue(status["profileOutputPinsReady"])
        self.assertTrue(status["prelaunchVerificationAvailable"])
        self.assertFalse(status["runtimeSaveProven"])
        self.assertFalse(status["controlledRuntimeCardAccepted"])
        self.assertFalse(status["postRunSnapshotImplemented"])
        self.assertFalse(status["postRunFinalizerImplemented"])
        partial = dict(VERIFIER.EXPECTED_PROFILE_OUTPUT_PINS)
        partial["receiptSize"] = VERIFIER.PENDING
        with (
            mock.patch.object(VERIFIER, "EXPECTED_PROFILE_OUTPUT_PINS", partial),
            self.assertRaisesRegex(
                VERIFIER.AggregateSaveProfileVerificationRefusal,
                "partially populated",
            ),
        ):
            VERIFIER._validate_output_pin_set(allow_pending=True)
        all_pending = {
            name: VERIFIER.PENDING
            for name in VERIFIER.EXPECTED_PROFILE_OUTPUT_PINS
        }
        with mock.patch.object(
            VERIFIER, "EXPECTED_PROFILE_OUTPUT_PINS", all_pending
        ):
            VERIFIER._validate_output_pin_set(allow_pending=True)
            pending_status = VERIFIER._status()
            self.assertFalse(pending_status["profileOutputPinsReady"])
            self.assertFalse(pending_status["prelaunchVerificationAvailable"])
            self.assertFalse(pending_status["runtimeSaveProven"])

    def test_10_prelaunch_rejects_any_card_artifact(self) -> None:
        VERIFIER._require_prelaunch_snapshot_has_no_cards({
            "dataFiles": [{"relativePath": "settings.ini"}]
        })
        with self.assertRaisesRegex(
            VERIFIER.AggregateSaveProfileVerificationRefusal,
            "memory-card artifact",
        ):
            VERIFIER._require_prelaunch_snapshot_has_no_cards({
                "dataFiles": [{"relativePath": "memcards/SCUS-94228_1.mcd"}]
            })

    def test_11_verify_prelaunch_accepts_only_the_pinned_empty_profile(self) -> None:
        all_pending = {
            name: VERIFIER.PENDING
            for name in VERIFIER.EXPECTED_PROFILE_OUTPUT_PINS
        }
        stdout = io.StringIO()
        stderr = io.StringIO()
        with (
            mock.patch.object(
                VERIFIER, "EXPECTED_PROFILE_OUTPUT_PINS", all_pending
            ),
            contextlib.redirect_stdout(stdout),
            contextlib.redirect_stderr(stderr),
        ):
            exit_code = VERIFIER.main([
                "verify-prelaunch",
                "--repository-root",
                os.fspath(REPOSITORY_ROOT),
            ])
        self.assertEqual(exit_code, 2)
        self.assertEqual(stdout.getvalue(), "")
        self.assertIn("output pins are PENDING", stderr.getvalue())
        self.assertEqual(os.path.lexists(self.destination), self.destination_existed)

        stdout = io.StringIO()
        stderr = io.StringIO()
        with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            exit_code = VERIFIER.main([
                "verify-prelaunch",
                "--repository-root",
                os.fspath(REPOSITORY_ROOT),
            ])
        self.assertEqual(exit_code, 0)
        self.assertEqual(stderr.getvalue(), "")
        preflight = json.loads(stdout.getvalue())
        self.assertEqual(preflight["status"], "PREFLIGHT_READY")
        self.assertTrue(preflight["prelaunchMemcardsEmpty"])
        self.assertFalse(preflight["duckStationLaunched"])
        self.assertFalse(preflight["runtimeSaveProven"])
        self.assertFalse(preflight["controlledRuntimeCardAccepted"])
        self.assertEqual(os.path.lexists(self.destination), self.destination_existed)

    def test_12_tools_have_no_process_launch_surface(self) -> None:
        forbidden_imports = {"subprocess"}
        forbidden_attributes = {
            ("os", "system"), ("os", "popen"), ("os", "startfile"),
            ("os", "execv"), ("os", "execve"), ("os", "spawnv"),
            ("os", "spawnve"),
        }
        for path in (BUILDER_PATH, VERIFIER_PATH):
            tree = ast.parse(path.read_bytes(), filename=os.fspath(path))
            imports = {
                alias.name.split(".", 1)[0]
                for node in ast.walk(tree)
                if isinstance(node, (ast.Import, ast.ImportFrom))
                for alias in (
                    node.names if isinstance(node, ast.Import) else [
                        ast.alias(name=node.module or "")
                    ]
                )
            }
            self.assertTrue(forbidden_imports.isdisjoint(imports), path.name)
            observed_attributes = {
                (node.value.id, node.attr)
                for node in ast.walk(tree)
                if isinstance(node, ast.Attribute)
                and isinstance(node.value, ast.Name)
            }
            self.assertTrue(
                forbidden_attributes.isdisjoint(observed_attributes), path.name
            )


def main() -> int:
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(
        AggregateSaveProfileTests
    )
    stream = io.StringIO()
    result = unittest.TextTestRunner(stream=stream, verbosity=2).run(suite)
    if not result.wasSuccessful():
        print(stream.getvalue(), file=sys.stderr, end="")
        return 1
    print(
        "ID65_AUTHORING_AGGREGATE_SAVE_PROFILE_OFFLINE_TEST_OK "
        f"tests={result.testsRun} "
        f"builder={EXPECTED_BUILDER_SHA256} "
        f"verifier={EXPECTED_VERIFIER_SHA256} "
        f"settings={BUILDER.EXPECTED_SAVE_SETTINGS_SHA256} "
        "realProfileBuilt=true prelaunchProfileVerified=true "
        "duckStationLaunched=false runtimeSaveProven=false "
        "postRunSnapshotImplemented=false postRunFinalizerImplemented=false"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
