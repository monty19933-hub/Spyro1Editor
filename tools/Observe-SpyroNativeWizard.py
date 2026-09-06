#!/usr/bin/env python3
"""Manual, read-only observations for the exact Dark one-Wizard research profile.

Only connect AFTER identifying DuckStation's process and exact open candidate
BIN externally. Initial GDB connection pauses emulation; resume/pause via UI.
This tool sends memory reads and protocol ACKs only. It never writes RAM, sends
execution commands or creates an automatic gameplay/peak-usage certificate.
Use `p label` while paused; samples are sequential, not atomic CPU snapshots.
"""
from __future__ import annotations

import argparse
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import socket
import struct
import sys

_spec = importlib.util.spec_from_file_location("wizard_observer_rsp", Path(__file__).with_name("Watch-SpyroId65RuntimeState.py"))
rsp = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = rsp
_spec.loader.exec_module(rsp)

PROFILE = "native-dark-wizard-authored-dry-corridor-placement-research-v1"
EXPECTED_DATA_SHA256 = "6ed74dbce04d6712e0d9ac12cfd05ae6ac334e0ec39fb7420f2105076132e537"
EXPECTED_ARENA = {"moby_base": 0x8013AE1C, "dynamic_base": 0x8013DA74,
                  "arena_end": 0x8014021C, "dynamic_capacity": 90}
# Fixed candidate scene rows begin at scene+8C38; the appended private owner is
# scene+EED4. This relocated pointer is a necessary scan gate, not complete
# loader-state or candidate identity proof. Exact raw death/reload rows can
# retain class11B while holding EED4 and stale, otherwise matching arena globals.
EXPECTED_WIZARD_PROPERTIES = EXPECTED_ARENA["moby_base"] - 0x8C38 + 0xEED4
FIELDS = {
    "level": (0x8007596C, "<I"), "game_state": (0x800757D8, "<I"),
    "lives": (0x8007582C, "<I"), "player_xyz": (0x80078A58, "<iii"),
    # Native facing byte; not the camera angle or a movement command.
    "player_heading_u8": (0x80078A66, "<B"),
    # EXE8003D4BC..4F0 adds this signed halfword to the pad-table bearing,
    # masks to12 bits, then stores the desired bearing at8003D50C..514.
    "camera_control_angle_s16": (0x80076E20, "<h"),
    "player_desired_bearing_u32": (0x80078B24, "<I"),
    "player_state": (0x80078AD0, "<I"), "player_substate": (0x80078AD4, "<I"),
    "player_state_timer": (0x80078AD8, "<I"),
    # EXE8003CC30..4C loads state, indexes8006C470, and stores this byte.
    "player_animation_id": (0x80078A71, "<B"),
    "moby_base": (0x80075828, "<I"), "dynamic_base": (0x80075890, "<I"),
    "dynamic_count": (0x800756A4, "<I"), "dynamic_capacity": (0x800756A8, "<I"),
    "arena_end": (0x800756E8, "<I"), "dynamic_properties_cursor": (0x80075930, "<I"),
    "next_free_moby_not_active_end": (0x8007573C, "<I"),
    "particle_base": (0x80075824, "<I"), "next_free_particle_not_active_end": (0x80075738, "<I"),
    "wizard_model_root": (0x800767E4, "<I"), "bolt_model_root": (0x80076410, "<I"),
    "reward_model_root": (0x800764D0, "<I"), "player_root": (0x80076378, "<I"),
    "sound_map": (0x800761D4, "<I"), "sound_descriptors": (0x800761D0, "<I"),
    "particle7_descriptor": (0x80076294, "<I"),
    "primitive_cursor": (0x800757B0, "<I"), "primitive_limit": (0x80075780, "<I"),
    "ordering_table": (0x80075820, "<I"), "renderer_exhaustion_flag": (0x800758B0, "<I"),
    "last_collision_saved_sp_not_peak": (0x80077DFC, "<I"),
}


class ReadOnlyClient:
    def __init__(self, connection): self.connection = connection

    def read(self, address, length):
        if type(address) is not int or type(length) is not int or not 1 <= length <= 1024 or not 0x80000000 <= address <= 0x80200000 - length:
            raise ValueError("Observation read is outside bounded main RAM; no packet sent.")
        self.connection.sendall(rsp.frame_payload(f"m{address:08x},{length:x}"))
        reply = rsp.read_data_reply(self.connection)
        if len(reply) != length * 2 or any(c not in b"0123456789abcdefABCDEF" for c in reply):
            raise ValueError("Observation reply is not the exact requested hexadecimal owner.")
        return bytes.fromhex(reply.decode("ascii"))


def decode_moby(row, index, address):
    if len(row) != 0x58: raise ValueError("A native row requires exactly0x58 bytes.")
    return {"index": index, "address": address, "class_id": struct.unpack_from("<H", row, 0x36)[0],
            "xyz": list(struct.unpack_from("<iii", row, 12)), "properties": struct.unpack_from("<I", row)[0],
            "state_bytes": list(row[0x48:0x4A]), "animation_raw": row[0x3C:0x40].hex(),
            "reward_selector": row[0x53], "raw_hex": bytes(row).hex()}


def scan_dynamic(rows, start):
    active = []; terminal = None; consumed = 0
    for slot, row in enumerate(rows):
        if slot >= 90: raise ValueError("Dynamic row scan exceeds exact candidate capacity.")
        if len(row) != 88: raise ValueError("Partial native dynamic row.")
        if row[0x48] == 0xFF: terminal = slot; break
        consumed += 1
        if row[0x48] < 0x80: active.append(decode_moby(row, 129 + slot, start + slot * 88))
    return {"active": active, "terminal_slot": terminal,
            "high_water_rows_before_terminal": consumed,
            "no_in_range_terminal": terminal is None}


def scan_particles(pool):
    if len(pool) != 0x2000: raise ValueError("Particle pool must have exactly256 native32-byte records.")
    counts = Counter(); samples = []; active = 0; terminal = None
    for i in range(256):
        row = pool[i * 32:(i + 1) * 32]
        if row[1] == 0xFF: terminal = i; break
        if row[1] >= 0x80: continue
        active += 1; counts[f"{row[0]:02X}/kind{row[1]}"] += 1
        if row[0] in (7, 0x41) and len(samples) < 16:
            samples.append({"slot": i, "selector": row[0], "kind": row[1], "age": row[2],
                            "visibility": row[3], "raw_hex": bytes(row).hex()})
    return {"active_count": active, "terminal_slot": terminal, "selector_kind_counts": dict(counts),
            "wizard_effect_samples": samples, "all256_slots_visited": terminal is None}


def require_arena(values):
    for name, expected in EXPECTED_ARENA.items():
        if values.get(name) != expected: raise ValueError("Wrong/uninitialized candidate arena: " + name)
    if not 0 <= values["dynamic_count"] <= 90: raise ValueError("Native dynamic count exceeds issued capacity.")
    cursor = values["dynamic_properties_cursor"]
    if not values["arena_end"] - 90 * 24 <= cursor <= values["arena_end"] or (values["arena_end"] - cursor) % 24:
        raise ValueError("Native properties cursor escaped the issued90-slot grid.")
    if values["dynamic_base"] + 90 * 88 > values["arena_end"] - 90 * 24:
        raise ValueError("Issued row and property capacity extents overlap.")


def primitive_slack(cursor, limit):
    """One sampled native limit-minus-cursor-minus0x400; never a peak claim."""
    if type(cursor) is not int or type(limit) is not int:
        return None, "Primitive cursor/limit are not integer main-RAM pointers."
    # The limit is exclusive, so exactly80200000 is a valid RAM endpoint.
    if not 0x80000000 <= cursor <= 0x80200000 or not 0x80000000 < limit <= 0x80200000:
        return None, "Primitive cursor/limit are outside main RAM or uninitialized."
    if cursor > limit:
        return None, "Primitive cursor is beyond its limit; sequential reads may be inconsistent."
    return limit - cursor - 0x400, None


def capture(client, label):
    result = {"utc": datetime.now(timezone.utc).isoformat(), "label": label[:120],
              "expected_profile_annotation": PROFILE, "expected_data_sha256_annotation": EXPECTED_DATA_SHA256,
              "sequential_reads_not_atomic": True, "runtime_pass": False}
    for name, (address, encoding) in FIELDS.items():
        values = struct.unpack(encoding, client.read(address, struct.calcsize(encoding)))
        result[name] = values[0] if len(values) == 1 else list(values)
    if result["level"] != 12:
        result["actor_scan"] = "Skipped: not Dark Hollow. Profile annotations do not certify the loaded candidate."
        return result
    try:
        require_arena(result)
        actor_address = result["moby_base"] + 128 * 88
        result["wizard128"] = decode_moby(client.read(actor_address, 88), 128, actor_address)
        if result["wizard128"]["class_id"] != 0x11B: raise ValueError("Appended row128 is not the source-bound Wizard.")
        if result["wizard128"]["properties"] != EXPECTED_WIZARD_PROPERTIES:
            raise ValueError("Wizard properties are unrelocated or outside the exact issued owner; loader phase or layout is unresolved.")
        # Both upward rows and downward property allocation share this arena.
        # The source-issued90-row extent ends BEFORE all90 property slots. A
        # reused property cursor is not the lowest live-property address, and
        # neither next-free pointer is a valid active-list end.
        result["dynamic_rows"] = scan_dynamic(
            (client.read(result["dynamic_base"] + i * 88, 88) for i in range(90)), result["dynamic_base"])
        if result["dynamic_count"] >= 69 or result["dynamic_rows"]["no_in_range_terminal"]:
            result["attention"] = "Pause in UI: native dynamic capacity/headroom or terminal requires inspection."
        if result["particle_base"] != 0x801C3BB8: raise ValueError("Wrong normal particle pool owner.")
        pool = b"".join(client.read(0x801C3BB8 + at, 1024) for at in range(0, 0x2000, 1024))
        result["particles"] = scan_particles(pool)
        result["particle_terminal_word_not_record257"] = client.read(0x801C5BB8, 4).hex()
        result["primitive_sampled_slack"], result["primitive_sampled_slack_unavailable_reason"] = primitive_slack(
            result["primitive_cursor"], result["primitive_limit"])
        if result["renderer_exhaustion_flag"] != 0:
            result["attention"] = "Pause in UI: native renderer exhaustion flag observed."
    except ValueError as error:
        result["actor_scan_refused"] = str(error)
        result["attention"] = "Pause in UI; this sample is not a native-layout or gameplay pass."
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--connect-identified-test-session", action="store_true",
                        help="Confirm the exact test process/open BIN was independently identified; onlylocalhost2345 is used.")
    args = parser.parse_args()
    if not args.connect_identified_test_session: parser.error("Identify the test process/open candidate first, then explicitly opt in.")
    with socket.create_connection(("127.0.0.1", 2345), timeout=2) as connection:
        client = ReadOnlyClient(connection)
        print("READ_ONLY_WIZARD_CONNECTED; p <label> samples, q closes. Resume/pause only via UI.", flush=True)
        for line in sys.stdin:
            command = line.strip()
            if command == "q": break
            if command == "p" or command.startswith("p "):
                print(json.dumps(capture(client, command[1:].strip())), flush=True)


if __name__ == "__main__": main()
