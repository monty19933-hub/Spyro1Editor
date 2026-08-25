#!/usr/bin/env python3
"""Build or verify the isolated DuckStation profile for the ID65 aggregate.

This is a no-launch adapter over the frozen, hash-pinned V3 profile builder.
It changes only the candidate identity and destination namespace.  The frozen
builder remains byte-for-byte unchanged and retains its atomic no-replace
publication, exact app/BIOS/settings checks, and common parent-directory lock.
"""

from __future__ import annotations

import argparse
import dataclasses
import hashlib
import json
import os
import pathlib
import sys
import types
from typing import Any, Final


class AggregateProfileRefusal(RuntimeError):
    """The isolated aggregate profile could not be handled exactly."""


class AggregateProfileOperationRefusal(AggregateProfileRefusal):
    """The frozen builder refused with a stable operation-specific exit."""

    def __init__(self, message: str, exit_code: int) -> None:
        super().__init__(message)
        self.exit_code = exit_code


class AggregateProfilePublishedBindingUnconfirmed(AggregateProfileOperationRefusal):
    """The profile published, but wrapper metadata changed after builder return."""

    def __init__(self, destination: pathlib.Path, reason: str) -> None:
        super().__init__(
            f"profile published at {destination}, but aggregate metadata binding "
            f"could not be reconfirmed after publication: {reason}",
            9,
        )


SCRIPT_PATH: Final = pathlib.Path(os.path.abspath(__file__))
PRODUCTION_REPOSITORY_ROOT: Final = SCRIPT_PATH.parent.parent
FROZEN_BUILDER_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "tools/Build-SpyroId65UnattendedProfile.py"
)
EXPECTED_FROZEN_BUILDER_SHA256: Final = (
    "0591f1b6bf3f61dac4bfa61d407df65a2a9862569c8d2a7678d216b95929b95c"
)

PROFILE_ID: Final = "duckstation-id65-authoring-aggregate-v1"
PROFILE_DESTINATION_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "_local/v5-stone-hill-level-replacement/"
    "duckstation-id65-authoring-aggregate-v1"
)
SHARED_PARENT_LOCK_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "_local/v5-stone-hill-level-replacement/"
    ".spyro-id65-unattended-profile.lock"
)
CANDIDATE_DIRECTORY_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "_local/id65-authoring-aggregate-workspace/_research/"
    "unusedlevel65blank/v1/materialized-candidates/"
    "aggregate-4955c54b3eb7ea5c1871205e559f5d5dda0766613e55aa0de988b92e896d5c21-"
    "receipt-d6c14861cde023e9e100fe2828227da8252c9de37c36e84d33ba4bfbd3b0516f"
)
CANDIDATE_STEM: Final = "unusedlevel65blank-authoring-aggregate-candidate-v1"
CANDIDATE_CUE_RELATIVE_PATH: Final = (
    CANDIDATE_DIRECTORY_RELATIVE_PATH / f"{CANDIDATE_STEM}.cue"
)
CANDIDATE_BIN_RELATIVE_PATH: Final = (
    CANDIDATE_DIRECTORY_RELATIVE_PATH / f"{CANDIDATE_STEM}.bin"
)
CANDIDATE_RECEIPT_RELATIVE_PATH: Final = (
    CANDIDATE_DIRECTORY_RELATIVE_PATH / f"{CANDIDATE_STEM}.receipt.json"
)
DOCUMENT_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "_local/id65-authoring-aggregate-workspace/_research/unusedlevel65blank/"
    "v1/authored-edits/unusedlevel65blank-authoring-document-v1.json"
)
EXPECTED_CANDIDATE_CUE_SHA256: Final = (
    "91c12af236642fc8ad601995cea86d22c414eb1b692a4957cc3f9fa3aedd6de8"
)
EXPECTED_CANDIDATE_CUE_SIZE: Final = 114
EXPECTED_CANDIDATE_BIN_SHA256: Final = (
    "083dd5b7e258b59010e87c3262274e4da9cb86b1a6a15e7730a8b5eeac001647"
)
EXPECTED_CANDIDATE_BIN_SIZE: Final = 661_547_040
EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256: Final = (
    "9f11a3811a54ef4932c5cb4d184877fe957f06eb003680c739d2478c6e7ed03b"
)
EXPECTED_CANDIDATE_RECEIPT_FILE_SIZE: Final = 3_064
EXPECTED_DOCUMENT_ENVELOPE_SHA256: Final = (
    "5669f5e29010009fa7a698f4eed213dbd7163bd3483ec37beba7732bda321b70"
)
EXPECTED_DOCUMENT_FILE_SIZE: Final = 3_931
EXPECTED_DOCUMENT_SHA256: Final = (
    "8f27bfc3f97238402c0b7dd59a081335ae19df1acfaefa9575d9ca7110dce808"
)
EXPECTED_AGGREGATE_PLAN_SHA256: Final = (
    "4955c54b3eb7ea5c1871205e559f5d5dda0766613e55aa0de988b92e896d5c21"
)
EXPECTED_OUTPUT_ROW80_SHA256: Final = (
    "dad25853f92e4f8300036edd85dae374672d265ba17c7b2682c6b524661b75f3"
)
EXPECTED_RECEIPT_CONTENT_SHA256: Final = (
    "d6c14861cde023e9e100fe2828227da8252c9de37c36e84d33ba4bfbd3b0516f"
)
LAUNCH_GATE: Final = "EXTERNAL_RUNTIME_LAUNCH_REQUIRED"
BUILD_HOOK_CHECKPOINTS: Final = (
    "after-initial-validation",
    "after-stage-validation",
    "before-publish",
    "after-publish",
)


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _canonical_json(value: object) -> str:
    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    )


def _require_repository_root(value: pathlib.Path) -> pathlib.Path:
    if not value.is_absolute():
        raise AggregateProfileRefusal("repository root must be absolute.")
    normalized = pathlib.Path(os.path.normpath(os.fspath(value)))
    if normalized != value:
        raise AggregateProfileRefusal("repository root must be lexically normalized.")
    try:
        actual = normalized.resolve(strict=True)
        expected = PRODUCTION_REPOSITORY_ROOT.resolve(strict=True)
    except OSError as exc:
        raise AggregateProfileRefusal(
            f"repository root cannot be resolved safely: {exc}"
        ) from exc
    if actual != expected or normalized != actual:
        raise AggregateProfileRefusal(
            "repository root must be the real checkout containing this adapter."
        )
    return actual


def _load_frozen_builder(repository_root: pathlib.Path) -> types.ModuleType:
    path = repository_root / FROZEN_BUILDER_RELATIVE_PATH
    try:
        source = path.read_bytes()
    except OSError as exc:
        raise AggregateProfileRefusal(
            f"frozen profile builder cannot be read: {exc}"
        ) from exc
    digest = _sha256_bytes(source)
    if digest != EXPECTED_FROZEN_BUILDER_SHA256:
        raise AggregateProfileRefusal(
            f"frozen profile builder hash changed: {digest}"
        )

    name = "_spyro_id65_frozen_profile_builder_for_authoring_aggregate_v1"
    module = types.ModuleType(name)
    module.__file__ = os.fspath(path)
    module.__package__ = None
    sys.modules[name] = module
    try:
        exec(compile(source, os.fspath(path), "exec"), module.__dict__)
    except Exception:
        sys.modules.pop(name, None)
        raise
    _require_builder_contract(module)
    return module


def _require_builder_contract(builder: types.ModuleType) -> None:
    required = (
        "BuildPins",
        "PRODUCTION_PINS",
        "SOURCE_APP_PATH",
        "BIOS_PATH",
        "COMMON_LOCK_LEAF",
        "build_profile",
        "verify_profile",
        "_open_pinned_file",
        "_repin_held_file",
        "_close_pinned_file",
        "ProfileRefusal",
        "PublishedProfileQuarantined",
        "PublishedProfileDurabilityUnconfirmed",
        "PublishedProfileQuarantineFailed",
        "PublishedProfileQuarantineDurabilityUnconfirmed",
        "PrivateStageCleanupFailed",
        "PrivateStageCleanupDurabilityUnconfirmed",
    )
    missing = [name for name in required if not hasattr(builder, name)]
    if missing:
        raise AggregateProfileRefusal(
            f"frozen profile builder API changed: {','.join(missing)}"
        )
    if builder.COMMON_LOCK_LEAF != SHARED_PARENT_LOCK_RELATIVE_PATH.name:
        raise AggregateProfileRefusal("frozen common profile lock identity changed.")


def _aggregate_pins(builder: types.ModuleType) -> Any:
    production = builder.PRODUCTION_PINS
    pins = dataclasses.replace(
        production,
        candidate_cue_name=f"{CANDIDATE_STEM}.cue",
        candidate_cue_sha256=EXPECTED_CANDIDATE_CUE_SHA256,
        candidate_cue_size=EXPECTED_CANDIDATE_CUE_SIZE,
        candidate_bin_name=f"{CANDIDATE_STEM}.bin",
        candidate_bin_sha256=EXPECTED_CANDIDATE_BIN_SHA256,
        candidate_bin_size=EXPECTED_CANDIDATE_BIN_SIZE,
    )
    if not isinstance(pins, builder.BuildPins):
        raise AggregateProfileRefusal("aggregate profile pins changed type.")
    return pins


def _paths(
    repository_root: pathlib.Path,
) -> tuple[pathlib.Path, pathlib.Path, pathlib.Path, pathlib.Path, pathlib.Path]:
    destination = repository_root / PROFILE_DESTINATION_RELATIVE_PATH
    cue = repository_root / CANDIDATE_CUE_RELATIVE_PATH
    binary = repository_root / CANDIDATE_BIN_RELATIVE_PATH
    receipt = repository_root / CANDIDATE_RECEIPT_RELATIVE_PATH
    document = repository_root / DOCUMENT_RELATIVE_PATH
    expected_parent = repository_root / SHARED_PARENT_LOCK_RELATIVE_PATH.parent
    if destination.parent != expected_parent or destination.name != PROFILE_ID:
        raise AggregateProfileRefusal("aggregate profile destination identity changed.")
    return destination, cue, binary, receipt, document


def _read_held_json(
    handle: Any,
    expected_size: int,
    label: str,
) -> dict[str, object]:
    os.lseek(handle.fd, 0, os.SEEK_SET)
    raw = os.read(handle.fd, expected_size + 1)
    os.lseek(handle.fd, 0, os.SEEK_SET)
    if len(raw) != expected_size:
        raise AggregateProfileRefusal(f"{label} byte length changed while held.")
    try:
        parsed = json.loads(raw)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise AggregateProfileRefusal(f"{label} is not strict JSON: {exc}") from exc
    if not isinstance(parsed, dict):
        raise AggregateProfileRefusal(f"{label} root must be an object.")
    return parsed


def _require_aggregate_metadata(
    builder: types.ModuleType,
    receipt_path: pathlib.Path,
    document_path: pathlib.Path,
) -> tuple[Any, Any]:
    receipt = builder._open_pinned_file(
        receipt_path,
        "aggregate candidate receipt",
        expected_name=f"{CANDIDATE_STEM}.receipt.json",
        expected_size=EXPECTED_CANDIDATE_RECEIPT_FILE_SIZE,
        expected_sha256=EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256,
    )
    document: Any | None = None
    try:
        document = builder._open_pinned_file(
            document_path,
            "aggregate authoring document",
            expected_name=DOCUMENT_RELATIVE_PATH.name,
            expected_size=EXPECTED_DOCUMENT_FILE_SIZE,
            expected_sha256=EXPECTED_DOCUMENT_ENVELOPE_SHA256,
        )
        receipt_json = _read_held_json(
            receipt,
            EXPECTED_CANDIDATE_RECEIPT_FILE_SIZE,
            "aggregate candidate receipt",
        )
        document_json = _read_held_json(
            document,
            EXPECTED_DOCUMENT_FILE_SIZE,
            "aggregate authoring document",
        )
        expected_receipt = {
            "format": "spyro-editor-id65-authoring-editor-aggregate-candidate-receipt",
            "schemaVersion": 1,
            "documentEnvelopeSha256": EXPECTED_DOCUMENT_ENVELOPE_SHA256,
            "documentCanonicalSha256": EXPECTED_DOCUMENT_SHA256,
            "aggregatePlanSha256": EXPECTED_AGGREGATE_PLAN_SHA256,
            "outputRow80Sha256": EXPECTED_OUTPUT_ROW80_SHA256,
            "outputImageSha256": EXPECTED_CANDIDATE_BIN_SHA256,
            "candidateCueSha256": EXPECTED_CANDIDATE_CUE_SHA256,
            "receiptContentSha256": EXPECTED_RECEIPT_CONTENT_SHA256,
            "runtimeCandidateAuthorized": False,
            "runtimeProven": False,
            "createBinEnabled": False,
            "normalCreateBinEnabled": False,
            "releaseAuthorized": False,
            "publishable": False,
        }
        for name, expected in expected_receipt.items():
            if receipt_json.get(name) != expected:
                raise AggregateProfileRefusal(
                    f"aggregate candidate receipt field changed: {name}."
                )
        expected_document = {
            "format": "spyro-editor-id65-authoring-editor-document-envelope",
            "schemaVersion": 1,
            "documentCanonicalSha256": EXPECTED_DOCUMENT_SHA256,
            "compositeManifestSha256": (
                "62635a61a79a59a59d052f50c3f719b56ea1bcdecf09ae91030063be410cfa34"
            ),
            "compositeOutputRow80Sha256": EXPECTED_OUTPUT_ROW80_SHA256,
        }
        for name, expected in expected_document.items():
            if document_json.get(name) != expected:
                raise AggregateProfileRefusal(
                    f"aggregate authoring document field changed: {name}."
                )
        nested = document_json.get("document")
        if not isinstance(nested, dict) or nested.get("contentSha256") != (
            "9d853ccfcf18627a46dc730844efc9eeeda28ee9a0d53c70ae3b7144652b4c3b"
        ):
            raise AggregateProfileRefusal(
                "aggregate authoring document content closure changed."
            )
        return receipt, document
    except Exception:
        if document is not None:
            builder._close_pinned_file(document)
        builder._close_pinned_file(receipt)
        raise


def _run_profile_operation(
    operation: str,
    repository_root: pathlib.Path,
) -> dict[str, object]:
    if operation not in {"build", "verify"}:
        raise AggregateProfileRefusal("profile operation is not build or verify.")
    root = _require_repository_root(repository_root)
    builder = _load_frozen_builder(root)
    pins = _aggregate_pins(builder)
    destination, cue, binary, receipt, document = _paths(root)
    receipt_handle: Any | None = None
    document_handle: Any | None = None
    observed_build_checkpoints: list[str] = []
    try:
        receipt_handle, document_handle = _require_aggregate_metadata(
            builder,
            receipt,
            document,
        )

        def repin_aggregate_metadata(context: Any) -> None:
            checkpoint_index = len(observed_build_checkpoints)
            if (
                checkpoint_index >= len(BUILD_HOOK_CHECKPOINTS)
                or context.checkpoint
                != BUILD_HOOK_CHECKPOINTS[checkpoint_index]
            ):
                raise AggregateProfileRefusal(
                    "frozen builder aggregate hook checkpoint changed."
                )
            observed_build_checkpoints.append(context.checkpoint)
            builder._repin_held_file(
                receipt_handle,
                "aggregate candidate receipt",
            )
            builder._repin_held_file(
                document_handle,
                "aggregate authoring document",
            )

        if operation == "build":
            result = builder.build_profile(
                builder.SOURCE_APP_PATH,
                destination,
                cue,
                binary,
                builder.BIOS_PATH,
                pins,
                hook=repin_aggregate_metadata,
            )
        else:
            result = builder.verify_profile(
                builder.SOURCE_APP_PATH,
                destination,
                cue,
                binary,
                builder.BIOS_PATH,
                pins,
            )
        try:
            builder._repin_held_file(
                receipt_handle,
                "aggregate candidate receipt",
            )
            builder._repin_held_file(
                document_handle,
                "aggregate authoring document",
            )
            if result.destination != destination or result.launch_gate != LAUNCH_GATE:
                raise AggregateProfileRefusal(
                    "frozen profile result identity changed."
                )
            if operation == "build" and tuple(observed_build_checkpoints) != (
                BUILD_HOOK_CHECKPOINTS
            ):
                raise AggregateProfileRefusal(
                    "frozen builder aggregate hook closure is incomplete."
                )
        except Exception as exc:
            if operation == "build":
                raise AggregateProfilePublishedBindingUnconfirmed(
                    destination,
                    str(exc),
                ) from exc
            raise
    except Exception as exc:
        mappings = (
            (builder.PublishedProfileQuarantined, 3),
            (builder.PublishedProfileDurabilityUnconfirmed, 4),
            (builder.PublishedProfileQuarantineFailed, 5),
            (builder.PublishedProfileQuarantineDurabilityUnconfirmed, 6),
            (builder.PrivateStageCleanupFailed, 7),
            (builder.PrivateStageCleanupDurabilityUnconfirmed, 8),
        )
        for exception_type, exit_code in mappings:
            if isinstance(exc, exception_type):
                raise AggregateProfileOperationRefusal(
                    str(exc),
                    exit_code,
                ) from exc
        if isinstance(exc, builder.ProfileRefusal):
            raise AggregateProfileOperationRefusal(str(exc), 2) from exc
        raise
    finally:
        if document_handle is not None:
            builder._close_pinned_file(document_handle)
        if receipt_handle is not None:
            builder._close_pinned_file(receipt_handle)
    return {
        "format": "spyro-id65-authoring-aggregate-profile-result",
        "formatVersion": 1,
        "operation": operation,
        "profileId": PROFILE_ID,
        "destination": os.fspath(destination),
        "candidateCueSha256": EXPECTED_CANDIDATE_CUE_SHA256,
        "candidateBinSha256": EXPECTED_CANDIDATE_BIN_SHA256,
        "documentCanonicalSha256": EXPECTED_DOCUMENT_SHA256,
        "aggregatePlanSha256": EXPECTED_AGGREGATE_PLAN_SHA256,
        "outputRow80Sha256": EXPECTED_OUTPUT_ROW80_SHA256,
        "candidateReceiptContentSha256": EXPECTED_RECEIPT_CONTENT_SHA256,
        "candidateReceiptFileSha256": EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256,
        "documentEnvelopeSha256": EXPECTED_DOCUMENT_ENVELOPE_SHA256,
        "handoffReceiptSha256": result.receipt_sha256,
        "handoffReceiptSchema": result.receipt_schema,
        "appContentManifestSha256": result.app_manifest_sha256,
        "settingsSha256": result.settings_sha256,
        "sharedParentLock": os.fspath(
            root / SHARED_PARENT_LOCK_RELATIVE_PATH
        ),
        "launchGate": result.launch_gate,
        "profileBuilt": operation == "build",
        "profileVerified": True,
        "duckStationLaunched": False,
        "runtimeProven": False,
        "evidenceAccepted": False,
        "createBinAuthorized": False,
        "releaseAuthorized": False,
        "publishable": False,
    }


def _self_test(repository_root: pathlib.Path) -> None:
    root = _require_repository_root(repository_root)
    builder = _load_frozen_builder(root)
    pins = _aggregate_pins(builder)
    destination, cue, binary, receipt, document = _paths(root)
    expected_cue = (
        f'FILE "{binary.name}" BINARY\n'
        "  TRACK 01 MODE2/2352\n"
        "    INDEX 01 00:00:00\n"
    ).encode("ascii")
    if (
        destination.parent / builder.COMMON_LOCK_LEAF
        != root / SHARED_PARENT_LOCK_RELATIVE_PATH
        or cue.name != pins.candidate_cue_name
        or binary.name != pins.candidate_bin_name
        or len(expected_cue) != pins.candidate_cue_size
        or _sha256_bytes(expected_cue) != pins.candidate_cue_sha256
        or pins.candidate_bin_size != EXPECTED_CANDIDATE_BIN_SIZE
        or pins.candidate_bin_sha256 != EXPECTED_CANDIDATE_BIN_SHA256
        or receipt.name != f"{CANDIDATE_STEM}.receipt.json"
        or document.name != DOCUMENT_RELATIVE_PATH.name
    ):
        raise AggregateProfileRefusal("aggregate profile adapter self-test failed.")


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Build or verify the no-launch DuckStation profile for the exact "
            "ID65 authoring aggregate."
        )
    )
    parser.add_argument("operation", choices=("self-test", "build", "verify"))
    parser.add_argument(
        "--repository-root",
        required=True,
        type=pathlib.Path,
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    arguments = _build_parser().parse_args(argv)
    try:
        if arguments.operation == "self-test":
            _self_test(arguments.repository_root)
            print("ID65_AUTHORING_AGGREGATE_PROFILE_ADAPTER_SELF_TEST_OK")
            return 0
        result = _run_profile_operation(
            arguments.operation,
            arguments.repository_root,
        )
        print(_canonical_json(result))
        return 0
    except AggregateProfileOperationRefusal as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        return exc.exit_code
    except AggregateProfileRefusal as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        return 2
    except Exception as exc:
        print(
            f"REFUSED: unexpected aggregate profile failure: "
            f"{type(exc).__name__}: {exc}",
            file=sys.stderr,
        )
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
