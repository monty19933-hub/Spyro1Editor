#!/usr/bin/env python3
"""Fixed-disc read-only live-ledger observation; no RAM writes or CPU control.

One complete Ready snapshot is not pickup, persistence, inventory, completion
percentage, or an all-transition lifetime proof. Resume/input use ordinary UI.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import socket
import struct
import sys
import time
import importlib.util

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("_admission_observer_reader", Path(__file__).with_name("Observe-SpyroCollectibleCardLoad.py"))
reader = importlib.util.module_from_spec(spec); spec.loader.exec_module(reader)
physical = reader.module("_admission_observer_physical", "Verify-SpyroCollectibleCardAdmission.py")
read = reader.read
LEDGER = 0x801FF600
FIELDS = {name: reader.FIELDS[name] for name in ("bound", "controller", "slot", "marker")}
FIELDS.update(game_state=0x800757D8, level=0x8007596C)
GATES = {
    0x800755A4: struct.pack("<I", 0xA00),
    **{at: struct.pack("<II", 0x3C028008, 0x24427A38) for at in (0x80012830, 0x8002E2FC, 0x80033624)},
    0x80012700: struct.pack("<20I", *physical.RESET_WORDS),
}


class CardAdmissionSession(reader.shared.FixedCandidateSession):
    image_name = physical.IMAGE_NAME
    # Independent original-only prediction and actual published BIN/CUE agree.
    # No expected-hash CLI override is accepted.
    image_sha256 = "99cb82cc6af22b83004a022c4f33fc3ceb9f8477ec476c1b61139afeb46a8aff"
    image_size = 661547040


def gates(client):
    for at, expected in GATES.items():
        if read(client, at, len(expected)) != expected:
            raise ValueError("Admission resident executable gate differs: %08x" % at)


def capture(client, label):
    result = dict(utc=datetime.now(timezone.utc).isoformat(), label=label[:120], profile=physical.PROFILE,
                  sequential_reads_not_atomic=True, live_ready_fixture_observed=False,
                  session_epoch_independently_authenticated=False,
                  collection_verified=False, native_save_reload_verified=False, inventory_verified=False,
                  completion_percentage_verified=False, all_transition_lifetime_verified=False, runtime_pass=False)
    try:
        gates(client)
        fields = {name: struct.unpack("<I", read(client, at, 4))[0] for name, at in FIELDS.items()}
        ledger = read(client, LEDGER, 512)
        phase = ledger[0x16]
        result.update(native=fields, phase=phase, ledger_hex=ledger.hex(), ledger_sha256=hashlib.sha256(ledger).hexdigest())
        if phase not in (0, 1): raise ValueError("Unknown live ledger phase")
        if phase == 1:
            controller, marker = ledger[0x18], struct.unpack_from("<H", ledger, 0x1A)[0]
            epoch = struct.unpack_from("<I", ledger, 0x8C)[0]
            if any(fields[name] != value for name, value in {
                "bound": 1, "controller": controller * 16, "slot": 0, "marker": marker,
            }.items()): raise ValueError("Ready ledger differs from native selected-save tuple")
            expected = reader.expected_shadow(controller, marker, epoch)
            # Native sentinel owns the first16 bytes; do not require zeros or
            # pretend its value authenticates collectible state.
            if ledger[0x10:] != expected[0x10:]:
                raise ValueError("Ready ledger differs from complete fixed-fixture ABI")
            result.update(epoch=epoch, selected_earned=1, active_total=2,
                          guard_hex=ledger[:0x10].hex())
        if read(client, LEDGER, 512) != ledger:
            raise ValueError("Live ledger changed during sequential sample")
        if any(struct.unpack("<I", read(client, at, 4))[0] != fields[name] for name, at in FIELDS.items()):
            raise ValueError("Native binding/scene changed during sequential sample")
        gates(client)
        result["live_ready_fixture_observed"] = phase == 1
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
        parser.error("Explicit opt-in, exact positive PID and fixed candidate are required")
    if len(CardAdmissionSession.image_sha256) != 64:
        parser.error("Admission candidate has not passed independent physical verification")
    with CardAdmissionSession(args.candidate_bin, args.pid) as owned:
        with socket.create_connection(("127.0.0.1", 2345), timeout=2) as connection:
            owned.require_fresh(); client = reader.shared.ReadOnlyClient(connection)
            print("READ_ONLY_CARD_ADMISSION_CONNECTED; p <label>, watch (30 seconds), q. UI-only resume/input.", flush=True)
            for line in sys.stdin:
                command = line.strip()
                if command == "q": break
                if command == "watch" or command == "p" or command.startswith("p "):
                    until = time.monotonic() + (30 if command == "watch" else 0)
                    while True:
                        owned.require_fresh(); result = capture(client, "watch" if command == "watch" else command[1:].strip())
                        owned.require_fresh()
                        result.update(process_candidate_ownership_checked=True, process_id=owned.pid,
                                      candidate_path=str(owned.path), loaded_disc_atomic_proof=False)
                        print(json.dumps(result), flush=True)
                        if time.monotonic() >= until: break
                        time.sleep(0.05)


if __name__ == "__main__": main()
