#!/usr/bin/env python3
"""Manual, read-only observations for the private Fresh chest P9/N32 experiment.

Independently identify DuckStation and its exact open BIN/CUE before connecting.
Hashes below are annotations, never loaded-disc authentication. The initial GDB
connection may pause emulation; resume/pause only in the UI. Use p <label> and q.
--profile defaults to r01; r02/r03 only select floor/destination annotations and
does not change runtime gates, addresses or read bounds. Unverified r02 payload
hashes are omitted, not inferred from another candidate or the selected label.
Only bounded memory-read packets and protocol acknowledgements are sent. No
execution, RAM writes, automatic input, file output or gameplay certification.
--self-test is offline and mutually exclusive with connecting.
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
from types import MappingProxyType

sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location(
    "fresh_chest_read_only_primitives", Path(__file__).with_name("Observe-SpyroNativeWizard.py"))
wizard = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = wizard
_spec.loader.exec_module(wizard)
ReadOnlyClient = wizard.ReadOnlyClient
decode_moby = wizard.decode_moby
# Do NOT import the legacy Dark level12/90-row capture or scanner.

PROFILE = "private-fresh-chest-p9-runtime-experiment-v1"
ANNOTATIONS = MappingProxyType({
    "scene_sha256": "075c84cfd5ec13dc245dfa8b888f349903056b07676fc1990c494875e90f20d3",
    "body_sha256": "77fdfded0383af6636ffa991c47d55f113b0f17327584213bb60a72494529d45",
    "executable_sha256": "81501f07ec9ce336089ec5a92f81bf51569aed723ac4f8ba1cf85ba0ef69a431",
})  # Source483: these are NOT a full-image identity or CLI certificate.
# Closed, deeply immutable annotations only. Both experiments have the same
# native memory layout below; no profile supplies a pointer, bound or gate.
# Preserve R01's original output fields/values by leaving its geometry absent.
PROFILES = MappingProxyType({
    "r01": MappingProxyType({
        "profile": PROFILE,
        "payload": ANNOTATIONS,
        "geometry": None,
    }),
    "r02": MappingProxyType({
        "profile": "private-fresh-chest-p9-runtime-experiment-v2",
        "payload": MappingProxyType({}),  # Add only after actual emitted payload verification.
        "geometry": MappingProxyType({
            "floor_xyz": (8448, 8448, 8704),
            "floor_width_depth": (3072, 3072),
            "spawn_xyz": (8960, 8960, 9216),
            "key_xyz": (9728, 9120, 8704),
            "chest_xyz": (10864, 9888, 8704),
            "chest_yaw_u8": 0xDC,
            "reward_relative_xyz": ((-410, 1229, 512), (0, 1229, 512),
                                    (-615, 205, 512), (-410, 615, 512), (204, 819, 512)),
            "annotations_not_sampled_or_authenticated": True,
        }),
    }),
    "r03": MappingProxyType({
        "profile": "private-fresh-chest-p9-runtime-experiment-v3",
        "payload": MappingProxyType({}),
        "geometry": MappingProxyType({
            "floor_xyz": (8448, 8448, 8704),
            "floor_width_depth": (3072, 3072),
            "spawn_xyz": (8960, 8960, 9216),
            "key_xyz": (9728, 9120, 8704),
            "chest_xyz": (10864, 9888, 8704),
            "chest_yaw_u8": 0xDC,
            "authored_world_destination_overrides": (("reward.2", 10454, 10503, 9216),),
            "reward_destination_xyz": ((10454, 11117, 9216), (10454, 10503, 9216),
                                       (10249, 10093, 9216), (10454, 10503, 9216), (11068, 10707, 9216)),
            "annotations_not_sampled_or_authenticated": True,
        }),
    }),
})
COPY, USED, MODEL, SCENE = 0x8008CA38, 0xA6C, 0x8008D4A4, 0x800A84A4
ROWS, DYNAMIC, ARENA_END = SCENE + 0xD4, SCENE + 0x3EC, SCENE + 0x1244
PLACED, CAPACITY, STRIDE = 9, 32, 0x58
# FamilyChestBodyCases and source_fresh_chest_scene_extension_oracle.py own
# these complete extents. The allocator lookahead at scene+EEC is NOT row33.
PRIVATE_OFFSETS = (0x1248, 0x1298, 0x12A0, 0x12A4, 0x12BC, 0x12E4, 0x130C, 0x1334, 0x135C)
PRIVATE_LENGTHS = (0x50, 8, 4, 0x18, 0x28, 0x28, 0x28, 0x28, 0x28)
INITIAL_CLASSES = (0x11B, 0x1F5, 0xAD, 0xAE, 0xD, 0xD, 0xD, 0xD, 0xD)
# Existing normal-resident BodyCases and both prior observers pin this 0x2000
# pool followed by a separate PETE first word; never read particle record257.
POOL, PETE = 0x801C3BB8, 0x801C5BB8
GATES = MappingProxyType({
    "level": (0x8007596C, "<I", 65),
    "treasure_slot": (0x80075964, "<I", 35),
    "game_state": (0x800757D8, "<I", 0),
    "normal_classifier": (0x80075690, "<I", 0),
    "copy_anchor": (0x800785D8, "<I", COPY),
    "copy_cursor": (0x800785DC, "<I", COPY),
    "model_anchor": (0x800785E0, "<I", MODEL),
    "scene_anchor": (0x800785E4, "<I", SCENE),
    "scene_disk_offset": (0x80078600, "<I", 0x102000),
    "scene_disk_length": (0x80078604, "<I", 0x3800),
    "moby_base": (0x80075828, "<I", ROWS),
    "dynamic_base": (0x80075890, "<I", DYNAMIC),
    "arena_end": (0x800756E8, "<I", ARENA_END),
    "bucket_base": (0x80075778, "<I", SCENE + 0x1390),
    "dynamic_capacity": (0x800756A8, "<I", CAPACITY),
    "particle_allocation_base": (0x800785F8, "<I", POOL),
    "particle_base": (0x80075824, "<I", POOL),
    # Exact retained Town callback values from the source450 initializer.
    "callback_spawn": (0x800758CC, "<I", 0x800872A4),
    "callback_effect_spawn": (0x800758E4, "<I", 0x80088F68),
})
CURSORS = MappingProxyType({
    "dynamic_count": (0x800756A4, "<I"),
    "dynamic_properties_cursor": (0x80075930, "<I"),
    "next_free_moby_not_active_end": (0x8007573C, "<I"),
})
_player_names = (
    "lives", "player_xyz", "player_heading_u8", "camera_control_angle_s16",
    "player_desired_bearing_u32", "player_state", "player_substate",
    "player_state_timer", "player_animation_id", "next_free_particle_not_active_end",
    "primitive_cursor", "primitive_limit", "renderer_exhaustion_flag",
)
FIELDS = MappingProxyType({name: (address, fmt) for name, (address, fmt, _) in GATES.items()} |
    dict(CURSORS) | {name: wizard.FIELDS[name] for name in _player_names} | {
        # Scalar observations only. Neither global is chased as a pointer.
        "key_state_raw_u32": (0x80075830, "<I"),
        "tracked_chest_gem_raw_u32": (0x80075758, "<I"),
        # Retained EXE award/HUD paths use distinct native-width owners. The
        # current slot is gated above; no sampled index becomes a read address.
        # See native-town-square-chest-runtime-observation-contract.md:48-83.
        "actual_global_gems": (0x80075860, "<I"),
        "actual_slot35_gems": (0x80077420 + 4 * 35, "<I"),
        "animated_hud_gems": (0x80077FC8, "<I"),
        "slot35_target_s16": (0x8006F438 + 2 * 35, "<h"),
        # Existing source-bound P1 observer fields, kept distinct: state6 is
        # neither proof of damage nor proof of an initiating collider contact.
        "player_health_s32": (0x80078BBC, "<i"),
        "player_damage_accumulator_u32": (0x80078A84, "<I"),
        "player_motion_78b4c_s32x3": (0x80078B4C, "<iii"),
        "player_motion_78b64_s32x3": (0x80078B64, "<iii"),
        "player_motion_78ba4_s32": (0x80078BA4, "<i"),
        "player_state_input_78af4_s32": (0x80078AF4, "<i"),
        "player_state_input_78b08_s32": (0x80078B08, "<i"),
        "companion_handle_raw_u32": (0x80075898, "<I"),
    })
REREAD = tuple(GATES) + tuple(CURSORS)
AUTHORITY_FLAGS = ("candidate_authenticated", "source_certified", "runtime_pass",
                   "publication_approved", "complete_census_proven",
                   "key_zero_contribution_proven", "full_lifecycle_proven")


def read_fields(client, names):
    result = {}
    for name in names:
        address, fmt = FIELDS[name]
        values = struct.unpack(fmt, client.read(address, struct.calcsize(fmt)))
        result[name] = values[0] if len(values) == 1 else list(values)
    return result


def require_layout(values):
    for name, (_, _, expected) in GATES.items():
        if values[name] != expected:
            raise ValueError("Wrong/uninitialized P9/N32 layout gate: " + name)
    if not 0 <= values["dynamic_count"] <= CAPACITY:
        raise ValueError("Native dynamic count escaped0..32.")
    cursor = values["dynamic_properties_cursor"]
    if not ARENA_END - CAPACITY * 24 <= cursor <= ARENA_END or (ARENA_END - cursor) % 24:
        raise ValueError("Dynamic properties cursor escaped the exact32-slot grid.")
    # next_free_moby is reported/reread, not an active end or read bound. Do not
    # add an unproven lifecycle equality between it, count and property cursor.


def scan_dynamic(rows):
    active, terminal, consumed = [], None, 0
    iterator = iter(rows)
    for slot in range(CAPACITY):
        try:
            row = next(iterator)
        except StopIteration as error:
            raise ValueError("Missing in-bound dynamic row before terminal/capacity.") from error
        if len(row) != STRIDE:
            raise ValueError("Partial native dynamic row.")
        if row[0x48] == 0xFF:
            terminal = slot
            break
        consumed += 1
        if row[0x48] < 0x80:
            active.append(decode_moby(row, PLACED + slot, DYNAMIC + slot * STRIDE))
    return {"active": active, "terminal_slot": terminal,
            "high_water_rows_before_terminal": consumed, "no_in_range_terminal": terminal is None}


def scan_particles(pool):
    if len(pool) != 0x2000:
        raise ValueError("Particle pool requires256 complete32-byte rows.")
    counts, samples, terminal, active = Counter(), [], None, 0
    for slot in range(256):
        row = pool[slot * 32:(slot + 1) * 32]
        if row[1] == 0xFF:
            terminal = slot
            break
        if row[1] >= 0x80:
            continue
        active += 1
        counts[f"{row[0]:02X}/kind{row[1]}"] += 1
        if row[0] in (0x0C, 0x46, 7, 0x41) and len(samples) < 16:
            samples.append({"slot": slot, "selector": row[0], "kind": row[1], "raw_hex": row.hex()})
    return {"active_count": active, "terminal_slot": terminal, "selector_kind_counts": dict(counts),
            "effect_samples": samples, "all256_slots_visited": terminal is None}


def capture(client, label, profile="r01"):
    if type(profile) is not str or profile not in PROFILES:
        raise ValueError("Unsupported chest annotation profile; expected r01 or r02.")
    annotations = PROFILES[profile]
    result = dict.fromkeys(AUTHORITY_FLAGS, False)
    result.update(utc=datetime.now(timezone.utc).isoformat(), label=str(label)[:120],
                  expected_profile_annotation=annotations["profile"],
                  expected_payload_annotations=dict(annotations["payload"]),
                  sequential_reads_not_atomic=True, bounded_sample_valid=False, gate_reread_consistent=False)
    if annotations["geometry"] is not None:
        result["expected_geometry_annotations"] = dict(annotations["geometry"])
    try:
        result.update(read_fields(client, FIELDS))
        require_layout(result)  # No actor, private, particle or model reads before this.
        placed = []
        for index in range(PLACED):
            address = ROWS + index * STRIDE
            row = decode_moby(client.read(address, STRIDE), index, address)
            row["initial_class_annotation"] = INITIAL_CLASSES[index]
            row["active_status_sample"] = row["state_bytes"][0] < 0x80
            owner, length = SCENE + PRIVATE_OFFSETS[index], PRIVATE_LENGTHS[index]
            if not row["active_status_sample"]:
                row["private_scan_skipped"] = "Inactive/deleted row; initial class is not a lifecycle gate."
            elif row["properties"] != owner:
                row["private_scan_skipped"] = "Pointer does not match this row's exact relocated static owner."
            else:
                # Read the fixed code-owned address, NOT an arbitrary sampled
                # pointer. No nested private/model/matrix pointers are followed.
                data = client.read(owner, length)
                if len(data) != length:
                    raise ValueError("Partial known static private owner.")
                row["private_owner_sample"] = {"address": owner, "length": length,
                    "raw_hex": data.hex(), "words_u32": list(struct.unpack("<" + "I" * (length // 4), data))}
            placed.append(row)
        dynamic = scan_dynamic(client.read(DYNAMIC + i * STRIDE, STRIDE) for i in range(CAPACITY))
        particles = scan_particles(b"".join(client.read(POOL + at, 1024) for at in range(0, 0x2000, 1024)))
        terminal = client.read(PETE, 4).hex()
        after = read_fields(client, REREAD)
        changed = [name for name in REREAD if result[name] != after[name]]
        if changed:
            result["changed_gate_fields"] = changed
            raise ValueError("Layout/cursor fields changed during sequential payload sampling.")
        result.update(bounded_sample_valid=True, gate_reread_consistent=True, placed_rows=placed,
                      dynamic_rows=dynamic, particles=particles, particle_terminal_word_not_record257=terminal)
        slack, reason = wizard.primitive_slack(result["primitive_cursor"], result["primitive_limit"])
        result.update(primitive_sampled_slack=slack, primitive_slack_unavailable_reason=reason)
        attention = []
        if dynamic["no_in_range_terminal"] or result["dynamic_count"] == CAPACITY:
            attention.append("Dynamic capacity/terminal requires manual inspection; no33rd row was read.")
        if particles["all256_slots_visited"] or terminal != "ffffffff":
            attention.append("Particle capacity/separate PETE first word requires inspection; no257th record was read.")
        if result["renderer_exhaustion_flag"] or slack is None or slack < 0:
            attention.append("Renderer flag/sampled slack requires inspection; this is not a peak-usage measure.")
        if any("private_scan_skipped" in row and row["active_status_sample"] for row in placed):
            attention.append("An active row has an unresolved private owner; its private bytes were not read.")
        if attention:
            result["attention"] = attention
    except (ValueError, struct.error) as error:
        result["actor_scan_refused"] = str(error)
        result["attention"] = ["Pause in UI and inspect; no actor/private interpretation is published from this sample."]
    return result


def self_test():
    """Small hostile fixtures; no sockets, original image, emulator or files."""
    import contextlib
    import io
    import unittest
    from unittest.mock import patch

    class Memory:
        def __init__(self):
            self.data, self.reads, self.flip = {}, [], None

        def map(self, address, data):
            self.data.update((address + i, value) for i, value in enumerate(data))

        def read(self, address, length):
            if not 1 <= length <= 1024:
                raise AssertionError("Unbounded read")
            self.reads.append((address, length))
            if self.flip and address == self.flip[0] and self.reads.count((address, length)) == 2:
                self.map(address, struct.pack("<I", self.flip[1]))
            return bytes(self.data[address + i] for i in range(length))

    def row(index, state=0):
        data = bytearray(STRIDE)
        struct.pack_into("<I", data, 0, SCENE + PRIVATE_OFFSETS[index])
        struct.pack_into("<H", data, 0x36, INITIAL_CLASSES[index])
        data[0x48] = state
        return data

    def fixture():
        memory = Memory()
        for name, (address, fmt) in FIELDS.items():
            values = [0] * len(struct.unpack(fmt, bytes(struct.calcsize(fmt))))
            values[0] = GATES[name][2] if name in GATES else 0
            if name == "dynamic_properties_cursor": values[0] = ARENA_END
            if name == "next_free_moby_not_active_end": values[0] = DYNAMIC
            memory.map(address, struct.pack(fmt, *values))
        for index in range(9):
            memory.map(ROWS + index * STRIDE, row(index))
            memory.map(SCENE + PRIVATE_OFFSETS[index], bytes(PRIVATE_LENGTHS[index]))
        memory.map(DYNAMIC, bytes(32 * STRIDE))
        memory.map(DYNAMIC + 0x48, b"\xff")
        memory.map(POOL, bytes(0x2000)); memory.map(POOL + 1, b"\xff")
        memory.map(PETE, b"\xff" * 4)
        return memory

    class Tests(unittest.TestCase):
        def test_exact_layout_and_complete_bounded_owners(self):
            self.assertEqual((0x800A8578, 0x800A8890, 0x800A96E8), (ROWS, DYNAMIC, ARENA_END))
            self.assertEqual(MODEL, COPY + USED); self.assertEqual(SCENE, MODEL + 0x1B000)
            self.assertEqual(0x13C, sum(PRIVATE_LENGTHS))
            self.assertEqual(DYNAMIC + 33 * STRIDE, ARENA_END - 32 * 24)
            memory = fixture(); sample = capture(memory, "offline")
            self.assertTrue(sample["bounded_sample_valid"])
            self.assertTrue(sample["gate_reread_consistent"])
            self.assertEqual(9, len(sample["placed_rows"]))
            for index in range(9):
                self.assertIn((SCENE + PRIVATE_OFFSETS[index], PRIVATE_LENGTHS[index]), memory.reads)
            self.assertEqual(10, sum(length == STRIDE for _, length in memory.reads))
            self.assertIn((PETE, 4), memory.reads)
            self.assertTrue(all(sample[name] is False for name in AUTHORITY_FLAGS))
            self.assertNotIn("expected_candidate_sha256", sample)

        def test_closed_profiles_preserve_default_and_identical_read_bounds(self):
            baseline = fixture(); default = capture(baseline, "same")
            explicit_memory = fixture(); explicit = capture(explicit_memory, "same", "r01")
            self.assertEqual(list(default), list(explicit))
            self.assertEqual({k: v for k, v in default.items() if k != "utc"},
                             {k: v for k, v in explicit.items() if k != "utc"})
            self.assertEqual("private-fresh-chest-p9-runtime-experiment-v1", default["expected_profile_annotation"])
            self.assertEqual({
                "scene_sha256": "075c84cfd5ec13dc245dfa8b888f349903056b07676fc1990c494875e90f20d3",
                "body_sha256": "77fdfded0383af6636ffa991c47d55f113b0f17327584213bb60a72494529d45",
                "executable_sha256": "81501f07ec9ce336089ec5a92f81bf51569aed723ac4f8ba1cf85ba0ef69a431",
            }, default["expected_payload_annotations"])
            self.assertNotIn("expected_geometry_annotations", default)
            self.assertEqual(baseline.reads, explicit_memory.reads)
            wide_memory = fixture(); wide = capture(wide_memory, "same", "r02")
            self.assertEqual(baseline.reads, wide_memory.reads)
            self.assertEqual({}, wide["expected_payload_annotations"])
            self.assertEqual("private-fresh-chest-p9-runtime-experiment-v2", wide["expected_profile_annotation"])
            geometry = wide["expected_geometry_annotations"]
            self.assertEqual((8448, 8448, 8704), geometry["floor_xyz"])
            self.assertEqual((3072, 3072), geometry["floor_width_depth"])
            self.assertEqual((8960, 8960, 9216), geometry["spawn_xyz"])
            self.assertEqual((9728, 9120, 8704), geometry["key_xyz"])
            self.assertEqual((10864, 9888, 8704), geometry["chest_xyz"])
            self.assertEqual(0xDC, geometry["chest_yaw_u8"])
            self.assertEqual(((-410, 1229, 512), (0, 1229, 512), (-615, 205, 512),
                              (-410, 615, 512), (204, 819, 512)), geometry["reward_relative_xyz"])
            self.assertTrue(geometry["annotations_not_sampled_or_authenticated"])
            omitted = {"utc", "expected_profile_annotation", "expected_payload_annotations", "expected_geometry_annotations"}
            self.assertEqual({k: v for k, v in default.items() if k not in omitted},
                             {k: v for k, v in wide.items() if k not in omitted})
            self.assertTrue(all(wide[name] is False for name in AUTHORITY_FLAGS))
            for mapping in (PROFILES, PROFILES["r02"], PROFILES["r02"]["payload"], PROFILES["r02"]["geometry"]):
                with self.assertRaises(TypeError): mapping["caller"] = "not authority"
            wide["expected_payload_annotations"]["body_sha256"] = "caller change"
            geometry["floor_width_depth"] = (1, 1)
            self.assertEqual({}, PROFILES["r02"]["payload"])
            self.assertEqual((3072, 3072), PROFILES["r02"]["geometry"]["floor_width_depth"])
            for profile in (None, True, [], {}, "", "R01", "r04"):
                memory = fixture()
                with self.assertRaises(ValueError): capture(memory, "invalid", profile)
                self.assertEqual([], memory.reads)

        def test_r03_authored_destination_annotation_keeps_identical_read_authority(self):
            baseline = fixture(); old = capture(baseline, "same", "r02")
            memory = fixture(); authored = capture(memory, "same", "r03")
            self.assertEqual(baseline.reads, memory.reads)
            self.assertEqual("private-fresh-chest-p9-runtime-experiment-v3", authored["expected_profile_annotation"])
            self.assertEqual({}, authored["expected_payload_annotations"])
            geometry = authored["expected_geometry_annotations"]
            self.assertEqual((("reward.2", 10454, 10503, 9216),), geometry["authored_world_destination_overrides"])
            self.assertEqual((10454, 10503, 9216), geometry["reward_destination_xyz"][1])
            self.assertEqual(geometry["reward_destination_xyz"][1], geometry["reward_destination_xyz"][3])
            for name in ("floor_xyz", "floor_width_depth", "spawn_xyz", "key_xyz", "chest_xyz", "chest_yaw_u8"):
                self.assertEqual(old["expected_geometry_annotations"][name], geometry[name])
            omitted = {"utc", "expected_profile_annotation", "expected_payload_annotations", "expected_geometry_annotations"}
            self.assertEqual({k: v for k, v in old.items() if k not in omitted},
                             {k: v for k, v in authored.items() if k not in omitted})
            self.assertTrue(all(authored[name] is False for name in AUTHORITY_FLAGS))
            for mapping in (PROFILES["r03"], PROFILES["r03"]["payload"], PROFILES["r03"]["geometry"]):
                with self.assertRaises(TypeError): mapping["caller"] = "not authority"
            geometry["authored_world_destination_overrides"] = ()
            self.assertEqual((("reward.2", 10454, 10503, 9216),),
                             PROFILES["r03"]["geometry"]["authored_world_destination_overrides"])

        def test_each_gate_refuses_before_payload_and_reread_discards_payload(self):
            scalar_reads = {(at, struct.calcsize(fmt)) for at, fmt in FIELDS.values()}
            for profile in PROFILES:
                for name, (address, _, expected) in GATES.items():
                    with self.subTest(profile=profile, gate=name):
                        memory = fixture(); memory.map(address, struct.pack("<I", expected ^ 1))
                        self.assertFalse(capture(memory, "wrong", profile)["bounded_sample_valid"])
                        self.assertTrue(all(read in scalar_reads for read in memory.reads))
                        memory = fixture(); memory.flip = (address, expected ^ 1)
                        sample = capture(memory, "transition", profile)
                        self.assertFalse(sample["bounded_sample_valid"])
                        self.assertEqual([name], sample["changed_gate_fields"])
                        self.assertNotIn("placed_rows", sample); self.assertNotIn("particles", sample)

        def test_cursor_bounds_and_read_only_untrusted_globals(self):
            for name, values in (("dynamic_count", (33, 0xFFFFFFFF)),
                                 ("dynamic_properties_cursor", (ARENA_END + 4, ARENA_END - 1, ARENA_END - 32 * 24 - 24))):
                for value in values:
                    memory = fixture(); memory.map(FIELDS[name][0], struct.pack("<I", value))
                    self.assertFalse(capture(memory, "bad-cursor")["bounded_sample_valid"])
                    self.assertFalse(any(length == STRIDE for _, length in memory.reads))
            memory = fixture()
            for name in ("key_state_raw_u32", "tracked_chest_gem_raw_u32", "next_free_moby_not_active_end"):
                memory.map(FIELDS[name][0], struct.pack("<I", 0xDEADBEEF))
            self.assertTrue(capture(memory, "opaque")["bounded_sample_valid"])
            self.assertFalse(any(at == 0xDEADBEEF for at, _ in memory.reads))

        def test_treasure_counters_are_distinct_observations_not_lifecycle_certificates(self):
            memory = fixture()
            for name, value in (("key_state_raw_u32", 3), ("actual_global_gems", 123),
                                ("actual_slot35_gems", 7), ("animated_hud_gems", 6),
                                ("slot35_target_s16", 20)):
                address, fmt = FIELDS[name]
                memory.map(address, struct.pack(fmt, value))
            sample = capture(memory, "partial-collection")
            self.assertTrue(sample["bounded_sample_valid"])
            self.assertEqual((3, 123, 7, 6, 20), tuple(sample[name] for name in
                ("key_state_raw_u32", "actual_global_gems", "actual_slot35_gems",
                 "animated_hud_gems", "slot35_target_s16")))
            self.assertIn((0x8006F47E, 2), memory.reads)
            self.assertTrue(all(sample[name] is False for name in AUTHORITY_FLAGS))

        def test_r02_retired_controllers_key3_and_nine_points_do_not_hide_escaped_gem(self):
            memory = fixture()
            memory.map(FIELDS["actual_global_gems"][0], struct.pack("<I", 58))
            before = capture(memory, "before-chest", "r02")
            self.assertTrue(before["bounded_sample_valid"])
            for index in range(2, 9):
                memory.map(ROWS + index * STRIDE, row(index, 0xFD))
            for name, value in (("key_state_raw_u32", 3), ("actual_global_gems", 67),
                                ("actual_slot35_gems", 9), ("animated_hud_gems", 8),
                                ("slot35_target_s16", 20)):
                address, fmt = FIELDS[name]
                memory.map(address, struct.pack(fmt, value))
            # Actual R02 sample19 coordinates, but a synthetic offline memory
            # fixture: five deleted D controllers do not count emitted gems.
            for slot in range(15):
                memory.map(DYNAMIC + slot * STRIDE + 0x48, b"\xfd")
            gem = bytearray(STRIDE)
            struct.pack_into("<H", gem, 0x36, 0x83)
            struct.pack_into("<iii", gem, 12, 10059, 12156, 6203)
            gem[0x48:0x4A] = b"\x00\x02"
            memory.map(DYNAMIC + 15 * STRIDE, gem)
            memory.map(DYNAMIC + 16 * STRIDE + 0x48, b"\xff")
            for name, value in (("dynamic_count", 16), ("dynamic_properties_cursor", ARENA_END - 16 * 24),
                                ("next_free_moby_not_active_end", DYNAMIC + 16 * STRIDE)):
                memory.map(FIELDS[name][0], struct.pack("<I", value))
            sample = capture(memory, "escaped-final-red", "r02")
            self.assertTrue(sample["bounded_sample_valid"])
            self.assertTrue(sample["gate_reread_consistent"])
            self.assertTrue(all(not r["active_status_sample"] and r["class_id"] == 0xD
                                for r in sample["placed_rows"][4:9]))
            self.assertEqual(3, sample["key_state_raw_u32"])
            self.assertEqual(9, sample["actual_slot35_gems"] - before["actual_slot35_gems"])
            self.assertEqual(9, sample["actual_global_gems"] - before["actual_global_gems"])
            self.assertEqual(8, sample["animated_hud_gems"])
            self.assertEqual(1, len(sample["dynamic_rows"]["active"]))
            escaped = sample["dynamic_rows"]["active"][0]
            self.assertEqual((24, 0x83, [10059, 12156, 6203]),
                             (escaped["index"], escaped["class_id"], escaped["xyz"]))
            self.assertGreater(escaped["xyz"][1], 11520)
            self.assertLess(escaped["xyz"][2], 8704)
            self.assertTrue(all(sample[name] is False for name in AUTHORITY_FLAGS))

        def test_motion_fields_keep_signed_width_and_never_chase_companion(self):
            memory = fixture()
            memory.map(0x80078BBC, struct.pack("<i", -1))
            memory.map(0x80078B4C, struct.pack("<iii", -352, 3264, 0))
            memory.map(0x80078B64, struct.pack("<iii", 7008, 192, 4640))
            memory.map(0x80075898, struct.pack("<I", 0xDEADBEEF))
            sample = capture(memory, "signed-motion")
            self.assertTrue(sample["bounded_sample_valid"])
            self.assertEqual(-1, sample["player_health_s32"])
            self.assertEqual([-352, 3264, 0], sample["player_motion_78b4c_s32x3"])
            self.assertEqual([7008, 192, 4640], sample["player_motion_78b64_s32x3"])
            self.assertFalse(any(at == 0xDEADBEEF for at, _ in memory.reads))
            self.assertTrue(all(sample[name] is False for name in AUTHORITY_FLAGS))

        def test_destroyed_or_transformed_classes_do_not_become_false_layout_failures(self):
            memory = fixture(); dead = row(2, 0xFD)
            struct.pack_into("<HI", dead, 0x36, 0xFFFF, 0)
            memory.map(ROWS + 2 * STRIDE, dead)
            transformed = row(4); struct.pack_into("<H", transformed, 0x36, 0xAE)
            memory.map(ROWS + 4 * STRIDE, transformed)
            sample = capture(memory, "lifecycle")
            self.assertTrue(sample["bounded_sample_valid"])
            self.assertNotIn((SCENE + PRIVATE_OFFSETS[2], 4), memory.reads)
            self.assertIn("private_owner_sample", sample["placed_rows"][4])

        def test_private_pointer_alias_raw_relative_and_unmapped_are_never_chased(self):
            for pointer in (PRIVATE_OFFSETS[3], SCENE + PRIVATE_OFFSETS[4],
                            SCENE + PRIVATE_OFFSETS[3] + 4, 0, 0xDEADBEEF):
                memory = fixture(); memory.map(ROWS + 3 * STRIDE, struct.pack("<I", pointer))
                sample = capture(memory, "private-refusal")
                self.assertTrue(sample["bounded_sample_valid"])
                self.assertIn("private_scan_skipped", sample["placed_rows"][3])
                self.assertNotIn((SCENE + PRIVATE_OFFSETS[3], PRIVATE_LENGTHS[3]), memory.reads)

        def test_both_scans_stop_at_terminal_and_never_read_one_extra(self):
            expected_reads = None
            for profile in PROFILES:
                memory = fixture(); memory.map(DYNAMIC + 0x48, b"\x00")
                memory.map(POOL + 1, b"\x00")
                sample = capture(memory, "full", profile)
                self.assertTrue(sample["dynamic_rows"]["no_in_range_terminal"])
                self.assertTrue(sample["particles"]["all256_slots_visited"])
                self.assertEqual(41, sum(length == STRIDE for _, length in memory.reads))
                self.assertNotIn((DYNAMIC + 32 * STRIDE, STRIDE), memory.reads)
                self.assertEqual([(PETE, 4)], [read for read in memory.reads if read[0] == PETE])
                if expected_reads is not None: self.assertEqual(expected_reads, memory.reads)
                expected_reads = memory.reads
            def endless():
                for _ in range(32): yield bytes(STRIDE)
                raise AssertionError("Scanner demanded row33")
            self.assertTrue(scan_dynamic(endless())["no_in_range_terminal"])
            with self.assertRaises(ValueError): scan_dynamic([])
            with self.assertRaises(ValueError): scan_dynamic([bytes(87)])
            with self.assertRaises(ValueError): scan_particles(bytes(0x2001))

        def test_protocol_and_cli_cannot_send_execution_or_connect_without_opt_in(self):
            class Wire:
                def __init__(self):
                    self.reply = bytearray(b"+" + wizard.rsp.frame_payload("01020304")); self.sent = []
                def recv(self, size):
                    data = bytes(self.reply[:size]); del self.reply[:size]; return data
                def sendall(self, data): self.sent.append(data)
            wire = Wire()
            self.assertEqual(b"\x01\x02\x03\x04", ReadOnlyClient(wire).read(0x80075830, 4))
            self.assertEqual([wizard.rsp.frame_payload("m80075830,4"), b"+"], wire.sent)
            for address, length in ((True, 4), (0x7FFFFFFF, 4), (0x801FFFFF, 4), (ROWS, 1025)):
                wire = Wire()
                with self.assertRaises(ValueError): ReadOnlyClient(wire).read(address, length)
                self.assertEqual([], wire.sent)
            with patch.object(socket, "create_connection", side_effect=AssertionError("No connection allowed")):
                for args in ([], ["--profile", "anything"],
                             ["--connect-identified-test-session", "--profile", "anything"],
                             ["--connect-identified-test-session", "--profile", "R01"],
                             ["--self-test", "--profile", "unsupported-profile"],
                             ["--self-test", "--connect-identified-test-session"]):
                    with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit): main(args)

        def test_cli_profile_selection_reaches_same_mocked_read_only_client(self):
            for profile in (None, *PROFILES):
                memory, output = fixture(), io.StringIO()
                args = ["--connect-identified-test-session"]
                if profile is not None: args += ["--profile", profile]
                with patch.object(socket, "create_connection") as connection, \
                        patch.object(sys, "stdin", io.StringIO("p cli\nq\n")), \
                        patch.dict(globals(), {"ReadOnlyClient": lambda _: memory}), contextlib.redirect_stdout(output):
                    main(args)
                connection.assert_called_once_with(("127.0.0.1", 2345), timeout=2)
                samples = [json.loads(line) for line in output.getvalue().splitlines() if line.startswith("{")]
                self.assertEqual(1, len(samples))
                self.assertEqual(PROFILES[profile or "r01"]["profile"], samples[0]["expected_profile_annotation"])
                self.assertTrue(samples[0]["bounded_sample_valid"])
                self.assertTrue(all(samples[0][name] is False for name in AUTHORITY_FLAGS))

    with patch.object(socket, "create_connection", side_effect=AssertionError("Offline test attempted socket connection")):
        result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(Tests))
    if not result.wasSuccessful():
        raise SystemExit(1)
    print(f"FRESH_CHEST_OBSERVER_OFFLINE_OK tests={result.testsRun} connections=0 romReads=0 runtime=false")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--self-test", action="store_true", help="Offline bounded fixtures only; never connect.")
    mode.add_argument("--connect-identified-test-session", action="store_true",
                      help="Confirm external identification of the exact private test process/open BIN; localhost2345 only.")
    parser.add_argument("--profile", choices=tuple(PROFILES), default="r01",
                        help="Closed annotation profile only (default r01); gates, addresses and read bounds are unchanged.")
    args = parser.parse_args(argv)
    if args.self_test:
        self_test()
        return
    print("READ_ONLY_CHEST: initial connection may pause emulation; resume/pause only via UI.", flush=True)
    with socket.create_connection(("127.0.0.1", 2345), timeout=2) as connection:
        client = ReadOnlyClient(connection)
        print("READ_ONLY_CHEST_CONNECTED; p <label> samples, q closes. No automatic sampling or gameplay commands.", flush=True)
        previous_lives = None
        for line in sys.stdin:
            command = line.strip()
            if command == "q":
                break
            if command == "p" or command.startswith("p "):
                sample = capture(client, command[1:].strip(), args.profile)
                if sample["bounded_sample_valid"]:
                    if previous_lives is not None and sample["lives"] < previous_lives:
                        sample.setdefault("attention", []).append("Lives decreased since the prior bounded sample; inspect in UI.")
                    previous_lives = sample["lives"]
                print(json.dumps(sample), flush=True)


if __name__ == "__main__":
    main()
