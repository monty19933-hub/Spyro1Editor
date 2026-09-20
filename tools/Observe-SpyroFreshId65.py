#!/usr/bin/env python3
"""Manual m-only observations for separately selected exact fresh-ID65 profiles.

Identify the actual process/open BIN and verify its published companions outside
this tool before opting in. The SHA annotations and matching RAM layout do NOT
authenticate the loaded disc. Initial GDB connection may pause DuckStation;
resume/pause only through its UI. Samples are sequential, never atomic or a
runtime/peak-usage certificate. Scope is idle and short center/seam walks, not
foreign entry, rescue, checkpoint reload, power grants or all-state acceptance.
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

_spec = importlib.util.spec_from_file_location(
    "fresh_id65_read_only_primitives", Path(__file__).with_name("Observe-SpyroNativeWizard.py"))
wizard = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = wizard
_spec.loader.exec_module(wizard)
ReadOnlyClient = wizard.ReadOnlyClient
decode_moby = wizard.decode_moby
primitive_slack = wizard.primitive_slack
# Deliberately do not reuse the Wizard's level12 capture/arena/90-row scanners.

PROFILE = "fresh-id65-untextured-a-n16-idle-center-walk-research-v1"
EXPECTED_CANDIDATE_SHA256 = "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188"
EXPECTED_BODY_SHA256 = "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c"
EXPECTED_SCENE_SHA256 = "4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433"
EXPECTED_EXE_SHA256 = "3473dc92e087c87858434125d5fb1ec879450682e3be64c508de6805029ce7a2"
BASE, ARENA_END, POOL, PETE = 0x8009A068, 0x8009A7C0, 0x801C3BB8, 0x801C5BB8
CAPACITY = 16
# Closed XY extent of the two authored floor triangles in the exact A body.
# The source fixture decodes their HP vertices/faces from that pinned body.
# This is an observation-scope boundary, NOT a player footprint, safe inset,
# Z/ground-state certificate or authentication of the loaded candidate.
AUTHORED_FLOOR_XY_BOUNDS = (8448, 8448, 9472, 9472)
PROFILE_CHOICES = ("starter-a-n16", "edited-landing-track02-r01", "tiled-three-track19-r01",
                   "tiled-two-material-three-track19-r01")

# Both legacy exact untextured N16 bodies retain this address layout. Equal RAM gates
# cannot distinguish their geometry/document/music or authenticate either disc.
# 785E0 uses the native used-data length8C8 rather than the CD length1000.
# The exact-source fixture executes the complete native generic model helper,
# six animations/90 paired frames, then the real store76558=800973C4.
GATES = {
    "level": (0x8007596C, "<I", 65),
    "game_state": (0x800757D8, "<I", 0),  # Global transition, NOT player78AD0.
    "normal_classifier": (0x80075690, "<I", 0),
    "flight_permission": (0x80078CA4, "<I", 0),
    "world_index": (0x8007595C, "<I", 5),
    "continuous_level_index": (0x80075964, "<I", 35),
    "superflame_active": (0x80078764, "<I", 0),
    "superflame_duration": (0x80078768, "<I", 0),
    "copy_anchor": (0x800785D8, "<I", 0x80089ECC),
    "copy_cursor": (0x800785DC, "<I", 0x80089ECC),
    "model_anchor": (0x800785E0, "<I", 0x8008A794),
    "scene_anchor": (0x800785E4, "<I", 0x80099F94),
    "scene_disk_offset": (0x80078600, "<I", 0xEF000),
    "scene_disk_length": (0x80078604, "<I", 0x3000),
    "moby_base": (0x80075828, "<I", BASE),
    "dynamic_base": (0x80075890, "<I", BASE),
    "arena_end": (0x800756E8, "<I", ARENA_END),
    "bucket_base": (0x80075778, "<I", 0x8009A7D0),
    "dynamic_capacity": (0x800756A8, "<I", CAPACITY),
    "companion_model_root": (0x80076558, "<I", 0x800973C4),
    "particle_allocation_base": (0x800785F8, "<I", POOL),
    "particle_base": (0x80075824, "<I", POOL),
    "callback_spawn": (0x800758CC, "<I", 0x800872A4),
    "callback_tick": (0x80075734, "<I", 0x8007DA54),
    "callback_effect_spawn": (0x800758E4, "<I", 0x80088F68),
    "callback_effect_tick": (0x800756BC, "<I", 0x80087E20),
    "callback_4": (0x800757A0, "<I", 0x8007AF2C),
    "callback_5": (0x800758A8, "<I", 0x8007B068),
    "callback_6": (0x8007574C, "<I", 0x8007B0AC),
    "callback_7": (0x800758D8, "<I", 0x8007D04C),
}
# Separate code-owned layout; never rewrite legacy globals to select a profile.
# source_variable_native_loader_oracle.py execute_owned_chain derives these
# through the actual parser, class78 relocator and native N16 scene list walk.
# Evidence286 reports A58/model8008A924/scene8009A124/arenaEnd8009A950.
# NativeId65FreshSceneLayout owns rows at D4, arena70*N+58, then 4/8/4 prefixes.
_TILED_USED = 0xA58
_TILED_MODEL = 0x80089ECC + _TILED_USED
_TILED_SCENE = _TILED_MODEL + 0xF800
_TILED_BASE = _TILED_SCENE + 0xD4
_TILED_ARENA_END = _TILED_BASE + 0x70 * CAPACITY + 0x58
_TILED_MOVED = MappingProxyType({
    "model_anchor": _TILED_MODEL, "scene_anchor": _TILED_SCENE,
    "moby_base": _TILED_BASE, "dynamic_base": _TILED_BASE,
    "arena_end": _TILED_ARENA_END, "bucket_base": _TILED_ARENA_END + 4 + 8 + 4,
    "companion_model_root": _TILED_MODEL + 0xCC30,
})
_TILED_GATES = MappingProxyType({
    name: (at, encoding, _TILED_MOVED.get(name, expected))
    for name, (at, encoding, expected) in GATES.items()
})
# Separate exact six-textured-face profile: native-loader346 derives the final
# BC8 used end, model8008AA94 and scene8009A294, not intermediate geometry A58.
# Rows/arena follow the same native D4 + 70*N + 58 layout; source369 confirms
# the complete scene/EXE stay byte-equal to A. None of these gates proves texture
# appearance, static source identity, GPU behavior or the loaded disc identity.
_TILED_MATERIAL_USED = 0xBC8
_TILED_MATERIAL_MODEL = 0x80089ECC + _TILED_MATERIAL_USED
_TILED_MATERIAL_SCENE = _TILED_MATERIAL_MODEL + 0xF800
_TILED_MATERIAL_BASE = _TILED_MATERIAL_SCENE + 0xD4
_TILED_MATERIAL_ARENA_END = _TILED_MATERIAL_BASE + 0x70 * CAPACITY + 0x58
_TILED_MATERIAL_MOVED = MappingProxyType({
    "model_anchor": _TILED_MATERIAL_MODEL, "scene_anchor": _TILED_MATERIAL_SCENE,
    "moby_base": _TILED_MATERIAL_BASE, "dynamic_base": _TILED_MATERIAL_BASE,
    "arena_end": _TILED_MATERIAL_ARENA_END, "bucket_base": _TILED_MATERIAL_ARENA_END + 4 + 8 + 4,
    "companion_model_root": _TILED_MATERIAL_MODEL + 0xCC30,
})
_TILED_MATERIAL_GATES = MappingProxyType({
    name: (at, encoding, _TILED_MATERIAL_MOVED.get(name, expected))
    for name, (at, encoding, expected) in GATES.items()
})
CURSORS = {
    "dynamic_count": (0x800756A4, "<I"),
    "dynamic_properties_cursor": (0x80075930, "<I"),
    "next_free_moby_not_active_end": (0x8007573C, "<I"),
    "companion_handle": (0x80075898, "<I"),
}
FIELDS = {name: (at, encoding) for name, (at, encoding, _) in GATES.items()} | CURSORS | {
    "lives": (0x8007582C, "<I"), "player_xyz": (0x80078A58, "<iii"),
    "player_heading_u8": (0x80078A66, "<B"),
    "camera_control_angle_s16": (0x80076E20, "<h"),
    "player_desired_bearing_u32": (0x80078B24, "<I"),
    "player_state": (0x80078AD0, "<I"), "player_substate": (0x80078AD4, "<I"),
    "player_state_timer": (0x80078AD8, "<I"),
    "player_animation_id": (0x80078A71, "<B"),
    "next_free_particle_not_active_end": (0x80075738, "<I"),
    "primitive_cursor": (0x800757B0, "<I"), "primitive_limit": (0x80075780, "<I"),
    "ordering_table": (0x80075820, "<I"), "renderer_exhaustion_flag": (0x800758B0, "<I"),
    "last_collision_saved_sp_not_peak": (0x80077DFC, "<I"),
}
REREAD_GATES = tuple(GATES) + tuple(CURSORS)


def _profile_layout(profile):
    if type(profile) is not str or profile not in PROFILE_CHOICES:
        raise ValueError("Only a fixed code-owned observer profile may be selected.")
    if profile == "tiled-three-track19-r01":
        return _TILED_GATES, _TILED_BASE, _TILED_ARENA_END, (8448, 8448, 14592, 10496)
    if profile == "tiled-two-material-three-track19-r01":
        return _TILED_MATERIAL_GATES, _TILED_MATERIAL_BASE, _TILED_MATERIAL_ARENA_END, (8448, 8448, 14592, 10496)
    bounds = (8192, 8192, 10240, 10240) if profile == "edited-landing-track02-r01" else AUTHORED_FLOOR_XY_BOUNDS
    return GATES, BASE, ARENA_END, bounds


def read_fields(client, names):
    result = {}
    for name in names:
        address, encoding = FIELDS[name]
        values = struct.unpack(encoding, client.read(address, struct.calcsize(encoding)))
        result[name] = values[0] if len(values) == 1 else list(values)
    return result


def require_baseline(values, *, profile="starter-a-n16"):
    gates, row_base, arena_end, _ = _profile_layout(profile)
    baseline = "three-tile/N16" if profile == "tiled-three-track19-r01" else "A/N16"
    if profile == "tiled-two-material-three-track19-r01":
        baseline = "six-textured-three-tile/N16"
    for name, (_, _, expected) in gates.items():
        if values[name] != expected:
            raise ValueError("Not the initialized fixed " + baseline + " baseline: " + name)
    if not 1 <= values["dynamic_count"] <= CAPACITY:
        raise ValueError("Companion baseline requires a native count in1..16.")
    properties = values["dynamic_properties_cursor"]
    if not arena_end - CAPACITY * 24 <= properties <= arena_end or (arena_end - properties) % 24:
        raise ValueError("Properties cursor escaped the exact16-slot grid.")
    cursor = values["next_free_moby_not_active_end"]
    if not row_base <= cursor <= row_base + CAPACITY * 88 or (cursor - row_base) % 88:
        raise ValueError("Next-free row escaped the exact16-slot grid.")
    handle = values["companion_handle"]
    if not row_base <= handle <= row_base + (CAPACITY - 1) * 88 or (handle - row_base) % 88:
        raise ValueError("Companion handle does not own a complete in-range row.")
    # Count, cursor distance and properties high-water are NOT equalities.


def scan_dynamic(rows, start):
    active, terminal, consumed = [], None, 0
    iterator = iter(rows)
    for slot in range(CAPACITY):
        try: row = next(iterator)
        except StopIteration: break
        if len(row) != 88: raise ValueError("Partial native dynamic row.")
        if row[0x48] == 0xFF:
            terminal = slot
            break
        consumed += 1
        if row[0x48] < 0x80:
            active.append(decode_moby(row, slot, start + slot * 88))
    return {"active": active, "terminal_slot": terminal,
            "high_water_rows_before_terminal": consumed, "no_in_range_terminal": terminal is None}


def scan_particles(pool):
    if len(pool) != 0x2000: raise ValueError("Particle owner is exactly256 records of32 bytes.")
    counts, samples, active, terminal = Counter(), [], 0, None
    for slot in range(256):
        row = pool[slot * 32:(slot + 1) * 32]
        if row[1] == 0xFF:
            terminal = slot
            break
        if row[1] >= 0x80: continue
        active += 1
        counts[f"{row[0]:02X}/kind{row[1]}"] += 1
        if row[0] in (0x0C, 0x42) and len(samples) < 16:
            samples.append({"slot": slot, "selector": row[0], "kind": row[1],
                            "age": row[2], "visibility": row[3], "raw_hex": bytes(row).hex()})
    return {"active_count": active, "terminal_slot": terminal, "selector_kind_counts": dict(counts),
            "companion_effect_samples": samples, "all256_slots_visited": terminal is None}


def capture(client, label, *, profile="starter-a-n16"):
    _, row_base, _, bounds = _profile_layout(profile)
    edited = profile == "edited-landing-track02-r01"
    tiled = profile == "tiled-three-track19-r01"
    tiled_material = profile == "tiled-two-material-three-track19-r01"
    result = {
        "utc": datetime.now(timezone.utc).isoformat(), "label": label[:120],
        "expected_profile_annotation": PROFILE,
        "expected_candidate_sha256_annotation": EXPECTED_CANDIDATE_SHA256,
        "expected_body_sha256_annotation": EXPECTED_BODY_SHA256,
        "expected_scene_sha256_annotation": EXPECTED_SCENE_SHA256,
        "expected_exe_sha256_annotation": EXPECTED_EXE_SHA256,
        "candidate_authenticated": False, "sequential_reads_not_atomic": True,
        "runtime_pass": False, "payload_valid": False, "gate_reread_consistent": False,
    }
    if edited:
        # These exact reviewed publication/document identities are ANNOTATIONS.
        # They neither authenticate a process/open BIN nor approve an editor UI
        # result. Initial spawn/heading/Z/music are not moving-player RAM gates.
        result.update(
            expected_profile_annotation="fresh-id65-edited-landing-track02-r01-idle-center-walk-research-v1",
            expected_candidate_sha256_annotation="ecc8f61cc16bf86a2f5117b327c49aede18f5c1e6a602c0e67f2ba58565aed16",
            expected_body_sha256_annotation="bae43882a39dfc1604f4e6d578a354f34f99107f97faa9330600b3e01f34b15b",
            expected_scene_sha256_annotation="13e3674859b79ff45fa950cb1c2f788a368f89d70bb24599dfbccf061a75c0de",
            expected_exe_sha256_annotation="2c1fd87e9fa8ce5c5f16be53e738405cec90c0bfc10ac9cb8cf1ce979b2e5c57",
            expected_cue_sha256_annotation="1fcb9bade2f3c9c59b6e6d7b57bd8a2bd0773534d151374d62fcb2ce442e663c",
            expected_receipt_sha256_annotation="3b545c4dd0e2102586e9f520dec96fda3ea8e84a088003d5b7a7a5ef701373d7",
            expected_canonical_document_sha256_annotation="b3f10d40db88cbfa3d3393e0d9c9e5e49e9a829f3fd311bcfa005082016e4cf1",
            expected_plan_sha256_annotation="fba3e61a3f93b9dff27d9a5304eb746b7c2ef9aa95a71cfeda6adaee62b571c2",
            expected_spawn_xyz_annotation=[9216, 9216, 9216], expected_spawn_heading_u8_annotation=96,
            expected_floor_z_annotation=8704, expected_music_track_id_annotation=2,
            ui_result_approved=False, normal_export_approved=False)
    elif tiled:
        # Closed independent three-tile fixture, not arbitrary schema4 support.
        # Its unchanged scene/EXE are byte-equal to A (TiledCompilationCases).
        # These hashes, bounds and layout gates NEVER identify the loaded disc.
        result.update(
            expected_profile_annotation="fresh-id65-tiled-three-track19-r01-idle-seam-walk-research-v1",
            expected_candidate_sha256_annotation="7ba0b29b68a1bee415901382c3a3b5345ccb01ca84872502131f68361afdc305",
            expected_body_sha256_annotation="37157a7248c90665309d8cee21c739a846aca284426cd602c65d627788c06009",
            expected_used_data_length_annotation=_TILED_USED,
            expected_spawn_xyz_annotation=[8960, 8960, 9216], expected_spawn_heading_u8_annotation=64,
            expected_floor_z_annotation=8704, expected_music_track_id_annotation=19,
            ui_result_approved=False, normal_export_approved=False)
    elif tiled_material:
        # Only the independently frozen352 six-textured Body/image is described.
        # Same RAM anchors cannot distinguish Base/FF edits or different material
        # bindings, and these expected hashes are not process/disc authentication.
        result.update(
            expected_profile_annotation="fresh-id65-tiled-two-material-three-track19-r01-idle-seam-walk-research-v1",
            expected_candidate_sha256_annotation="462c992f80f512f0b6be781b1a79e707e37a0e18f5e5e47356cd332ef61b2ba8",
            expected_body_sha256_annotation="461326c0e362a7c0bdb4c15c21996d15f6b1674e2156d60802f0efc94e6c36be",
            expected_used_data_length_annotation=_TILED_MATERIAL_USED,
            expected_spawn_xyz_annotation=[8960, 8960, 9216], expected_spawn_heading_u8_annotation=64,
            expected_floor_z_annotation=8704, expected_music_track_id_annotation=19,
            ui_result_approved=False, normal_export_approved=False)
    try:
        result.update(read_fields(client, FIELDS))
        require_baseline(result, profile=profile)
        handle = result["companion_handle"]
        companion = decode_moby(client.read(handle, 88), (handle - row_base) // 88, handle)
        if companion["class_id"] != 0x78 or companion["state_bytes"][0] >= 0x80:
            raise ValueError("Companion is absent, inactive or a stale raw reload row.")
        # Its properties are opaque data. Never chase them, nor any sampled
        # model/callback/renderer pointer. No17th Moby or257th particle read.
        dynamic = scan_dynamic((client.read(row_base + i * 88, 88) for i in range(CAPACITY)), row_base)
        pool = b"".join(client.read(POOL + offset, 1024) for offset in range(0, 0x2000, 1024))
        particles = scan_particles(pool)
        terminal_word = client.read(PETE, 4).hex()  # Separate PETE first-word owner.
        after = read_fields(client, REREAD_GATES)
        changed = [name for name in REREAD_GATES if after[name] != result[name]]
        if changed:
            result["changed_gate_fields"] = changed
            raise ValueError("Baseline gates changed across sequential payload reads.")
        # Publish interpretations only after the small gate reread agrees.
        # Equal gates do not rule out intermediate changes or establish atomicity.
        result.update(payload_valid=True, gate_reread_consistent=True, companion=companion,
                      dynamic_rows=dynamic, particles=particles,
                      particle_terminal_word_not_record257=terminal_word)
        slack, reason = primitive_slack(result["primitive_cursor"], result["primitive_limit"])
        result["primitive_sampled_slack"] = slack
        result["primitive_sampled_slack_unavailable_reason"] = reason
        attention = []
        min_x, min_y, max_x, max_y = bounds
        x, y, _ = result["player_xyz"]
        inside = min_x <= x <= max_x and min_y <= y <= max_y
        result["authored_floor_xy_bounds_annotation"] = list(bounds)
        result["sampled_player_xy_inside_authored_floor"] = inside
        if not inside:
            attention.append(
                "Pause in UI: sampled player XY is outside the authored floor "
                f"[{min_x},{max_x}] x [{min_y},{max_y}]; outside this idle/center-walk observation "
                "scope, not a collision-bug diagnosis.")
        if result["dynamic_count"] == CAPACITY or dynamic["no_in_range_terminal"]:
            attention.append("Pause in UI: dynamic capacity or in-range terminal requires inspection; not proof of N+1 allocation.")
        if particles["all256_slots_visited"]:
            attention.append("Pause in UI: full256 particle pool; no257th record was read.")
        if terminal_word != "ffffffff":
            attention.append("Pause in UI: separate PETE first-word terminal differs; complete PETE integrity is not inferred.")
        if result["renderer_exhaustion_flag"] or slack is None or slack < 0:
            attention.append("Pause in UI: renderer flag or sampled primitive slack requires inspection; not a peak measurement.")
        if attention: result["attention"] = attention
    except (ValueError, struct.error) as error:
        result["actor_scan_refused"] = str(error)
        result["attention"] = ["Pause in UI; this sample is not a native-layout or gameplay pass."]
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=PROFILE_CHOICES, default="starter-a-n16",
                        help="Fixed expected source profile only; matching RAM does not authenticate its disc.")
    parser.add_argument("--connect-identified-test-session", action="store_true",
                        help="Confirm the selected exact process/open BIN was independently identified; only localhost2345 is used.")
    args = parser.parse_args()
    if not args.connect_identified_test_session:
        parser.error("Independently identify the process/open fixed candidate, then explicitly opt in.")
    with socket.create_connection(("127.0.0.1", 2345), timeout=2) as connection:
        client = ReadOnlyClient(connection)
        print("READ_ONLY_FRESH_ID65_CONNECTED; p <label> samples, q closes. Resume/pause only via UI.", flush=True)
        previous_lives = None
        for line in sys.stdin:
            command = line.strip()
            if command == "q": break
            if command == "p" or command.startswith("p "):
                sample = capture(client, command[1:].strip(), profile=args.profile)
                if sample["payload_valid"]:
                    if previous_lives is not None and sample["lives"] < previous_lives:
                        sample.setdefault("attention", []).append("Pause in UI: lives decreased since the prior valid sample.")
                    previous_lives = sample["lives"]
                print(json.dumps(sample), flush=True)


if __name__ == "__main__":
    main()
