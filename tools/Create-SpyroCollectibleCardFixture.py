#!/usr/bin/env python3
"""Create a NEW isolated research card, never edit an existing card.

This deliberately seeds one test egg in a copied file's otherwise-FF tail.
It is NOT a native writer, migration policy, or evidence of collecting/saving.
Directory field geometry was checked against DuckStation memory_card_image.cpp:
https://github.com/stenzek/duckstation/blob/master/src/core/memory_card_image.cpp
Only the pinned one-file USA baseline is admitted by the CLI.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import stat
import struct
import uuid

SOURCE_SHA256 = "d69b43f8be2585f7b5e7fd1b07f85217ba74c01f102c01d7a22928ee0e9e8ae2"
NAME = "collectible-card-load-fixture-r01.mcd"
CONTENT_ID = uuid.UUID("12345678-1234-5678-9012-123456789abc").bytes
INSTANCE_IDS = tuple(uuid.UUID(s).bytes for s in (
    "11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222"))


def sha(data):
    return hashlib.sha256(data).hexdigest()


def identity(metadata):
    # Reads may update atime; only content/ownership changes invalidate the hold.
    return (metadata.st_dev, metadata.st_ino, metadata.st_mode, metadata.st_size,
            metadata.st_mtime_ns, metadata.st_ctime_ns)


def envelope():
    # Independent fixed encoding of DetachedCollectibleManifest/SaveCodec v1.
    manifest = bytearray(128)
    manifest[:8] = b"SPYCMAN\0"
    struct.pack_into("<HHI", manifest, 8, 1, 80, 160)
    manifest[16:32] = CONTENT_ID
    struct.pack_into("<II", manifest, 32, 1, 2)
    for bit, identity in enumerate(INSTANCE_IDS):
        at = 80 + bit * 24
        manifest[at:at + 16] = identity
        struct.pack_into("<I", manifest, at + 16, bit)
    manifest += hashlib.sha256(manifest).digest()
    save = bytearray(99)
    save[:8] = b"SPYCSAV\0"
    struct.pack_into("<HHI", save, 8, 1, 96, 131)
    save[16:32] = CONTENT_ID
    struct.pack_into("<IIHH", save, 32, 1, 2, 3, 1)
    save[48:80] = hashlib.sha256(manifest).digest()
    save[96] = 1  # Selected ordinary slot zero: first of two IDs earned.
    save += hashlib.sha256(save).digest()
    return bytes(save), sha(manifest)


def compose(source):
    if len(source) != 131072 or source[:2] != b"MC":
        raise ValueError("Expected a raw 128-KiB MC card")
    for frame in range(16):
        checksum = 0
        for value in source[frame * 128:(frame + 1) * 128]: checksum ^= value
        if checksum: raise ValueError("Invalid card header/directory XOR")
    # Closed fixture geometry; do not infer ownership from a loose filename hit.
    if struct.unpack_from("<IIH", source, 128) != (0x51, 0x2000, 0xFFFF):
        raise ValueError("Expected exactly one unchained Spyro block at block one")
    if source[138:159].split(b"\0", 1)[0] != b"BASCUS-94228SPYRO":
        raise ValueError("Unexpected native save filename")
    for frame in range(2, 16):
        if struct.unpack_from("<IIH", source, frame * 128) != (0xA0, 0, 0xFFFF):
            raise ValueError("This fixture profile requires remaining directory entries free")
    if source[0x2000:0x2004] != b"SC\x13\x01":
        raise ValueError("Unexpected Spyro native file header")
    for slot in range(3):
        at = 0x2200 + slot * 0x600
        if sum(source[at:at + 0x58C]) != struct.unpack_from("<I", source, at + 0x58C)[0]:
            raise ValueError("Invalid ordinary native slot checksum")
    if source[0x2240] != 1:
        raise ValueError("Slot zero must already be a native saved game")
    if source[0x3400:0x4000] != b"\xff" * 0xC00:
        raise ValueError("Refusing to replace non-FF or unknown extension data")
    extension, manifest_hash = envelope()
    result = source[:0x3400] + extension + source[0x3483:]
    if len(result) != len(source) or result[:0x3400] != source[:0x3400] or result[0x3483:] != source[0x3483:]:
        raise ValueError("Fixture complement preservation failed")
    return result, {
        "profile": "native-collectible-card-load-isolated-fixture-v1",
        "source_sha256": sha(source), "fixture_sha256": sha(result),
        "manifest_sha256": manifest_hash, "envelope_sha256": sha(extension),
        "native_prefix_sha256": sha(source[0x2000:0x3400]),
        "changed_extent": {"offset": 0x3400, "length": 131},
        "slot_bits": [1, 0, 0], "native_prefix_preserved": True,
        "native_save_or_collection_proven": False,
    }


def publish(source_path, new_directory):
    source_path, new_directory = Path(source_path).absolute(), Path(new_directory).absolute()
    # No source symlinks, output reuse, normal-card replacement or hash override.
    fd = os.open(source_path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, "rb") as handle:
        original_stat = os.fstat(handle.fileno())
        if not stat.S_ISREG(original_stat.st_mode) or original_stat.st_size != 131072:
            raise ValueError("Source is not an ordinary raw card file")
        source = handle.read()
        if sha(source) != SOURCE_SHA256: raise ValueError("Different source card; fixed baseline required")
        candidate, receipt = compose(source)
        if identity(os.stat(source_path, follow_symlinks=False)) != identity(original_stat):
            raise ValueError("Source card changed during inspection")
        handle.seek(0)
        if handle.read() != source: raise ValueError("Source bytes changed during inspection")
        new_directory.mkdir(mode=0o700, parents=False, exist_ok=False)
        with (new_directory / NAME).open("xb") as output: output.write(candidate)
        if (new_directory / NAME).read_bytes() != candidate:
            raise ValueError("Fixture readback mismatch; retained for diagnosis")
        handle.seek(0)
        if handle.read() != source or identity(os.stat(source_path, follow_symlinks=False)) != identity(original_stat):
            raise ValueError("Source changed externally; do not use this fixture")
        receipt.update(source_card=str(source_path), fixture_card=str(new_directory / NAME))
        with (new_directory / "fixture-receipt.json").open("x") as output:
            json.dump(receipt, output, indent=2); output.write("\n")
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--create-isolated-research-fixture", action="store_true")
    parser.add_argument("--source-card", type=Path, required=True)
    parser.add_argument("--new-output-directory", type=Path, required=True)
    args = parser.parse_args()
    if not args.create_isolated_research_fixture:
        parser.error("Explicit isolated-research-fixture opt-in required")
    print(json.dumps(publish(args.source_card, args.new_output_directory), indent=2))


if __name__ == "__main__": main()
