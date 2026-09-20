"""Exact three-tile observer tests: fake RAM and pure byte fixtures only.

No source image, emulator, real socket, write packet or runtime acceptance.
The moved literals below independently match native-loader evidence 286.
"""
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import socket
import struct
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location(
    "tiled_observer_starter_control", Path(__file__).with_name("test_fresh_id65_observer.py"))
base = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(base)
observer = base.OBSERVER
TILED = "tiled-three-track19-r01"
BASE, END, BUCKETS, COMPANION = 0x8009A1F8, 0x8009A950, 0x8009A960, 0x80097554
MOVED = {
    "model_anchor": 0x8008A924, "scene_anchor": 0x8009A124,
    "moby_base": BASE, "dynamic_base": BASE, "arena_end": END,
    "bucket_base": BUCKETS, "companion_model_root": COMPANION,
}
GATES = {name: (at, fmt, MOVED.get(name, value)) for name, (at, fmt, value) in base.GATES.items()}
CURSORS = {
    "dynamic_count": (0x800756A4, "<I", 1),
    "dynamic_properties_cursor": (0x80075930, "<I", END - 24),
    "next_free_moby_not_active_end": (0x8007573C, "<I", BASE + 88),
    "companion_handle": (0x80075898, "<I", BASE),
}
EXPECTED = {
    "expected_profile_annotation": "fresh-id65-tiled-three-track19-r01-idle-seam-walk-research-v1",
    "expected_candidate_sha256_annotation": "7ba0b29b68a1bee415901382c3a3b5345ccb01ca84872502131f68361afdc305",
    "expected_body_sha256_annotation": "37157a7248c90665309d8cee21c739a846aca284426cd602c65d627788c06009",
    "expected_scene_sha256_annotation": "4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433",
    "expected_exe_sha256_annotation": "3473dc92e087c87858434125d5fb1ec879450682e3be64c508de6805029ce7a2",
    "expected_used_data_length_annotation": 0xA58,
    "expected_spawn_xyz_annotation": [8960, 8960, 9216],
    "expected_spawn_heading_u8_annotation": 64,
    "expected_floor_z_annotation": 8704,
    "expected_music_track_id_annotation": 19,
}


def fixture():
    reader = base.MemoryReader()
    for name, (_, _, value) in (GATES | CURSORS | base.REPORT).items():
        reader.set(name, value)
    reader.map(BASE, base.row() + base.row(0xFF) + bytes(88 * 14))
    pool = bytearray(0x2000); pool[1] = 0xFF
    reader.map(base.POOL, pool)
    reader.map(base.PETE, bytes.fromhex("ffffffff"))
    return reader


def heap_reads(reader):
    return [(at, n) for at, n in reader.reads if at >= 0x80079000]


class TiledObserverTests(unittest.TestCase):
    def capture(self, reader=None):
        return observer.capture(reader or fixture(), "three-tile", profile=TILED)

    def test_literal_moved_layout_annotations_and_bounded_reads(self):
        reader = fixture(); sample = self.capture(reader)
        self.assertTrue(sample["payload_valid"])
        self.assertTrue(sample["gate_reread_consistent"])
        for name, (_, _, value) in GATES.items(): self.assertEqual(value, sample[name], name)
        for name, value in EXPECTED.items(): self.assertEqual(value, sample[name], name)
        for name in ("runtime_pass", "candidate_authenticated", "ui_result_approved", "normal_export_approved"):
            self.assertIs(False, sample[name])
        self.assertTrue(sample["sequential_reads_not_atomic"])
        self.assertEqual(BASE, sample["companion"]["address"])
        self.assertEqual(0, sample["companion"]["index"])
        self.assertEqual(0xDEADBEEF, sample["companion"]["properties"])
        self.assertEqual([(BASE, 88), (BASE, 88), (BASE + 88, 88)],
                         [(at, n) for at, n in heap_reads(reader) if at < base.POOL])
        self.assertEqual([(base.POOL + n, 1024) for n in range(0, 0x2000, 1024)] + [(base.PETE, 4)],
                         [(at, n) for at, n in heap_reads(reader) if at >= base.POOL])
        self.assertNotIn((0xDEADBEEF, 4), reader.reads)
        self.assertNotIn("attention", sample)

    def test_pure_surface_fixture_and_existing_layout_arithmetic(self):
        # This independent fixture performs no source/image reads or writes.
        path = base.TOOLS.parent / "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_surface_oracle.py"
        spec = importlib.util.spec_from_file_location("tiled_observer_surface_fixture", path)
        surface = importlib.util.module_from_spec(spec); spec.loader.exec_module(surface)
        emitted = surface.fresh()
        self.assertEqual(0x2D4, len(emitted))
        self.assertEqual(((8448, 8448, 8704), (10496, 8448, 8704), (12544, 8448, 8704)), surface.TILES)
        self.assertEqual(0xA58, 8 + len(emitted) + 0x77C)
        self.assertEqual(MOVED["model_anchor"], 0x80089ECC + 0xA58)
        self.assertEqual(MOVED["scene_anchor"], MOVED["model_anchor"] + 0xF800)
        self.assertEqual(BASE, MOVED["scene_anchor"] + 0xD4)
        self.assertEqual(END, BASE + 0x70 * 16 + 0x58)
        self.assertEqual(BUCKETS, END + 4 + 8 + 4)
        self.assertEqual(COMPANION, MOVED["model_anchor"] + 0xCC30)
        self.assertEqual(0x8009A778, BASE + 16 * 88)
        self.assertEqual(0x8009A7D0, END - 16 * 24)
        self.assertEqual(88, END - 16 * 24 - (BASE + 16 * 88))
        self.assertTrue(self.capture()["payload_valid"])

    def test_profiles_refuse_each_others_layout_before_heap(self):
        for profile in ("starter-a-n16", "edited-landing-track02-r01"):
            reader = fixture(); sample = observer.capture(reader, "wrong-layout", profile=profile)
            self.assertFalse(sample["payload_valid"])
            self.assertEqual([], heap_reads(reader))
        reader = base.fixture(); sample = self.capture(reader)
        self.assertFalse(sample["payload_valid"])
        self.assertEqual([], heap_reads(reader))

    def test_every_tiled_gate_refuses_before_heap(self):
        for name, (_, _, value) in GATES.items():
            with self.subTest(name=name):
                reader = fixture(); reader.set(name, value ^ 1)
                sample = self.capture(reader)
                self.assertFalse(sample["payload_valid"])
                self.assertIn("actor_scan_refused", sample)
                self.assertEqual([], heap_reads(reader))

    def test_moved_cursor_grids_and_complete_sixteenth_row(self):
        for n in range(17):
            reader = fixture()
            reader.set("next_free_moby_not_active_end", BASE + n * 88)
            reader.set("dynamic_properties_cursor", END - (16 - n) * 24)
            self.assertTrue(self.capture(reader)["payload_valid"])
        for name, value in (("companion_handle", 0), ("companion_handle", base.BASE),
                            ("companion_handle", BASE + 1), ("companion_handle", BASE + 16 * 88),
                            ("dynamic_properties_cursor", END + 24), ("dynamic_properties_cursor", END - 17 * 24),
                            ("dynamic_properties_cursor", END - 1), ("dynamic_properties_cursor", base.ARENA_END),
                            ("next_free_moby_not_active_end", BASE - 88), ("next_free_moby_not_active_end", BASE + 17 * 88),
                            ("next_free_moby_not_active_end", BASE + 1), ("dynamic_count", 0), ("dynamic_count", 17)):
            reader = fixture(); reader.set(name, value)
            self.assertFalse(self.capture(reader)["payload_valid"], name)
            self.assertEqual([], heap_reads(reader), name)
        reader = fixture(); reader.map(BASE, base.row() * 16)
        reader.set("dynamic_count", 16); reader.set("companion_handle", BASE + 15 * 88)
        sample = self.capture(reader)
        self.assertTrue(sample["payload_valid"])
        self.assertEqual(15, sample["companion"]["index"])
        self.assertEqual(list(range(16)), [row["index"] for row in sample["dynamic_rows"]["active"]])
        self.assertTrue(sample["dynamic_rows"]["no_in_range_terminal"])
        self.assertNotIn((BASE + 16 * 88, 88), reader.reads)
        self.assertTrue(all(at + n <= BASE + 16 * 88 for at, n in heap_reads(reader) if at < base.POOL))
        self.assertFalse(sample["runtime_pass"])

    def test_signed_holes_early_terminal_and_stale_companion_stay_bounded(self):
        reader = fixture(); reader.map(BASE + 88, base.row(0x80) + base.row(0xFE) + base.row(0xFF))
        sample = self.capture(reader)
        self.assertEqual([0], [row["index"] for row in sample["dynamic_rows"]["active"]])
        self.assertEqual(3, sample["dynamic_rows"]["terminal_slot"])
        self.assertNotIn((BASE + 4 * 88, 88), reader.reads)
        for bad in (bytes(88), base.row(0x80), base.row(0xFF), base.row(class_id=0x11B)):
            reader = fixture(); reader.map(BASE, bad)
            self.assertFalse(self.capture(reader)["payload_valid"])
            self.assertEqual([(BASE, 88)], heap_reads(reader))

    def test_every_tiled_gate_and_cursor_is_reread_before_interpretation(self):
        for name, (at, _, value) in (GATES | CURSORS).items():
            reader = fixture(); reader.flip_on_second_read[at] = value ^ 1
            sample = self.capture(reader)
            self.assertFalse(sample["payload_valid"], name)
            self.assertFalse(sample["gate_reread_consistent"], name)
            self.assertEqual([name], sample["changed_gate_fields"])
            for key in ("companion", "dynamic_rows", "particles", "sampled_player_xy_inside_authored_floor"):
                self.assertNotIn(key, sample, name)

    def test_three_tile_extents_seams_leaf_boundary_and_z_non_authority(self):
        for x in (8448, 10496, 12288, 12544, 14592):
            for y in (8448, 9472, 10496):
                for z in (-2147483648, 8704, 2147483647):
                    reader = fixture(); reader.set("player_xyz", (x, y, z))
                    sample = self.capture(reader)
                    self.assertEqual([8448, 8448, 14592, 10496], sample["authored_floor_xy_bounds_annotation"])
                    self.assertTrue(sample["sampled_player_xy_inside_authored_floor"])
                    self.assertNotIn("attention", sample)
                    self.assertFalse(sample["runtime_pass"])
        for x, y in ((8447, 9472), (14593, 9472), (12288, 8447), (12288, 10497)):
            reader = fixture(); reader.set("player_xyz", (x, y, 8704))
            sample = self.capture(reader)
            self.assertTrue(sample["payload_valid"])
            self.assertFalse(sample["sampled_player_xy_inside_authored_floor"])
            self.assertTrue(any("not a collision-bug diagnosis" in text for text in sample["attention"]))

    def test_legacy_globals_sample_and_read_trace_unchanged_after_tiled_capture(self):
        self.capture()
        self.assertEqual(base.GATES, observer.GATES)
        self.assertEqual(base.BASE, observer.BASE)
        self.assertEqual(base.ARENA_END, observer.ARENA_END)
        for explicit in (False, True):
            reader = base.fixture()
            sample = observer.capture(reader, "starter-preservation", **({"profile": "starter-a-n16"} if explicit else {}))
            sample["utc"] = "fixed"
            self.assertEqual("0fcc9a65d924086ad77b972cb20487f89cd8b6bb4b8099fdfe7486ab80d02d57",
                             hashlib.sha256(json.dumps(sample, sort_keys=True, separators=(",", ":")).encode()).hexdigest())
            self.assertEqual("7d32b2ad67f5de0234595a1f6492a6752489f35937b2e706f399f61f53992fc7",
                             hashlib.sha256(json.dumps(reader.reads, separators=(",", ":")).encode()).hexdigest())

    def test_closed_profile_rejects_hostile_metadata_before_read_or_socket(self):
        class StringSubclass(str): pass
        for profile in (None, False, {}, [TILED], {"profile": TILED}, TILED + " ", StringSubclass(TILED)):
            reader = fixture()
            with self.subTest(profile=profile), self.assertRaises(ValueError):
                observer.capture(reader, "invalid", profile=profile)
            self.assertEqual([], reader.reads)
        for args in (["--profile", TILED], ["--profile", TILED + " ", "--connect-identified-test-session"]):
            with patch.object(sys, "argv", ["observer", *args]), patch.object(socket, "create_connection") as connect:
                with self.assertRaises(SystemExit), patch("sys.stderr", io.StringIO()): observer.main()
                connect.assert_not_called()

    def test_explicit_cli_selects_tiled_capture_with_mock_only_transport(self):
        reader = fixture()
        with patch.object(sys, "argv", ["observer", "--profile", TILED, "--connect-identified-test-session"]), \
                patch.object(socket, "create_connection") as connect, \
                patch.object(observer, "ReadOnlyClient", return_value=reader), \
                patch.object(sys, "stdin", io.StringIO("p tiled-cli\nq\n")), \
                patch.object(sys, "stdout", io.StringIO()) as output:
            observer.main()
        connect.assert_called_once_with(("127.0.0.1", 2345), timeout=2)
        sample = json.loads(output.getvalue().splitlines()[1])
        self.assertEqual(EXPECTED["expected_profile_annotation"], sample["expected_profile_annotation"])
        self.assertTrue(sample["payload_valid"])
        self.assertFalse(sample["runtime_pass"])


if __name__ == "__main__":
    unittest.main()
