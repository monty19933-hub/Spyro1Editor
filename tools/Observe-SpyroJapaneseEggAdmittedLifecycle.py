#!/usr/bin/env python3
"""Read-only samples for the fixed admitted-egg research disc, never game control.

Static bounds come from source-owned final geometry, not a receipt or RAM
pointer. Sequential samples cannot establish atomicity, gameplay causation,
persistence, inventory or completion. Connection requires the independently
predicted and physically verified complete BIN hash frozen below.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import socket
import struct
import sys
import time

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("_egg_admission_reader", Path(__file__).with_name("Observe-SpyroCollectibleCardAdmission.py"))
admission = importlib.util.module_from_spec(spec); spec.loader.exec_module(admission)
read = admission.read
PROFILE = "native-artisans-two-japanese-eggs-admitted-lifecycle-research-v1"
LEDGER = admission.LEDGER
FIELDS = dict(admission.FIELDS)
RESIDENT_GATES = dict(admission.GATES) | {0x8005A718: struct.pack("<II", 0x3C028009, 0x2442AA38)}
# Actual source final layout: 10000-byte overlay, anchor8008AA38. These
# addresses must never be adjusted from a CLI argument, receipt or live RAM.
GUARD_PC, GUARD_LENGTH = 0x8008A030, 0x800
GUARD_SHA256 = "04ccb42bf627100bec0ff9614dd987dc16f55ae0cf9bbdb0ba647eb543e9d8fd"
SCENE_CODE_GATES = {
    0x80088C3C: struct.pack("<II", 0x0802280C, 0),
    0x80088AE4: struct.pack("<II", 0x08022818, 0),
}
SCENE_GATES = {
    0x80075690: 0, 0x80075734: 0x8007D9C8, 0x800785D8: 0x8008AA38,
    0x800785E0: 0x80117D54, 0x800785E4: 0x80167D54,
    0x80075828: 0x80172000, 0x80075890: 0x80175C80,
    0x80076B1C: 0x80166554, 0x80076B40: 0x80166D5C,
}
ROWS, PRIVATE, CLASSES = (0x80175BD0, 0x80175C28), (0x80179C20, 0x80179C2C), (489, 498)
PLAYER_POSITION, PLAYER_STATE = 0x80078A58, 0x80078AD0


class AdmittedEggSession(admission.reader.shared.FixedCandidateSession):
    image_name = "artisans-native-japanese-eggs-admitted-lifecycle-r01.bin"
    image_sha256 = "d77ac18c17a2bd0a06ab2cf4efd5a1eb2912e8634836db1105dbb32a7f65b4f1"
    image_size = 661547040


def expected_ledger(controller, marker, epoch, bits):
    if bits not in (1, 3): raise ValueError("Unexpected fixed-recipe earned bitmap")
    data = bytearray(admission.reader.expected_shadow(controller, marker, epoch))
    if bits == 3:
        data[0x19] = 1
        struct.pack_into("<H", data, 0x7A, 2)
        struct.pack_into("<I", data, 0x7C, 1)
        data[0x90] = 3
    return bytes(data)


def resident_gates(client):
    for at, data in RESIDENT_GATES.items():
        if read(client, at, len(data)) != data: raise ValueError("Resident gate differs: %08x" % at)


def scene_gates(client):
    for at, value in SCENE_GATES.items():
        if read(client, at, 4) != struct.pack("<I", value): raise ValueError("Final scene gate differs: %08x" % at)
    for at, data in SCENE_CODE_GATES.items():
        if read(client, at, len(data)) != data: raise ValueError("Lifecycle adapter edge differs: %08x" % at)
    code = b"".join(read(client, GUARD_PC + offset, min(1024, GUARD_LENGTH - offset))
                    for offset in range(0, GUARD_LENGTH, 1024))
    if hashlib.sha256(code).hexdigest() != GUARD_SHA256: raise ValueError("Final guard code capacity differs")


def row_identity(row):
    # Timer/yaw are legitimately animated between reads, not identity fields.
    return row[:4] + row[0x36:0x38] + row[0x48:0x49]


def capture(client, label):
    result = dict(utc=datetime.now(timezone.utc).isoformat(), label=label[:120], profile=PROFILE,
                  sequential_reads_not_atomic=True, session_epoch_independently_authenticated=False,
                  seeded_state_observed=False, pickup_state_observed=False, egg_rows_observed=False,
                  collection_verified=False, native_save_reload_verified=False, inventory_verified=False,
                  completion_percentage_verified=False, all_transition_lifetime_verified=False, runtime_pass=False)
    try:
        resident_gates(client)
        fields = {name: struct.unpack("<I", read(client, at, 4))[0] for name, at in FIELDS.items()}
        ledger = read(client, LEDGER, 512); phase = ledger[0x16]
        result.update(native=fields, phase=phase, ledger_hex=ledger.hex(), ledger_sha256=hashlib.sha256(ledger).hexdigest())
        if phase not in (0, 1): raise ValueError("Unknown live ledger phase")
        rows = []
        if phase == 1:
            controller, marker = ledger[0x18], struct.unpack_from("<H", ledger, 0x1A)[0]
            epoch, bits = struct.unpack_from("<I", ledger, 0x8C)[0], ledger[0x90]
            if any(fields[name] != value for name, value in {
                    "bound": 1, "controller": controller * 16, "slot": 0, "marker": marker}.items()):
                raise ValueError("Ready differs from native selected-save tuple")
            if ledger[0x10:] != expected_ledger(controller, marker, epoch, bits)[0x10:]:
                raise ValueError("Complete ledger differs from seeded or single-pickup ABI")
            result.update(epoch=epoch, selected_earned=1 if bits == 1 else 2, active_total=2,
                          guard_hex=ledger[:0x10].hex(), dirty=ledger[0x19], generation=struct.unpack_from("<I", ledger, 0x7C)[0])
            if fields["level"] == 10 and fields["game_state"] == 0:
                scene_gates(client)
                for index, at in enumerate(ROWS):
                    row = read(client, at, 0x58); rows.append(row)
                    if (struct.unpack_from("<I", row)[0] != PRIVATE[index] or
                            struct.unpack_from("<H", row, 0x36)[0] != CLASSES[index] or
                            row[0x48] != (0xFD if bits & (1 << index) else 0)):
                        raise ValueError("Native egg identity/retirement disagrees with ledger")
                result["rows"] = [dict(address=at, bytes=row.hex()) for at, row in zip(ROWS, rows)]
                # Navigation evidence only; position is allowed to advance
                # during the sequential sample and is not an acceptance gate.
                result["player_xyz"] = struct.unpack("<iii", read(client, PLAYER_POSITION, 12))
                result["player_state"] = struct.unpack("<I", read(client, PLAYER_STATE, 4))[0]
        if read(client, LEDGER, 512) != ledger: raise ValueError("Ledger changed during sequential sample")
        if any(struct.unpack("<I", read(client, at, 4))[0] != fields[name] for name, at in FIELDS.items()):
            raise ValueError("Native binding/scene changed during sequential sample")
        if rows:
            if any(row_identity(read(client, at, 0x58)) != row_identity(row) for at, row in zip(ROWS, rows)):
                raise ValueError("Row identity/retirement changed during sequential sample")
            scene_gates(client)
        resident_gates(client)
        result.update(seeded_state_observed=phase == 1 and bits == 1,
                      pickup_state_observed=phase == 1 and bits == 3, egg_rows_observed=bool(rows))
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
    if len(AdmittedEggSession.image_sha256) != 64:
        parser.error("Admitted egg candidate has not passed independent physical verification")
    with AdmittedEggSession(args.candidate_bin, args.pid) as owned:
        with socket.create_connection(("127.0.0.1", 2345), timeout=2) as connection:
            owned.require_fresh(); client = admission.reader.shared.ReadOnlyClient(connection)
            print("READ_ONLY_ADMITTED_EGG_CONNECTED; p <label>, watch (30 seconds), q. UI-only resume/input.", flush=True)
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
