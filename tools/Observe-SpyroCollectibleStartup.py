#!/usr/bin/env python3
"""Read-only samples of the exact startup-only research disc in USA Artisans.

The fixed full-image identity must be issued after independent publication
verification. No receipt/hash/offset override, pointer chasing, debugger writes
or automatic runtime approval. Initial connection may pause the emulator;
all gameplay and resume/pause remain in the UI. Zero RAM at a later sample
cannot by itself demonstrate that the bootstrap executed its clearing loop.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import socket
import struct
import sys
from types import MappingProxyType

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("startup_read_primitives", Path(__file__).with_name("Observe-SpyroJapaneseEggs.py"))
shared = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = shared
spec.loader.exec_module(shared)

PROFILE = "native-usa-collectible-startup-only-research-r01"
GATES = MappingProxyType({
    "reserve": (0x800755A4, 0xA00), "stack_origin": (0x800755A8, 0x200000),
    "initial_heap_byte_count": (0x800730C4, 0x184BC0),
    "level": (0x8007596C, 10), "game_state": (0x800757D8, 0),
    "normal_classifier": (0x80075690, 0), "pete_resident_base": (0x800785FC, 0x801C59B8),
    "frame_floor": (0x800785E8, 0x801879B0), "other_frame_floor": (0x800785EC, 0x801A39B0),
    "ordering_clear_base": (0x800785F0, 0x801BF9B0), "ordering_table_base": (0x800785F4, 0x801BF9B8),
    "particle_base": (0x800785F8, 0x801C39B8),
})
OBSERVATIONS = MappingProxyType({
    **{name: shared.wizard.FIELDS[name] for name in (
        "lives", "player_xyz", "player_state", "primitive_cursor", "primitive_limit", "renderer_exhaustion_flag")},
    "native_file_bound": (0x80078D14, "<I"), "controller_encoding": (0x80078D18, "<I"),
    "ordinary_slot": (0x80078D1C, "<I"), "file_marker_not_unique_identity": (0x80078D20, "<I"),
})


class StartupSession(shared.FixedCandidateSession):
    image_name = "collectible-startup-bootstrap-reservation-r01.bin"
    # Independent Python full-disc derivation/readback matches the C# writer.
    image_sha256 = "895074e78abcf7de47faa15b62ee340239d996a486d1bc7ef5897c68cd4a06d3"
    image_size = 661547040


def _read(client, address, count):
    data = client.read(address, count)
    if len(data) != count:
        raise ValueError("Partial bounded memory read")
    return data


def _gates(client):
    result = {}
    for name, (address, expected) in GATES.items():
        value = struct.unpack("<I", _read(client, address, 4))[0]
        if value != expected:
            raise ValueError("Different or uninitialized startup-only Artisans geometry: " + name)
        result[name] = value
    return result


def capture(client, label):
    result = {"utc": datetime.now(timezone.utc).isoformat(), "label": label[:120], "profile": PROFILE,
              "sequential_reads_not_atomic": True, "clearing_execution_observed": False,
              "native_residence_approved": False, "save_verified": False, "runtime_pass": False}
    try:
        gates = _gates(client)
        reservation = _read(client, 0x801FF600, 0x200)
        observed = {}
        for name, (address, fmt) in OBSERVATIONS.items():
            values = struct.unpack(fmt, _read(client, address, struct.calcsize(fmt)))
            observed[name] = values[0] if len(values) == 1 else list(values)
        if _gates(client) != gates or _read(client, 0x801FF600, 0x200) != reservation:
            raise ValueError("Reservation or geometry changed during sequential sample")
        result.update(gates)
        result.update(observed)
        result.update(reservation_hex=reservation.hex(), guard_hex=reservation[:0x10].hex(),
                      reservation_sha256=hashlib.sha256(reservation).hexdigest(),
                      metadata_and_bitmap_region_zero=not any(reservation[0x10:]),
                      metadata_nonzero_byte_count=sum(b != 0 for b in reservation[0x10:]))
    except (ValueError, struct.error) as error:
        result["sample_refused"] = str(error)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--connect-identified-test-session", action="store_true")
    parser.add_argument("--pid", type=int)
    parser.add_argument("--candidate-bin")
    args = parser.parse_args()
    if not args.connect_identified_test_session or args.pid is None or args.pid <= 0 or not args.candidate_bin:
        parser.error("Explicit opt-in, exact positive --pid and --candidate-bin are required before connection.")
    with StartupSession(args.candidate_bin, args.pid) as owned:
        with socket.create_connection(("127.0.0.1", 2345), timeout=2) as connection:
            owned.require_fresh()
            client = shared.ReadOnlyClient(connection)
            print("READ_ONLY_STARTUP_CONNECTED; p <label> samples, q closes. UI-only input/resume.", flush=True)
            for line in sys.stdin:
                command = line.strip()
                if command == "q": break
                if command == "p" or command.startswith("p "):
                    owned.require_fresh()
                    result = capture(client, command[1:].strip())
                    owned.require_fresh()
                    result.update(process_candidate_ownership_checked=True, process_id=owned.pid,
                                  candidate_path=str(owned.path), loaded_disc_atomic_proof=False)
                    print(json.dumps(result), flush=True)


if __name__ == "__main__":
    main()
