#!/usr/bin/env python3
"""Fail-closed prelaunch verifier for the save-capable ID65 aggregate profile.

The frozen V3 verifier, frozen V3 builder, and save-profile adapter are loaded
from stable hash-pinned source bytes.  This verifier proves only an immutable
prelaunch profile whose memcards directory is empty.  A card produced during a
real run is deliberately outside this contract and needs a separate held-file
post-run snapshot plus a separate finalizer before any save claim can be made.
"""

from __future__ import annotations

import argparse
import dataclasses
import hashlib
import json
import os
import pathlib
import stat
import sys
import types
from typing import Any, Final


class AggregateSaveProfileVerificationRefusal(RuntimeError):
    """The save-capable aggregate profile identity could not be proven exactly."""


SCRIPT_PATH: Final = pathlib.Path(os.path.abspath(__file__))
PRODUCTION_REPOSITORY_ROOT: Final = SCRIPT_PATH.parent.parent
SAVE_BUILDER_ADAPTER_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "tools/Build-SpyroId65AuthoringAggregateSaveV1Profile.py"
)
EXPECTED_SAVE_BUILDER_ADAPTER_SHA256: Final = (
    "759ec6a561166fc3e879db58db56781f757735b0975a5db0768248f276aec297"
)
FROZEN_VERIFIER_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "tools/Verify-SpyroId65UnattendedProfile.py"
)
EXPECTED_FROZEN_VERIFIER_SHA256: Final = (
    "74f4ed279a3e3f60af6c4c0a0b1fa8ffc0a67886c478d8cc7d1a288b093c3671"
)
FROZEN_BUILDER_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "tools/Build-SpyroId65UnattendedProfile.py"
)
EXPECTED_FROZEN_BUILDER_SHA256: Final = (
    "0591f1b6bf3f61dac4bfa61d407df65a2a9862569c8d2a7678d216b95929b95c"
)
MAX_SOURCE_BYTES: Final = 512 * 1024
READ_CHUNK_SIZE: Final = 1024 * 1024

PROFILE_ID: Final = "duckstation-id65-authoring-aggregate-save-v1"
PROFILE_DESTINATION_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "_local/v5-stone-hill-level-replacement/"
    "duckstation-id65-authoring-aggregate-save-v1"
)
PENDING: Final = "PENDING"

# Frozen from the first audited no-launch build. A partially populated pin set
# is always refused.
EXPECTED_PROFILE_OUTPUT_PINS: Final[dict[str, int | str]] = {
    "receiptSize": 74_525,
    "receiptSha256": (
        "17ce1f4fd9394bf4890048f08afe28d97a709fd656e7d262e34553f5cd6e58d3"
    ),
    "receiptClosureSha256": (
        "7b43c1c81e4ca40148c99c2c0d908891453e2882c9f93b0d0f9d440c52cb5330"
    ),
    "appOutputTreeEntryCount": 292,
    "appOutputTreeManifestSha256": (
        "08d0a74450b709ab7e702182d64db55f6bc3395728181b052fec307c4d320ba7"
    ),
    "homeOutputTreeEntryCount": 12,
    "homeOutputTreeManifestSha256": (
        "5e3bcd2c9c3bd8b457bbfe613244663d545b90dbc072b132e1e4aee1e3e94cb9"
    ),
    "packageEnvelopeEntryCount": 4,
    "packageEnvelopeSha256": (
        "7544e65f34c99d4d78677321ea38e992310386843ddf933f31fce514e2937872"
    ),
    "candidateManifestSha256": (
        "44f74dcefab535670d4abe0e1b1bcec35b832dec421d9caea8c6c132b860681d"
    ),
}

PREFLIGHT_FORMAT: Final = "spyro-id65-authoring-aggregate-save-profile-preflight"
PREFLIGHT_FORMAT_VERSION: Final = 1
STATUS_FORMAT: Final = "spyro-id65-authoring-aggregate-save-profile-status"
STATUS_FORMAT_VERSION: Final = 1
LAUNCH_GATE: Final = "EXTERNAL_RUNTIME_LAUNCH_REQUIRED"
POST_RUN_CARD_BOUNDARY: Final = "SEPARATE_HELD_SNAPSHOT_AND_FINALIZER_REQUIRED"


@dataclasses.dataclass
class _AggregateInputHandles:
    cue: Any
    binary: Any
    candidate_receipt: Any
    document: Any


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _canonical_json(value: object) -> str:
    return json.dumps(
        value,
        allow_nan=False,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    )


def _require_absolute_normalized(path: pathlib.Path, label: str) -> pathlib.Path:
    if (
        not isinstance(path, pathlib.Path)
        or not path.is_absolute()
        or pathlib.Path(os.path.normpath(os.fspath(path))) != path
    ):
        raise AggregateSaveProfileVerificationRefusal(
            f"{label} must be an explicit absolute normalized pathlib.Path."
        )
    return path


def _reject_symlink_segments(path: pathlib.Path, label: str) -> None:
    _require_absolute_normalized(path, label)
    current = pathlib.Path(path.anchor)
    for segment in path.parts[1:]:
        current /= segment
        try:
            metadata = os.lstat(current)
        except FileNotFoundError as exc:
            raise AggregateSaveProfileVerificationRefusal(
                f"{label} has a missing path segment."
            ) from exc
        if stat.S_ISLNK(metadata.st_mode):
            raise AggregateSaveProfileVerificationRefusal(
                f"{label} traverses a symbolic link."
            )


def _require_repository_root(value: pathlib.Path) -> pathlib.Path:
    root = _require_absolute_normalized(value, "repository root")
    _reject_symlink_segments(root, "repository root")
    if root != PRODUCTION_REPOSITORY_ROOT.resolve(strict=True):
        raise AggregateSaveProfileVerificationRefusal(
            "repository root must be the audited checkout containing this verifier."
        )
    return root


def _stable_read_source(
    path: pathlib.Path,
    expected_sha256: str,
    label: str,
) -> bytes:
    _reject_symlink_segments(path, label)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    try:
        before = os.fstat(fd)
        if (
            not stat.S_ISREG(before.st_mode)
            or before.st_uid != os.geteuid()
            or before.st_gid != os.getegid()
            or stat.S_IMODE(before.st_mode) & 0o022
            or before.st_nlink != 1
            or before.st_size > MAX_SOURCE_BYTES
        ):
            raise AggregateSaveProfileVerificationRefusal(
                f"{label} metadata is unsafe."
            )
        chunks: list[bytes] = []
        digest = hashlib.sha256()
        observed = 0
        while True:
            chunk = os.read(fd, READ_CHUNK_SIZE)
            if not chunk:
                break
            chunks.append(chunk)
            digest.update(chunk)
            observed += len(chunk)
        after = os.fstat(fd)
        fields = (
            "st_dev", "st_ino", "st_mode", "st_uid", "st_gid", "st_nlink",
            "st_size", "st_mtime_ns", "st_ctime_ns",
        )
        if observed != before.st_size or any(
            getattr(before, name) != getattr(after, name) for name in fields
        ):
            raise AggregateSaveProfileVerificationRefusal(
                f"{label} changed while read."
            )
        observed_sha256 = digest.hexdigest()
        if observed_sha256 != expected_sha256:
            raise AggregateSaveProfileVerificationRefusal(
                f"{label} hash changed: {observed_sha256}."
            )
        return b"".join(chunks)
    finally:
        os.close(fd)


def _execute_module(
    source: bytes,
    path: pathlib.Path,
    module_name: str,
) -> types.ModuleType:
    module = types.ModuleType(module_name)
    module.__file__ = os.fspath(path)
    module.__package__ = None
    previous = sys.modules.get(module_name)
    sys.modules[module_name] = module
    try:
        exec(compile(source, os.fspath(path), "exec", dont_inherit=True), module.__dict__)
    except Exception:
        if previous is None:
            sys.modules.pop(module_name, None)
        else:
            sys.modules[module_name] = previous
        raise
    return module


def _load_save_builder_adapter(repository_root: pathlib.Path) -> types.ModuleType:
    root = _require_repository_root(repository_root)
    path = root / SAVE_BUILDER_ADAPTER_RELATIVE_PATH
    source = _stable_read_source(
        path,
        EXPECTED_SAVE_BUILDER_ADAPTER_SHA256,
        "save profile builder adapter",
    )
    module = _execute_module(
        source, path, "_spyro_id65_authoring_aggregate_save_v1_builder_adapter"
    )
    required = (
        "_load_frozen_builder", "_save_build_pins", "_paths",
        "_require_aggregate_metadata", "EXPECTED_FROZEN_BUILDER_SHA256",
        "FROZEN_BUILDER_RELATIVE_PATH", "PROFILE_ID",
        "PROFILE_DESTINATION_RELATIVE_PATH", "EXPECTED_SAVE_SETTINGS_SIZE",
        "EXPECTED_SAVE_SETTINGS_SHA256", "EXPECTED_NO_CARD_SETTINGS_SHA256",
    )
    missing = [name for name in required if not hasattr(module, name)]
    if missing:
        raise AggregateSaveProfileVerificationRefusal(
            f"save profile builder adapter API changed: {','.join(missing)}."
        )
    if (
        module.EXPECTED_FROZEN_BUILDER_SHA256 != EXPECTED_FROZEN_BUILDER_SHA256
        or module.FROZEN_BUILDER_RELATIVE_PATH != FROZEN_BUILDER_RELATIVE_PATH
        or module.PROFILE_ID != PROFILE_ID
        or module.PROFILE_DESTINATION_RELATIVE_PATH
        != PROFILE_DESTINATION_RELATIVE_PATH
    ):
        raise AggregateSaveProfileVerificationRefusal(
            "save profile builder adapter identity changed."
        )
    return module


def _load_frozen_verifier(repository_root: pathlib.Path) -> types.ModuleType:
    root = _require_repository_root(repository_root)
    path = root / FROZEN_VERIFIER_RELATIVE_PATH
    source = _stable_read_source(
        path, EXPECTED_FROZEN_VERIFIER_SHA256, "frozen profile verifier"
    )
    module = _execute_module(
        source, path, "_spyro_id65_frozen_verifier_for_aggregate_save_v1"
    )
    required = (
        "ProfilePins", "PRODUCTION_PINS", "ProfileVerificationRefusal",
        "_read_receipt_only", "_capture_runtime_snapshot_with_builder",
        "EXPECTED_BUILDER_SHA256", "BUILDER_RELATIVE_PATH",
    )
    missing = [name for name in required if not hasattr(module, name)]
    if missing:
        raise AggregateSaveProfileVerificationRefusal(
            f"frozen verifier API changed: {','.join(missing)}."
        )
    if (
        module.EXPECTED_BUILDER_SHA256 != EXPECTED_FROZEN_BUILDER_SHA256
        or module.BUILDER_RELATIVE_PATH != FROZEN_BUILDER_RELATIVE_PATH
    ):
        raise AggregateSaveProfileVerificationRefusal(
            "frozen verifier no longer binds the expected frozen builder."
        )
    return module


def _validate_output_pin_set(*, allow_pending: bool) -> None:
    expected_keys = {
        "receiptSize", "receiptSha256", "receiptClosureSha256",
        "appOutputTreeEntryCount", "appOutputTreeManifestSha256",
        "homeOutputTreeEntryCount", "homeOutputTreeManifestSha256",
        "packageEnvelopeEntryCount", "packageEnvelopeSha256",
        "candidateManifestSha256",
    }
    if set(EXPECTED_PROFILE_OUTPUT_PINS) != expected_keys:
        raise AggregateSaveProfileVerificationRefusal(
            "save profile output-pin key closure changed."
        )
    pending = sorted(
        name for name, value in EXPECTED_PROFILE_OUTPUT_PINS.items()
        if value == PENDING
    )
    if pending:
        if len(pending) != len(expected_keys):
            raise AggregateSaveProfileVerificationRefusal(
                "save profile output pins are partially populated; replace the "
                "complete audited set atomically."
            )
        if allow_pending:
            return
        raise AggregateSaveProfileVerificationRefusal(
            "save profile output pins are PENDING: " + ", ".join(pending) + "."
        )
    integer_names = {
        "receiptSize", "appOutputTreeEntryCount", "homeOutputTreeEntryCount",
        "packageEnvelopeEntryCount",
    }
    for name, value in EXPECTED_PROFILE_OUTPUT_PINS.items():
        if name in integer_names:
            if not isinstance(value, int) or isinstance(value, bool) or value <= 0:
                raise AggregateSaveProfileVerificationRefusal(
                    f"save profile output pin is not a positive integer: {name}."
                )
        elif not (
            isinstance(value, str)
            and len(value) == 64
            and all(character in "0123456789abcdef" for character in value)
        ):
            raise AggregateSaveProfileVerificationRefusal(
                f"save profile output pin is not canonical SHA-256: {name}."
            )


def _ready_output_pin(name: str) -> int | str:
    _validate_output_pin_set(allow_pending=False)
    return EXPECTED_PROFILE_OUTPUT_PINS[name]


def _profile_pins(
    verifier: types.ModuleType,
    save_adapter: types.ModuleType,
    builder: types.ModuleType,
    build_pins: Any,
) -> Any:
    _validate_output_pin_set(allow_pending=False)
    base = verifier.PRODUCTION_PINS
    if (
        base.app_leaf != builder.PACKAGED_APP_LEAF
        or base.app_version != build_pins.app_version
        or base.executable_size != build_pins.executable_size
        or base.executable_sha256 != build_pins.executable_sha256
        or base.app_content_manifest_entry_count != build_pins.app_manifest_entry_count
        or base.app_content_manifest_sha256 != build_pins.app_manifest_sha256
        or base.bios_size != build_pins.bios_size
        or base.bios_sha256 != build_pins.bios_sha256
        or base.source_app_path != builder.SOURCE_APP_PATH
        or base.source_bios_path != builder.BIOS_PATH
    ):
        raise AggregateSaveProfileVerificationRefusal(
            "frozen verifier and customized builder immutable inputs diverged."
        )
    pins = dataclasses.replace(
        base,
        profile_id=PROFILE_ID,
        package_relative_path=PROFILE_DESTINATION_RELATIVE_PATH,
        receipt_size=_ready_output_pin("receiptSize"),
        receipt_sha256=_ready_output_pin("receiptSha256"),
        receipt_closure_sha256=_ready_output_pin("receiptClosureSha256"),
        app_output_tree_entry_count=_ready_output_pin("appOutputTreeEntryCount"),
        app_output_tree_manifest_sha256=_ready_output_pin(
            "appOutputTreeManifestSha256"
        ),
        home_output_tree_entry_count=_ready_output_pin("homeOutputTreeEntryCount"),
        home_output_tree_manifest_sha256=_ready_output_pin(
            "homeOutputTreeManifestSha256"
        ),
        package_envelope_entry_count=_ready_output_pin("packageEnvelopeEntryCount"),
        package_envelope_sha256=_ready_output_pin("packageEnvelopeSha256"),
        settings_size=save_adapter.EXPECTED_SAVE_SETTINGS_SIZE,
        settings_sha256=save_adapter.EXPECTED_SAVE_SETTINGS_SHA256,
        candidate_cue_relative_path=save_adapter.CANDIDATE_CUE_RELATIVE_PATH,
        candidate_cue_size=save_adapter.EXPECTED_CANDIDATE_CUE_SIZE,
        candidate_cue_sha256=save_adapter.EXPECTED_CANDIDATE_CUE_SHA256,
        candidate_bin_relative_path=save_adapter.CANDIDATE_BIN_RELATIVE_PATH,
        candidate_bin_size=save_adapter.EXPECTED_CANDIDATE_BIN_SIZE,
        candidate_bin_sha256=save_adapter.EXPECTED_CANDIDATE_BIN_SHA256,
        candidate_manifest_sha256=_ready_output_pin("candidateManifestSha256"),
    )
    if not isinstance(pins, verifier.ProfilePins):
        raise AggregateSaveProfileVerificationRefusal(
            "save verifier pin type changed."
        )
    return pins


def _open_aggregate_inputs(
    save_adapter: types.ModuleType,
    builder: types.ModuleType,
    repository_root: pathlib.Path,
) -> _AggregateInputHandles:
    _, cue_path, binary_path, receipt_path, document_path = save_adapter._paths(
        repository_root
    )
    opened: list[Any] = []
    try:
        cue = builder._open_pinned_file(
            cue_path, "aggregate candidate CUE",
            expected_name=f"{save_adapter.CANDIDATE_STEM}.cue",
            expected_size=save_adapter.EXPECTED_CANDIDATE_CUE_SIZE,
            expected_sha256=save_adapter.EXPECTED_CANDIDATE_CUE_SHA256,
        )
        opened.append(cue)
        binary = builder._open_pinned_file(
            binary_path, "aggregate candidate BIN",
            expected_name=f"{save_adapter.CANDIDATE_STEM}.bin",
            expected_size=save_adapter.EXPECTED_CANDIDATE_BIN_SIZE,
            expected_sha256=save_adapter.EXPECTED_CANDIDATE_BIN_SHA256,
        )
        opened.append(binary)
        builder._validate_cue_pair(cue, binary)
        candidate_receipt, document = save_adapter._require_aggregate_metadata(
            builder, receipt_path, document_path
        )
        opened.extend((candidate_receipt, document))
        handles = _AggregateInputHandles(cue, binary, candidate_receipt, document)
        _repin_aggregate_inputs(builder, handles)
        return handles
    except Exception:
        for handle in reversed(opened):
            builder._close_pinned_file(handle)
        raise


def _repin_aggregate_inputs(
    builder: types.ModuleType,
    handles: _AggregateInputHandles,
) -> None:
    for handle, label in (
        (handles.cue, "aggregate candidate CUE"),
        (handles.binary, "aggregate candidate BIN"),
        (handles.candidate_receipt, "aggregate candidate receipt"),
        (handles.document, "aggregate authoring document"),
    ):
        builder._repin_held_file(handle, label)
    builder._validate_cue_pair(handles.cue, handles.binary)


def _close_aggregate_inputs(
    builder: types.ModuleType,
    handles: _AggregateInputHandles | None,
) -> None:
    if handles is None:
        return
    for handle in (
        handles.document, handles.candidate_receipt, handles.binary, handles.cue
    ):
        builder._close_pinned_file(handle)


def _require_prelaunch_snapshot_has_no_cards(snapshot: dict[str, object]) -> None:
    data_files = snapshot.get("dataFiles")
    if not isinstance(data_files, list):
        raise AggregateSaveProfileVerificationRefusal(
            "prelaunch snapshot dataFiles is malformed."
        )
    for entry in data_files:
        if not isinstance(entry, dict) or not isinstance(
            entry.get("relativePath"), str
        ):
            raise AggregateSaveProfileVerificationRefusal(
                "prelaunch snapshot data-file record is malformed."
            )
        relative = entry["relativePath"]
        if relative == "memcards" or relative.startswith("memcards/"):
            raise AggregateSaveProfileVerificationRefusal(
                "prelaunch verifier found a memory-card artifact."
            )


def _verify_prelaunch(repository_root: pathlib.Path) -> dict[str, object]:
    root = _require_repository_root(repository_root)
    save_adapter = _load_save_builder_adapter(root)
    verifier = _load_frozen_verifier(root)
    builder = save_adapter._load_frozen_builder(root)
    build_pins = save_adapter._save_build_pins(builder)
    pins = _profile_pins(verifier, save_adapter, builder, build_pins)
    destination, cue, binary, _, _ = save_adapter._paths(root)
    handles: _AggregateInputHandles | None = None
    try:
        handles = _open_aggregate_inputs(save_adapter, builder, root)
        try:
            receipt_before = verifier._read_receipt_only(builder, root, pins)
            result = builder.verify_profile(
                builder.SOURCE_APP_PATH,
                destination,
                cue,
                binary,
                builder.BIOS_PATH,
                build_pins,
            )
            _repin_aggregate_inputs(builder, handles)
            receipt_after = verifier._read_receipt_only(builder, root, pins)
            if receipt_after != receipt_before:
                raise AggregateSaveProfileVerificationRefusal(
                    "handoff receipt changed across frozen builder verification."
                )
            expected_result = {
                "destination": destination,
                "receipt_sha256": pins.receipt_sha256,
                "receipt_schema": (
                    f"{pins.receipt_format}@{pins.receipt_format_version}"
                ),
                "app_manifest_sha256": pins.app_content_manifest_sha256,
                "settings_sha256": pins.settings_sha256,
                "launch_gate": LAUNCH_GATE,
            }
            for name, expected in expected_result.items():
                if getattr(result, name, None) != expected:
                    raise AggregateSaveProfileVerificationRefusal(
                        f"builder verification result changed: {name}."
                    )
            snapshot = verifier._capture_runtime_snapshot_with_builder(
                builder,
                root,
                phase="preflight",
                session_id="save-preflight",
                process_instance_id="not-applicable",
                captured_at_utc=None,
                pins=pins,
            )
            # The frozen snapshot verifier requires every safety directory,
            # including memcards, to be empty.  This additional projection keeps
            # that prelaunch-only fact explicit in the save-profile result.
            _require_prelaunch_snapshot_has_no_cards(snapshot)
            _repin_aggregate_inputs(builder, handles)
        except (
            AggregateSaveProfileVerificationRefusal,
            save_adapter.AggregateSaveProfileRefusal,
        ):
            raise
        except (verifier.ProfileVerificationRefusal, builder.ProfileRefusal) as exc:
            raise AggregateSaveProfileVerificationRefusal(str(exc)) from exc
    finally:
        _close_aggregate_inputs(builder, handles)

    return {
        "format": PREFLIGHT_FORMAT,
        "formatVersion": PREFLIGHT_FORMAT_VERSION,
        "status": "PREFLIGHT_READY",
        "profileId": PROFILE_ID,
        "receiptSha256": pins.receipt_sha256,
        "appContentManifestSha256": pins.app_content_manifest_sha256,
        "appOutputTreeManifestSha256": pins.app_output_tree_manifest_sha256,
        "homeOutputTreeManifestSha256": pins.home_output_tree_manifest_sha256,
        "packageEnvelopeSha256": pins.package_envelope_sha256,
        "noCardSettingsSha256": save_adapter.EXPECTED_NO_CARD_SETTINGS_SHA256,
        "settingsSha256": pins.settings_sha256,
        "memoryCard1Type": "PerGameTitle",
        "memoryCard2Type": "None",
        "memoryCardDirectory": "memcards",
        "prelaunchMemcardsEmpty": True,
        "candidateCueSha256": pins.candidate_cue_sha256,
        "candidateBinSha256": pins.candidate_bin_sha256,
        "launchGate": LAUNCH_GATE,
        "duckStationLaunched": False,
        "runtimeSaveProven": False,
        "controlledRuntimeCardAccepted": False,
        "postRunCardBoundary": POST_RUN_CARD_BOUNDARY,
        "postRunSnapshotImplemented": False,
        "postRunFinalizerImplemented": False,
        "snapshot": snapshot,
    }


def _status() -> dict[str, object]:
    _validate_output_pin_set(allow_pending=True)
    ready = not any(
        value == PENDING for value in EXPECTED_PROFILE_OUTPUT_PINS.values()
    )
    return {
        "format": STATUS_FORMAT,
        "formatVersion": STATUS_FORMAT_VERSION,
        "profileId": PROFILE_ID,
        "profileOutputPinsReady": ready,
        "prelaunchVerificationAvailable": ready,
        "prelaunchMemcardsRequiredEmpty": True,
        "memoryCard1Type": "PerGameTitle",
        "memoryCard2Type": "None",
        "memoryCardDirectory": "memcards",
        "duckStationLaunched": False,
        "runtimeSaveProven": False,
        "controlledRuntimeCardAccepted": False,
        "postRunCardBoundary": POST_RUN_CARD_BOUNDARY,
        "postRunSnapshotImplemented": False,
        "postRunFinalizerImplemented": False,
        "releaseAuthorized": False,
        "publishable": False,
    }


def _self_test(repository_root: pathlib.Path) -> None:
    root = _require_repository_root(repository_root)
    save_adapter = _load_save_builder_adapter(root)
    verifier = _load_frozen_verifier(root)
    builder = save_adapter._load_frozen_builder(root)
    pins = save_adapter._save_build_pins(builder)
    _validate_output_pin_set(allow_pending=True)
    if (
        save_adapter.PROFILE_ID != PROFILE_ID
        or pins.settings_size != save_adapter.EXPECTED_SAVE_SETTINGS_SIZE
        or pins.settings_sha256 != save_adapter.EXPECTED_SAVE_SETTINGS_SHA256
        or builder.CANONICAL_SETTINGS.count(save_adapter.SAVE_CARD_LINE) != 1
        or builder.CANONICAL_SETTINGS.count(save_adapter.NO_CARD_LINE) != 0
        or verifier.SAFETY_DIRECTORIES.count("memcards") != 1
        or POST_RUN_CARD_BOUNDARY
        != "SEPARATE_HELD_SNAPSHOT_AND_FINALIZER_REQUIRED"
    ):
        raise AggregateSaveProfileVerificationRefusal(
            "save profile verifier self-test failed."
        )
    _require_prelaunch_snapshot_has_no_cards({
        "dataFiles": [
            {"relativePath": "settings.ini"},
            {"relativePath": pins.bios_name},
        ]
    })


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Verify only the empty-memcards prelaunch state of the exact "
            "save-capable ID65 aggregate profile."
        )
    )
    parser.add_argument(
        "operation", choices=("self-test", "status", "verify-prelaunch")
    )
    parser.add_argument("--repository-root", required=True, type=pathlib.Path)
    return parser


def main(argv: list[str] | None = None) -> int:
    arguments = _build_parser().parse_args(argv)
    try:
        root = _require_repository_root(arguments.repository_root)
        if arguments.operation == "self-test":
            _self_test(root)
            print("ID65_AUTHORING_AGGREGATE_SAVE_PROFILE_VERIFIER_SELF_TEST_OK")
            return 0
        if arguments.operation == "status":
            print(_canonical_json(_status()))
            return 0
        print(_canonical_json(_verify_prelaunch(root)))
        return 0
    except AggregateSaveProfileVerificationRefusal as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        return 2
    except Exception as exc:
        print(
            f"REFUSED: unexpected save verifier failure: {type(exc).__name__}: {exc}",
            file=sys.stderr,
        )
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
