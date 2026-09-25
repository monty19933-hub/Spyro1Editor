#!/usr/bin/env python3
"""Manual read-only samples for the fixed two-F5 USA Artisans experiment.

Identify the actual DuckStation PID and open candidate BIN externally first.
Connect only to that isolated session. Initial connection can pause execution;
pause/resume and all gameplay input stay in the UI. These sequential RAM reads
are not atomic snapshots or a runtime pass. No sampled pointer becomes a read
address; no card, settings, file, execution or memory writes are performed.
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
_spec = importlib.util.spec_from_file_location(
    "japanese_egg_read_primitives", Path(__file__).with_name("Observe-SpyroNativeWizard.py"))
wizard = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = wizard
_spec.loader.exec_module(wizard)
ReadOnlyClient = wizard.ReadOnlyClient

PROFILE = "native-artisans-two-japanese-eggs-visual-research-v1"
# Independently derived from the actual source-issued model/scene append.
# Static bounds, NOT offsets supplied by a receipt or observed RAM pointers.
SCENE, ROOT = 0x80164D54, 0x80163D54
ROWS = (0x80172BD0, 0x80172C28)
PRIVATE = (0x80176C20, 0x80176C2C)
COLORS = (0x801641B0, 0x801642C8)
GATES = MappingProxyType({
    "level": (0x8007596C, "<I", 10),
    "game_state": (0x800757D8, "<I", 0),
    "normal_classifier": (0x80075690, "<I", 0),
    "copy_anchor": (0x800785D8, "<I", 0x80089238),
    "model_anchor": (0x800785E0, "<I", 0x80116554),
    "scene_anchor": (0x800785E4, "<I", SCENE),
    "scene_disk_offset": (0x80078600, "<I", 0x1CA800),
    "scene_disk_length": (0x80078604, "<I", 0x14800),
    "moby_base": (0x80075828, "<I", 0x8016F000),
    "dynamic_base": (0x80075890, "<I", 0x80172C80),
    "dynamic_capacity": (0x800756A8, "<I", 89),
    "egg_model_root": (0x8007674C, "<I", ROOT),
})
FIELDS = MappingProxyType({name: (address, fmt) for name, (address, fmt, _) in GATES.items()} | {
    name: wizard.FIELDS[name] for name in (
        "lives", "player_xyz", "player_heading_u8", "camera_control_angle_s16",
        "player_desired_bearing_u32", "player_state", "player_substate",
        "primitive_cursor", "primitive_limit", "renderer_exhaustion_flag", "dynamic_count")
})


def read_fields(client, names):
    result = {}
    for name in names:
        address, fmt = FIELDS[name]
        value = struct.unpack(fmt, client.read(address, struct.calcsize(fmt)))
        result[name] = value[0] if len(value) == 1 else list(value)
    return result


def require_layout(values):
    for name, (_, _, expected) in GATES.items():
        if values[name] != expected:
            raise ValueError("Wrong/uninitialized fixed egg layout: " + name)
    if not 0 <= values["dynamic_count"] <= 89:
        raise ValueError("Dynamic count exceeds the exact native arena capacity.")


def require_row(row, index):
    if len(row) != 0x58:
        raise ValueError("Partial egg row.")
    if struct.unpack_from("<H", row, 0x36)[0] != 0xF5:
        raise ValueError("Appended actor is not F5.")
    if struct.unpack_from("<I", row)[0] != PRIVATE[index]:
        raise ValueError("Unrelocated or different private owner; no pointer chase.")
    if row[0x4B] != 0x94 or row[0x4F] != 1 or row[0x48] != 0:
        raise ValueError("Different format/mode/live state; visual profile not initialized.")
    if struct.unpack_from("<ii", row, 12) != (72704, 47104 + index * 2048):
        raise ValueError("Actor is not at its fixed source-inspected XY site.")


def capture(client, label):
    result = {"utc": datetime.now(timezone.utc).isoformat(), "label": label[:120],
              "profile_annotation": PROFILE, "sequential_reads_not_atomic": True,
              "candidate_authenticated": False, "runtime_pass": False,
              "pickup_verified": False, "save_verified": False}
    result.update(read_fields(client, FIELDS))
    try:
        require_layout(result)
        rows = [client.read(address, 0x58) for address in ROWS]
        for index, row in enumerate(rows):
            require_row(row, index)
        eggs = []
        for index, row in enumerate(rows):
            observed = wizard.decode_moby(row, 174 + index, ROWS[index])
            observed["yaw_u8"] = row[0x46]
            observed["visible_raw_u8"] = row[0x51]
            observed["private_words_raw"] = list(struct.unpack("<III", client.read(PRIVATE[index], 12)))
            eggs.append(observed)
        colors = [client.read(address, 0x118) for address in COLORS]
        # Re-read all layout gates and both rows' identity, not mutable animation
        # or bob fields. This detects known transitions, not every possible race.
        after = read_fields(client, tuple(GATES) + ("dynamic_count",))
        require_layout(after)
        if any(result[name] != after[name] for name in after):
            raise ValueError("Layout/capacity state changed during sequential sample.")
        for index, address in enumerate(ROWS):
            require_row(client.read(address, 0x58), index)
        result["eggs"] = eggs
        result["native_model_color_arrays"] = [
            {"role": role, "address": address, "sha256": hashlib.sha256(data).hexdigest(),
             "first_packed_word": struct.unpack_from("<I", data)[0], "words": 70}
            for role, address, data in zip(("near", "far"), COLORS, colors)]
        result["primitive_sampled_slack"], result["primitive_slack_reason"] = wizard.primitive_slack(
            result["primitive_cursor"], result["primitive_limit"])
        if result["renderer_exhaustion_flag"]:
            result["attention"] = "Pause in UI: renderer exhaustion flag observed."
    except (ValueError, struct.error) as error:
        result["actor_sample_refused"] = str(error)
        result["attention"] = "Pause in UI; sample is not a layout/gameplay pass."
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--connect-identified-test-session", action="store_true")
    args = parser.parse_args()
    if not args.connect_identified_test_session:
        parser.error("Identify the exact isolated PID/open BIN before explicitly opting in.")
    with socket.create_connection(("127.0.0.1", 2345), timeout=2) as connection:
        client = ReadOnlyClient(connection)
        print("READ_ONLY_JP_EGGS_CONNECTED; p <label> samples, q closes. UI-only input/resume.", flush=True)
        for line in sys.stdin:
            command = line.strip()
            if command == "q":
                break
            if command == "p" or command.startswith("p "):
                print(json.dumps(capture(client, command[1:].strip())), flush=True)


if __name__ == "__main__":
    main()
