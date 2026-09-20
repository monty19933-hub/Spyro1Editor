"""Exact six-textured/three-tile observer tests: synthetic RAM only.

No source image, emulator, real socket or runtime acceptance. Layout literals
come from native-loader346; Body/image references were independently frozen352.
Matching RAM and expected hashes remain annotations, never authentication.
"""
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import socket
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location(
    "tiled_material_observer_starter_control", Path(__file__).with_name("test_fresh_id65_observer.py"))
base = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(base)
observer = base.OBSERVER

PROFILE = "tiled-two-material-three-track19-r01"
BASE, END, BUCKETS, COMPANION = 0x8009A368, 0x8009AAC0, 0x8009AAD0, 0x800976C4
MOVED = {
    "model_anchor": 0x8008AA94, "scene_anchor": 0x8009A294,
    "moby_base": BASE, "dynamic_base": BASE, "arena_end": END,
    "bucket_base": BUCKETS, "companion_model_root": COMPANION,
}
# Unchanged scalar addresses/values come from the independent original test
# fixture, never from the observer's profile map. Only these seven gates move.
GATES = {name: (at, fmt, MOVED.get(name, value)) for name, (at, fmt, value) in base.GATES.items()}
CURSORS = {
    "dynamic_count": (0x800756A4, "<I", 1),
    "dynamic_properties_cursor": (0x80075930, "<I", END - 24),
    "next_free_moby_not_active_end": (0x8007573C, "<I", BASE + 88),
    "companion_handle": (0x80075898, "<I", BASE),
}
EXPECTED = {
    "expected_profile_annotation": "fresh-id65-tiled-two-material-three-track19-r01-idle-seam-walk-research-v1",
    "expected_candidate_sha256_annotation": "462c992f80f512f0b6be781b1a79e707e37a0e18f5e5e47356cd332ef61b2ba8",
    "expected_body_sha256_annotation": "461326c0e362a7c0bdb4c15c21996d15f6b1674e2156d60802f0efc94e6c36be",
    "expected_scene_sha256_annotation": "4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433",
    "expected_exe_sha256_annotation": "3473dc92e087c87858434125d5fb1ec879450682e3be64c508de6805029ce7a2",
    "expected_used_data_length_annotation": 0xBC8,
    "expected_spawn_xyz_annotation": [8960, 8960, 9216],
    "expected_spawn_heading_u8_annotation": 64,
    "expected_floor_z_annotation": 8704,
    "expected_music_track_id_annotation": 19,
}
OLD_TILED_MOVED = {
    "model_anchor": 0x8008A924, "scene_anchor": 0x8009A124,
    "moby_base": 0x8009A1F8, "dynamic_base": 0x8009A1F8, "arena_end": 0x8009A950,
    "bucket_base": 0x8009A960, "companion_model_root": 0x80097554,
}
OLD_PROFILES = ("starter-a-n16", "edited-landing-track02-r01", "tiled-three-track19-r01")


def fixture(reader=None):
    reader = reader or base.MemoryReader()
    for name, (_, _, value) in (GATES | CURSORS | base.REPORT).items():
        reader.set(name, value)
    reader.map(BASE, base.row() + base.row(0xFF) + bytes(88 * 14))
    pool = bytearray(0x2000); pool[1] = 0xFF
    reader.map(base.POOL, pool)
    reader.map(base.PETE, bytes.fromhex("ffffffff"))
    return reader


def old_fixture(profile):
    reader = base.fixture()
    if profile == "tiled-three-track19-r01":
        for name, value in OLD_TILED_MOVED.items(): reader.set(name, value)
        reader.set("dynamic_properties_cursor", 0x8009A950 - 24)
        reader.set("next_free_moby_not_active_end", 0x8009A1F8 + 88)
        reader.set("companion_handle", 0x8009A1F8)
        reader.map(0x8009A1F8, base.row() + base.row(0xFF) + bytes(88 * 14))
    return reader


def heap_reads(reader):
    return [(at, count) for at, count in reader.reads if at >= 0x80079000]


class TiledMaterialObserverTests(unittest.TestCase):
    def capture(self, reader=None):
        return observer.capture(reader or fixture(), "six-textured-three-tile", profile=PROFILE)

    def test_exact_code_owned_layout_and_annotations_never_authenticate_candidate(self):
        self.assertEqual(OLD_PROFILES + (PROFILE,), observer.PROFILE_CHOICES)
        gates, row_base, arena_end, bounds = observer._profile_layout(PROFILE)
        self.assertEqual(GATES, gates)
        self.assertEqual((BASE, END, (8448, 8448, 14592, 10496)), (row_base, arena_end, bounds))
        with self.assertRaises(TypeError): gates["model_anchor"] = (0, "<I", 0)
        reader = fixture(); sample = self.capture(reader)
        self.assertTrue(sample["payload_valid"])
        self.assertTrue(sample["gate_reread_consistent"])
        for name, (_, _, value) in GATES.items(): self.assertEqual(value, sample[name], name)
        for name, value in EXPECTED.items(): self.assertEqual(value, sample[name], name)
        for name in ("candidate_authenticated", "runtime_pass", "ui_result_approved", "normal_export_approved"):
            self.assertIs(False, sample[name], name)
        self.assertTrue(sample["sequential_reads_not_atomic"])
        self.assertEqual(255, sample["player_heading_u8"], "moving-player heading is not the annotated spawn heading")
        self.assertNotIn("attention", sample)
        self.assertEqual([(BASE, 88), (BASE, 88), (BASE + 88, 88)],
                         [(at, count) for at, count in heap_reads(reader) if at < base.POOL])
        self.assertEqual([(base.POOL + offset, 1024) for offset in range(0, 0x2000, 1024)] + [(base.PETE, 4)],
                         [(at, count) for at, count in heap_reads(reader) if at >= base.POOL])

    def test_independent_native_layout_arithmetic_keeps_padding_out_of_live_endpoint(self):
        self.assertEqual(0xBC8, 0xA58 + 2 * 184)
        self.assertEqual(0x8008AA94, 0x80089ECC + 0xBC8)
        self.assertEqual(0x8009A294, 0x8008AA94 + 0xF800)
        self.assertEqual(0x8009D294, 0x8009A294 + 0x3000)
        self.assertEqual(BASE, 0x8009A294 + 0xD4)
        self.assertEqual(END, BASE + 0x70 * 16 + 0x58)
        self.assertEqual(BUCKETS, END + 4 + 8 + 4)
        self.assertEqual(COMPANION, 0x8008AA94 + 0xCC30)
        self.assertEqual(0x8009A8E8, BASE + 16 * 88)
        self.assertEqual(0x8009A940, END - 16 * 24)
        self.assertEqual(88, END - 16 * 24 - (BASE + 16 * 88))
        self.assertTrue(self.capture()["payload_valid"])

    def test_all_old_profiles_and_new_profile_reject_incompatible_layout_before_heap(self):
        for old in OLD_PROFILES:
            with self.subTest(old=old):
                reader = fixture(); sample = observer.capture(reader, "wrong-layout", profile=old)
                self.assertFalse(sample["payload_valid"])
                self.assertEqual([], heap_reads(reader))
                reader = old_fixture(old); sample = self.capture(reader)
                self.assertFalse(sample["payload_valid"])
                self.assertEqual([], heap_reads(reader))
        for name, value in (("model_anchor", 0x8008A904), ("scene_anchor", 0x8009A104),
                            ("model_anchor", 0x8008AECC), ("scene_anchor", 0x8009A6CC)):
            reader = fixture(); reader.set(name, value)
            self.assertFalse(self.capture(reader)["payload_valid"])
            self.assertEqual([], heap_reads(reader))

    def test_every_gate_is_required_before_any_heap_payload(self):
        for name, (_, _, value) in GATES.items():
            with self.subTest(name=name):
                reader = fixture(); reader.set(name, value ^ 1)
                sample = self.capture(reader)
                self.assertFalse(sample["payload_valid"])
                self.assertIn("actor_scan_refused", sample)
                self.assertEqual([], heap_reads(reader))
        for state in (1, 12):
            reader = fixture(); reader.set("game_state", state)
            self.assertFalse(self.capture(reader)["payload_valid"])
            self.assertEqual([], heap_reads(reader))

    def test_cursor_grids_are_bounded_and_not_count_equations(self):
        for index in range(17):
            reader = fixture()
            reader.set("next_free_moby_not_active_end", BASE + index * 88)
            reader.set("dynamic_properties_cursor", END - (16 - index) * 24)
            self.assertTrue(self.capture(reader)["payload_valid"])
        for name, value in (("dynamic_count", 0), ("dynamic_count", 17),
                            ("companion_handle", 0), ("companion_handle", BASE - 88),
                            ("companion_handle", BASE + 1), ("companion_handle", BASE + 16 * 88),
                            ("dynamic_properties_cursor", END + 24), ("dynamic_properties_cursor", END - 17 * 24),
                            ("dynamic_properties_cursor", END - 1),
                            ("next_free_moby_not_active_end", BASE - 88),
                            ("next_free_moby_not_active_end", BASE + 17 * 88),
                            ("next_free_moby_not_active_end", BASE + 1)):
            with self.subTest(name=name, value=value):
                reader = fixture(); reader.set(name, value)
                self.assertFalse(self.capture(reader)["payload_valid"])
                self.assertEqual([], heap_reads(reader))

    def test_full_pools_never_read_seventeenth_moby_or_257th_particle(self):
        reader = fixture(); reader.map(BASE, base.row() * 16)
        reader.set("dynamic_count", 16); reader.set("companion_handle", BASE + 15 * 88)
        reader.map(base.POOL, (bytes([0x42, 2]) + bytes(30)) * 256)
        sample = self.capture(reader)
        self.assertTrue(sample["payload_valid"])
        self.assertEqual(15, sample["companion"]["index"])
        self.assertEqual(list(range(16)), [row["index"] for row in sample["dynamic_rows"]["active"]])
        self.assertTrue(sample["dynamic_rows"]["no_in_range_terminal"])
        self.assertEqual(256, sample["particles"]["active_count"])
        self.assertTrue(sample["particles"]["all256_slots_visited"])
        self.assertEqual(16, len(sample["particles"]["companion_effect_samples"]))
        self.assertEqual(2, len(sample["attention"]))
        self.assertNotIn((BASE + 16 * 88, 88), reader.reads)
        self.assertNotIn((base.PETE, 32), reader.reads)
        self.assertEqual([(base.PETE, 4)], [(at, count) for at, count in reader.reads if at >= base.PETE])
        self.assertTrue(all(at + count <= BASE + 16 * 88 for at, count in heap_reads(reader) if at < base.POOL))
        self.assertFalse(sample["runtime_pass"])

    def test_live_companion_and_signed_holes_do_not_chase_opaque_pointers(self):
        for bad in (bytes(88), base.row(0x80), base.row(0xFE), base.row(0xFF), base.row(class_id=0x11B)):
            reader = fixture(); reader.map(BASE, bad)
            self.assertFalse(self.capture(reader)["payload_valid"])
            self.assertEqual([(BASE, 88)], heap_reads(reader))
        reader = fixture(); reader.map(BASE + 88, base.row(0x80) + base.row(0xFE) + base.row(0xFF))
        sample = self.capture(reader)
        self.assertEqual([0], [row["index"] for row in sample["dynamic_rows"]["active"]])
        self.assertEqual(3, sample["dynamic_rows"]["terminal_slot"])
        self.assertNotIn((BASE + 4 * 88, 88), reader.reads)
        self.assertEqual(0xDEADBEEF, sample["companion"]["properties"])
        self.assertNotIn((0xDEADBEEF, 4), reader.reads)
        for pointer in (COMPANION, BUCKETS, 0x80188000, 0x800872A4):
            self.assertFalse(any(at == pointer for at, _ in reader.reads))

    def test_every_gate_and_cursor_is_reread_before_publishing_interpretations(self):
        for name, (at, _, value) in (GATES | CURSORS).items():
            with self.subTest(name=name):
                reader = fixture(); reader.flip_on_second_read[at] = value ^ 1
                sample = self.capture(reader)
                self.assertFalse(sample["payload_valid"])
                self.assertFalse(sample["gate_reread_consistent"])
                self.assertEqual([name], sample["changed_gate_fields"])
                for key in ("companion", "dynamic_rows", "particles", "sampled_player_xy_inside_authored_floor"):
                    self.assertNotIn(key, sample)
                self.assertFalse(sample["candidate_authenticated"])
                self.assertFalse(sample["runtime_pass"])

    def test_matching_reread_cannot_detect_intermediate_aba_or_create_atomic_pass(self):
        class AbaReader(base.MemoryReader):
            transitions = 0

            def read(self, address, length):
                if (address, length) == (BASE, 88):
                    self.transitions += 1
                    if self.transitions == 1: self.set("game_state", 12)
                    if self.transitions == 2: self.set("game_state", 0)
                return super().read(address, length)

        reader = fixture(AbaReader()); sample = self.capture(reader)
        self.assertEqual(2, reader.transitions)
        self.assertTrue(sample["payload_valid"])
        self.assertTrue(sample["gate_reread_consistent"])
        self.assertTrue(sample["sequential_reads_not_atomic"])
        self.assertFalse(sample["runtime_pass"])
        self.assertFalse(sample["candidate_authenticated"])
        self.assertFalse(sample["ui_result_approved"])

    def test_exact_three_tile_edges_seams_and_outside_are_xy_scope_only(self):
        for x in (8448, 10496, 12288, 12544, 14592):
            for y in (8448, 9472, 10496):
                for z in (-2147483648, 8704, 2147483647):
                    reader = fixture(); reader.set("player_xyz", (x, y, z))
                    sample = self.capture(reader)
                    self.assertTrue(sample["sampled_player_xy_inside_authored_floor"])
                    self.assertEqual([8448, 8448, 14592, 10496], sample["authored_floor_xy_bounds_annotation"])
                    self.assertNotIn("attention", sample)
                    self.assertFalse(sample["runtime_pass"])
        for x, y in ((8447, 9472), (14593, 9472), (12288, 8447), (12288, 10497)):
            reader = fixture(); reader.set("player_xyz", (x, y, 8704))
            sample = self.capture(reader)
            self.assertTrue(sample["payload_valid"])
            self.assertFalse(sample["sampled_player_xy_inside_authored_floor"])
            self.assertTrue(any("not a collision-bug diagnosis" in text for text in sample["attention"]))

    def test_three_old_profiles_and_shared_globals_are_unchanged_by_material_selection(self):
        before = {}
        for profile in OLD_PROFILES:
            reader = old_fixture(profile)
            sample = observer.capture(reader, "preservation", profile=profile); sample["utc"] = "fixed"
            self.assertTrue(sample["payload_valid"])
            before[profile] = (sample, reader.reads)
        self.capture()
        self.assertEqual(base.GATES, observer.GATES)
        self.assertEqual(base.BASE, observer.BASE)
        self.assertEqual(base.ARENA_END, observer.ARENA_END)
        for profile in OLD_PROFILES:
            reader = old_fixture(profile)
            sample = observer.capture(reader, "preservation", profile=profile); sample["utc"] = "fixed"
            self.assertEqual(before[profile], (sample, reader.reads))
        reader = base.fixture(); sample = observer.capture(reader, "starter-preservation"); sample["utc"] = "fixed"
        self.assertEqual("0fcc9a65d924086ad77b972cb20487f89cd8b6bb4b8099fdfe7486ab80d02d57",
                         hashlib.sha256(json.dumps(sample, sort_keys=True, separators=(",", ":")).encode()).hexdigest())
        self.assertEqual("7d32b2ad67f5de0234595a1f6492a6752489f35937b2e706f399f61f53992fc7",
                         hashlib.sha256(json.dumps(reader.reads, separators=(",", ":")).encode()).hexdigest())

    def test_closed_profile_rejects_metadata_and_cli_extensions_before_reads_or_connection(self):
        class StringSubclass(str): pass
        for value in (None, False, {}, [PROFILE], {"profile": PROFILE}, PROFILE + " ", StringSubclass(PROFILE),
                      EXPECTED["expected_candidate_sha256_annotation"]):
            reader = fixture()
            with self.subTest(value=value), self.assertRaises(ValueError):
                observer.capture(reader, "invalid", profile=value)
            self.assertEqual([], reader.reads)
        for args in (["--profile", PROFILE], ["--profile", PROFILE + " ", "--connect-identified-test-session"],
                     ["--profile", PROFILE, "--connect-identified-test-session", "--host", "example.com"],
                     ["--profile", PROFILE, "--connect-identified-test-session", "--candidate-sha256", EXPECTED["expected_candidate_sha256_annotation"]]):
            with patch.object(sys, "argv", ["observer", *args]), patch.object(socket, "create_connection") as connect:
                with self.assertRaises(SystemExit), patch("sys.stderr", io.StringIO()): observer.main()
                connect.assert_not_called()

    def test_read_only_transport_and_limits_are_reused_without_write_or_execution_packets(self):
        self.assertIs(observer.ReadOnlyClient, observer.wizard.ReadOnlyClient)
        self.assertIs(observer.decode_moby, observer.wizard.decode_moby)
        self.assertIs(observer.primitive_slack, observer.wizard.primitive_slack)
        sock = base.FakeSocket("00" * 88)
        self.assertEqual(bytes(88), observer.ReadOnlyClient(sock).read(BASE, 88))
        self.assertRegex(sock.sent[0], rb"^\$m8009a368,58#[0-9a-f]{2}$")
        self.assertEqual([b"+"], sock.sent[1:])
        for address, count in ((True, 4), (BASE, True), (BASE, 0), (BASE, 1025), (0, 4), (0x801FFFFF, 2)):
            sock = base.FakeSocket("00")
            with self.assertRaises(ValueError): observer.ReadOnlyClient(sock).read(address, count)
            self.assertEqual([], sock.sent)

    def test_explicit_cli_samples_only_manual_p_and_never_creates_runtime_pass(self):
        reader = fixture()
        with patch.object(sys, "argv", ["observer", "--profile", PROFILE, "--connect-identified-test-session"]), \
                patch.object(socket, "create_connection") as connect, \
                patch.object(observer, "ReadOnlyClient", return_value=reader), \
                patch.object(sys, "stdin", io.StringIO("continue\nstep\n\np material-manual\nq\np after-close\n")), \
                patch.object(sys, "stdout", io.StringIO()) as output:
            observer.main()
        connect.assert_called_once_with(("127.0.0.1", 2345), timeout=2)
        lines = output.getvalue().splitlines()
        self.assertEqual(2, len(lines), "no automatic, execution-command, or post-q samples")
        sample = json.loads(lines[1])
        self.assertEqual("material-manual", sample["label"])
        self.assertTrue(sample["payload_valid"])
        for name, value in EXPECTED.items(): self.assertEqual(value, sample[name], name)
        self.assertFalse(sample["runtime_pass"])
        self.assertFalse(sample["candidate_authenticated"])


if __name__ == "__main__":
    unittest.main()
