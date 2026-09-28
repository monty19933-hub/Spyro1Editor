#!/usr/bin/env python3
"""Read-only, fixed-disc card-load observation; never writes RAM/cards or controls CPU.

Prepared means a temporary shadow, NOT live Ready, collection or save/reload.
Connection may pause DuckStation; resume and all input stay in its ordinary UI.
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


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(filename))
    result = importlib.util.module_from_spec(spec); sys.modules[name] = result
    spec.loader.exec_module(result)
    return result


shared = module("card_load_read_primitives", "Observe-SpyroJapaneseEggs.py")
fixture = module("card_load_fixture_encoding", "Create-SpyroCollectibleCardFixture.py")
STATE, SHADOW, INPUT, READ_BUFFER, PREFIX = 0x80081C40, 0x80081A40, 0x80083200, 0x80081E00, 0x80083E00
PREFIX_SHA = "4cd8a991ea27602c25ff1c20bd3209ff973987bae2e2845b10038ad7cdc9482b"
FIELDS = {
    "bound": 0x80078D14, "controller": 0x80078D18, "slot": 0x80078D1C,
    "marker": 0x80078D20, "outer": 0x80078D78, "substate": 0x80078D7C,
    "cursor_slot": 0x80078D8C, "intent": 0x80078D94, "cursor_controller": 0x80078DA0,
    "menu_pointer": 0x80078DC4, "busy": 0x80075B50, "completion_latch": 0x80075B58,
    "arena_end": 0x800785E8,
}
GATES = {
    0x800755A4: struct.pack("<I", 0xA00),
    **{at: struct.pack("<II", 0x3C028008, 0x24425238) for at in (0x80012830, 0x8002E2FC, 0x80033624)},
    0x8007CA98: struct.pack("<II", 0x0C01FE8E, 0),
    0x8007CC00: struct.pack("<II", 0x080201A6, 0),
}


class CardLoadSession(shared.FixedCandidateSession):
    image_name = "collectible-card-load-research-r01.bin"
    # Original-only prediction and independent published whole-disc/CUE readback agree.
    image_sha256 = "ef36363011f74a1f2bdb56f1e709d5654f8de0bbec497c95c16e3debf8e355e7"
    image_size = 661547040


def read(client, at, length):
    parts = []
    for offset in range(0, length, 1024):
        size = min(1024, length - offset); part = client.read(at + offset, size)
        if len(part) != size: raise ValueError("Partial bounded memory read")
        parts.append(part)
    return b"".join(parts)


def gates(client):
    for at, expected in GATES.items():
        if read(client, at, len(expected)) != expected:
            raise ValueError("Card overlay or executable anchor not resident: %08x" % at)


def expected_shadow(controller, marker, epoch):
    if controller not in (0, 1) or not 0 <= marker <= 0xFFFF or not 1 <= epoch <= 0xFFFFFFFF:
        raise ValueError("Invalid observed selection context")
    envelope, manifest_hash = fixture.envelope()
    out = bytearray(512)
    struct.pack_into("<IHBB", out, 0x10, 0x524C4353, 1, 1, 0)
    out[0x18] = controller; struct.pack_into("<H", out, 0x1A, marker)
    out[0x20:0x30] = fixture.CONTENT_ID
    out[0x30:0x50] = bytes.fromhex(manifest_hash)
    out[0x50:0x70] = hashlib.sha256(envelope).digest()
    struct.pack_into("<IHHHH", out, 0x70, 1, 2, 1, 2, 1)
    struct.pack_into("<I", out, 0x8C, epoch)
    out[0x90], out[0xA0] = 1, 3  # Each bitmap owns a separately aligned 16-byte span.
    return bytes(out)


def capture(client, label):
    result = {"utc": datetime.now(timezone.utc).isoformat(), "label": label[:120],
              "profile": "native-collectible-card-load-research-v1", "sequential_reads_not_atomic": True,
              "prepared_fixture_observed": False, "native_ready_issued": False,
              "collection_verified": False, "native_save_reload_verified": False, "runtime_pass": False}
    try:
        gates(client)
        values = {name: struct.unpack("<I", read(client, at, 4))[0] for name, at in FIELDS.items()}
        state = read(client, STATE, 0x80)
        stage, epoch, operation, io_result, failure, menu = struct.unpack_from("<6I", state)
        result.update(native=values, stage=stage, epoch=epoch, operation=operation,
                      io_result=io_result, failure=failure, state_hex=state.hex())
        if stage not in (0, 1, 2, 3): raise ValueError("Unknown or overwritten card-load state")
        if stage == 2:
            slot, controller, marker, captured_epoch = struct.unpack_from("<4I", state, 0x20)
            if (slot, operation, io_result, failure, captured_epoch) != (0, 3, 0, 0, epoch):
                raise ValueError("Prepared state has invalid completion/context")
            if menu % 4 or not 0x800C3238 <= menu <= 0x801FD600 or values["arena_end"] != menu + 0x2000:
                raise ValueError("Native menu buffer is outside its inspected extent")
            if any(values[key] != expected for key, expected in {
                "bound": 1, "controller": controller * 16, "slot": 0, "marker": marker,
                "outer": 2, "substate": 5, "cursor_slot": 0, "intent": 1,
                "cursor_controller": controller, "menu_pointer": menu, "busy": 0, "completion_latch": 0,
            }.items()): raise ValueError("Native selection differs from captured prepared tuple")
            raw = read(client, INPUT, 131); shadow = read(client, SHADOW, 512)
            if raw != fixture.envelope()[0]: raise ValueError("Different or corrupt fixture envelope")
            if shadow != expected_shadow(controller, marker, epoch):
                raise ValueError("Prepared shadow differs from complete expected ABI image")
            for at in (PREFIX, READ_BUFFER):
                if hashlib.sha256(read(client, at, 0x1400)).hexdigest() != PREFIX_SHA:
                    raise ValueError("Native prefix copy differs from preserved fixture")
            ledger = read(client, 0x801FF600, 512)
            if any(ledger): raise ValueError("Unexpected live reservation mutation")
            if read(client, SHADOW, 512) != shadow or read(client, INPUT, 131) != raw:
                raise ValueError("Prepared data changed during sample")
            for at in (PREFIX, READ_BUFFER):
                if hashlib.sha256(read(client, at, 0x1400)).hexdigest() != PREFIX_SHA:
                    raise ValueError("Native prefix changed during sample")
            if read(client, 0x801FF600, 512) != ledger:
                raise ValueError("Live reservation changed during sample")
            result.update(shadow_hex=shadow.hex(), envelope_sha256=hashlib.sha256(raw).hexdigest(),
                          selected_earned=1, active_total=2, live_reservation_zero=True)
        if read(client, STATE, 0x80) != state:
            raise ValueError("Card load transitioned during sample")
        if any(struct.unpack("<I", read(client, at, 4))[0] != values[name] for name, at in FIELDS.items()):
            raise ValueError("Native fields changed during sequential sample")
        gates(client)
        result["prepared_fixture_observed"] = stage == 2
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
    with CardLoadSession(args.candidate_bin, args.pid) as owned:
        with socket.create_connection(("127.0.0.1", 2345), timeout=2) as connection:
            owned.require_fresh(); client = shared.ReadOnlyClient(connection)
            print("READ_ONLY_CARD_LOAD_CONNECTED; p <label>, watch (30 seconds), q. UI-only resume/input.", flush=True)
            for line in sys.stdin:
                command = line.strip()
                if command == "q": break
                if command == "watch" or command == "p" or command.startswith("p "):
                    until = time.monotonic() + (30 if command == "watch" else 0)
                    while True:
                        owned.require_fresh(); result = capture(client, command[1:].strip())
                        owned.require_fresh()
                        result.update(process_candidate_ownership_checked=True, process_id=owned.pid,
                                      candidate_path=str(owned.path), loaded_disc_atomic_proof=False)
                        print(json.dumps(result), flush=True)
                        if time.monotonic() >= until: break
                        time.sleep(0.05)


if __name__ == "__main__": main()
