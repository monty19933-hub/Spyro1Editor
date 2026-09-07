#!/usr/bin/env python3
"""Read-only observation of the exact Dark Hollow two-Wizard research candidate.

Identify DuckStation's process/open BIN and verify full candidate companions
externally before connecting. Matching RAM and hash annotations do not identify
the loaded image. This tool sends only bounded memory reads/protocol ACKs;
initial connection may pause the game. Control gameplay only through the UI.
Sample while paused in the UI. Reads are sequential, not atomic, and never
certify gameplay or peak budgets. A changed row can be ordinary animation.
The existing one-Wizard and fresh-ID65 observers are unchanged.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import importlib.util
from pathlib import Path
import json
import socket
import struct
import sys

_spec = importlib.util.spec_from_file_location("two_wizard_read_only_primitives", Path(__file__).with_name("Observe-SpyroNativeWizard.py"))
one = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = one
_spec.loader.exec_module(one)
ReadOnlyClient = one.ReadOnlyClient

PROFILE = "native-dark-two-wizards-z128-a129-research-v1"
IMAGE_SHA = "612e68e1a8642d42f2417fd6153368c9549094ac391a3835c9cf6dfee7808cba"
DATA_SHA = "375b9dc41a76d507ded9ffdf442e4febbb1916e68bb5d4597477bb9031e2fd90"
CUE_SHA = "d54624ebee82a0362ca3c3c65e771bcba681c37f392e0a29a2c5adc34ea1c7e6"
RECEIPT_SHA = "ac0f7e2c49a1207b036444b386a3443376c2f29417958a0922a98df39ae59f75"
MOBY, DYNAMIC, END, CAPACITY = 0x8013AE1C, 0x8013DACC, 0x8014021C, 89
POOL, PETE = 0x801C3BB8, 0x801C5BB8
# Two distinct relocated private owners and their only nested pointer fields.
# Necessary loader-phase checks, not complete image authentication.
WIZARDS = ((128, 0x801410B8), (129, 0x80141108))
GATES = {
    "level": (0x8007596C, "<I", 12),
    "game_state": (0x800757D8, "<I", 0),
    "normal_classifier": (0x80075690, "<I", 0),
    "continuous_level_index": (0x80075964, "<I", 2),
    "copy_anchor": (0x800785D8, "<I", 0x80087238),
    "model_anchor": (0x800785E0, "<I", 0x800BF9E4),
    "scene_anchor": (0x800785E4, "<I", 0x801321E4),
    "scene_disk_offset": (0x80078600, "<I", 0x1A7000),
    "scene_disk_length": (0x80078604, "<I", 0x11800),
    "moby_base": (0x80075828, "<I", MOBY),
    "dynamic_base": (0x80075890, "<I", DYNAMIC),
    "arena_end": (0x800756E8, "<I", END),
    "dynamic_capacity": (0x800756A8, "<I", CAPACITY),
    "particle_base": (0x80075824, "<I", POOL),
}
OWNERS = {
    "owners_live_word": (0x800778C8, "<I"),
    "owners_checkpoint_word": (0x800778A8, "<I"),
    "owners_durable_word": (0x80077958, "<I"),
}
FIELDS = dict(one.FIELDS) | {k: (at, encoding) for k, (at, encoding, _) in GATES.items()} | OWNERS
REREAD_FIELDS = tuple(GATES) + ("dynamic_count", "dynamic_properties_cursor", "next_free_moby_not_active_end") + tuple(OWNERS)


def read_fields(client, names):
    result = {}
    for name in names:
        at, encoding = FIELDS[name]
        values = struct.unpack(encoding, client.read(at, struct.calcsize(encoding)))
        result[name] = values[0] if len(values) == 1 else list(values)
    return result


def require_baseline(values):
    for name, (_, _, expected) in GATES.items():
        if values[name] != expected:
            raise ValueError("Not the initialized exact two-Wizard profile: " + name)
    if not 0 <= values["dynamic_count"] <= CAPACITY:
        raise ValueError("Native dynamic count exceeds the exact89-slot capacity.")
    props, row = values["dynamic_properties_cursor"], values["next_free_moby_not_active_end"]
    if not END - CAPACITY * 24 <= props <= END or (END - props) % 24:
        raise ValueError("Properties cursor escaped the exact89-slot grid.")
    if not DYNAMIC <= row <= DYNAMIC + CAPACITY * 88 or (row - DYNAMIC) % 88:
        raise ValueError("Next-free pointer escaped the exact89-row grid.")


def scan_dynamic(client):
    active = []
    for slot in range(CAPACITY):
        at = DYNAMIC + slot * 88
        row = client.read(at, 88)
        if row[0x48] == 0xFF:
            return {"active": active, "terminal_slot": slot, "high_water_rows_before_terminal": slot, "no_in_range_terminal": False}
        if row[0x48] < 0x80:
            item = one.decode_moby(row, 130 + slot, at)
            # Source Dark child emitter and native collection use this BYTE.
            # Its meaning for arbitrary other classes is not inferred here.
            item["raw_56_u8"] = row[0x56]
            if item["class_id"] == 0x56:
                item["native_reward_parent_index_u8"] = row[0x56]
            active.append(item)
    return {"active": active, "terminal_slot": None, "high_water_rows_before_terminal": CAPACITY, "no_in_range_terminal": True}


def capture(client, label):
    result = {"utc": datetime.now(timezone.utc).isoformat(), "label": label[:120],
              "expected_profile_annotation": PROFILE, "expected_candidate_sha256_annotation": IMAGE_SHA,
              "expected_data_sha256_annotation": DATA_SHA, "expected_cue_sha256_annotation": CUE_SHA,
              "expected_receipt_sha256_annotation": RECEIPT_SHA,
              "candidate_authenticated": False, "sequential_reads_not_atomic": True,
              "runtime_pass": False, "payload_valid": False, "gate_reread_consistent": False}
    result.update(read_fields(client, FIELDS))
    try:
        require_baseline(result)
        witnesses = []
        for index, private in WIZARDS:
            at = MOBY + index * 88
            raw = client.read(at, 88)
            row = one.decode_moby(raw, index, at)
            result[f"wizard{index}"] = row
            if row["class_id"] != 0x11B or row["properties"] != private:
                raise ValueError(f"Wizard{index} class/properties is unrelocated or outside its issued owner.")
            link = client.read(private, 4)
            result[f"wizard{index}_private_link"] = struct.unpack("<I", link)[0]
            if result[f"wizard{index}_private_link"] != private + 0x28:
                raise ValueError(f"Wizard{index} nested private pointer is unrelocated or changed.")
            witnesses.extend(((at, raw), (private, link)))
        result["dynamic_rows"] = scan_dynamic(client)
        pool = b"".join(client.read(POOL + at, 1024) for at in range(0, 0x2000, 1024))
        result["particles"] = one.scan_particles(pool)
        result["particle_terminal_word_not_record257"] = client.read(PETE, 4).hex()
        if result["particle_terminal_word_not_record257"] != "ffffffff":
            raise ValueError("Native particle terminal owner changed or is not initialized.")
        after = read_fields(client, REREAD_FIELDS)
        for name in REREAD_FIELDS:
            if after[name] != result[name]:
                raise ValueError("Gate/cursor/owner word changed during sequential payload reads: " + name)
        for at, before in witnesses:
            if client.read(at, len(before)) != before:
                raise ValueError("Placed/private owner changed during sequential payload reads.")
        result["gate_reread_consistent"] = True
        result["payload_valid"] = True
        result["primitive_sampled_slack"], result["primitive_sampled_slack_unavailable_reason"] = one.primitive_slack(
            result["primitive_cursor"], result["primitive_limit"])
        if result["dynamic_count"] >= CAPACITY - 21 or result["dynamic_rows"]["no_in_range_terminal"]:
            result["attention"] = "Pause in UI: dynamic headroom/terminal requires inspection, not a peak fit certificate."
        if result["renderer_exhaustion_flag"]:
            result["attention"] = "Pause in UI: native renderer exhaustion flag observed."
    except ValueError as error:
        result["payload_refused"] = str(error)
        result["attention"] = "Pause in UI; this is not an accepted native-layout/gameplay sample."
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--connect-identified-test-session", action="store_true")
    args = parser.parse_args()
    if not args.connect_identified_test_session:
        parser.error("Independently identify the exact two-Wizard process/open BIN first.")
    with socket.create_connection(("127.0.0.1", 2345), timeout=2) as connection:
        client = ReadOnlyClient(connection)
        print("READ_ONLY_TWO_WIZARDS_CONNECTED; pause in UI before p <label>; q closes. Control only through UI.", flush=True)
        for line in sys.stdin:
            command = line.strip()
            if command == "q":
                break
            if command == "p" or command.startswith("p "):
                print(json.dumps(capture(client, command[1:].strip())), flush=True)


if __name__ == "__main__":
    main()
