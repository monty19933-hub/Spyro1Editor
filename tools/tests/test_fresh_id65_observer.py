"""Tests first: the fresh-ID65 production observer must remain absent for RED.

No emulator/socket connection, source image or runtime acceptance is used here.
The literal fixture describes only fixed untextured A/N16. Candidate hashes are
annotations; an equal-anchor different scene cannot be authenticated from RAM.
"""
import importlib.util
from pathlib import Path
import re
import socket
import struct
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
TOOLS = Path(__file__).resolve().parents[1]


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, TOOLS / filename)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    with patch.object(socket, "create_connection", side_effect=AssertionError("No test may connect.")):
        spec.loader.exec_module(module)
    return module


WIZARD = load("fresh_observer_reused_wizard", "Observe-SpyroNativeWizard.py")
# Intentional tests-first RED: do not create a stub production module to hide it.
OBSERVER = load("fresh_id65_observer", "Observe-SpyroFreshId65.py")
BIN_SHA = "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188"
BASE, ARENA_END, POOL, PETE = 0x8009A068, 0x8009A7C0, 0x801C3BB8, 0x801C5BB8

# Address, exact width, initial value. These are independent literals rather
# than values copied from the observer's expected-value map.
GATES = {
    "level": (0x8007596C, "<I", 65),
    "game_state": (0x800757D8, "<I", 0),
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
    "dynamic_capacity": (0x800756A8, "<I", 16),
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
CURSORS = {
    "dynamic_count": (0x800756A4, "<I", 1),
    "dynamic_properties_cursor": (0x80075930, "<I", ARENA_END - 24),
    "next_free_moby_not_active_end": (0x8007573C, "<I", BASE + 88),
    "companion_handle": (0x80075898, "<I", BASE),
}
REPORT = {
    "lives": (0x8007582C, "<I", 4),
    "player_xyz": (0x80078A58, "<iii", (8192, 8192, 10240)),
    "player_heading_u8": (0x80078A66, "<B", 255),
    "camera_control_angle_s16": (0x80076E20, "<h", -1024),
    "player_desired_bearing_u32": (0x80078B24, "<I", 3072),
    "player_state": (0x80078AD0, "<I", 0),
    "player_substate": (0x80078AD4, "<I", 0),
    "player_state_timer": (0x80078AD8, "<I", 0),
    "player_animation_id": (0x80078A71, "<B", 8),
    "next_free_particle_not_active_end": (0x80075738, "<I", POOL),
    "primitive_cursor": (0x800757B0, "<I", 0x80188000),
    "primitive_limit": (0x80075780, "<I", 0x80189000),
    "ordering_table": (0x80075820, "<I", 0x80188000),
    "renderer_exhaustion_flag": (0x800758B0, "<I", 0),
    "last_collision_saved_sp_not_peak": (0x80077DFC, "<I", 0x801FFE00),
}
ALL_FIELDS = GATES | CURSORS | REPORT


class MemoryReader:
    def __init__(self):
        self.memory = {}
        self.reads = []
        self.flip_on_second_read = {}

    def map(self, address, value):
        self.memory.update((address + i, b) for i, b in enumerate(value))

    def set(self, name, value):
        address, encoding, _ = ALL_FIELDS[name]
        self.map(address, struct.pack(encoding, *(value if isinstance(value, tuple) else (value,))))

    def read(self, address, length):
        if type(address) is not int or type(length) is not int or not 1 <= length <= 1024:
            raise AssertionError("Unbounded test read.")
        self.reads.append((address, length))
        if (address in self.flip_on_second_read and
                self.reads.count((address, length)) == 2):
            self.map(address, struct.pack("<I", self.flip_on_second_read[address]))
        try: return bytes(self.memory[address + i] for i in range(length))
        except KeyError as error: raise AssertionError("Read escaped approved fixture owner: " + hex(address)) from error


def row(status=0, class_id=0x78, properties=0xDEADBEEF):
    data = bytearray(88)
    struct.pack_into("<I", data, 0, properties)
    struct.pack_into("<H", data, 0x36, class_id)
    data[0x48] = status
    return bytes(data)


def fixture():
    reader = MemoryReader()
    for name, (_, _, value) in ALL_FIELDS.items(): reader.set(name, value)
    reader.map(BASE, row() + row(0xFF) + bytes(88 * 14))
    pool = bytearray(0x2000)
    pool[1] = 0xFF
    reader.map(POOL, pool)
    reader.map(PETE, bytes.fromhex("ffffffff"))
    return reader


def heap_reads(reader):
    return [(at, n) for at, n in reader.reads
            if BASE <= at < ARENA_END or POOL <= at < PETE + 4]


class FakeSocket:
    def __init__(self, reply):
        self.reply = bytearray(b"+" + WIZARD.rsp.frame_payload(reply))
        self.sent = []
    def recv(self, count):
        value = bytes(self.reply[:count]); del self.reply[:count]; return value
    def sendall(self, value): self.sent.append(value)


class FreshObserverTests(unittest.TestCase):
    def test_fixed_fields_and_reused_read_only_primitives(self):
        self.assertEqual({name: (at, fmt) for name, (at, fmt, _) in ALL_FIELDS.items()}, OBSERVER.FIELDS)
        self.assertEqual((TOOLS / "Observe-SpyroNativeWizard.py").resolve(),
                         Path(OBSERVER.wizard.__file__).resolve())
        self.assertIs(OBSERVER.ReadOnlyClient, OBSERVER.wizard.ReadOnlyClient)
        self.assertIs(OBSERVER.decode_moby, OBSERVER.wizard.decode_moby)
        self.assertIs(OBSERVER.primitive_slack, OBSERVER.wizard.primitive_slack)
        self.assertEqual(BASE + 16 * 88, 0x8009A5E8)
        self.assertEqual(ARENA_END - 16 * 24, 0x8009A640)
        self.assertEqual(88, ARENA_END - 16 * 24 - (BASE + 16 * 88))

    def test_transport_sends_only_memory_and_ack(self):
        sock = FakeSocket("00112233")
        self.assertEqual(bytes.fromhex("00112233"), OBSERVER.ReadOnlyClient(sock).read(0x8007596C, 4))
        self.assertRegex(sock.sent[0], rb"^\$m8007596c,4#[0-9a-f]{2}$")
        self.assertEqual([b"+"], sock.sent[1:])
        for address, size in [(True, 4), (0x80000000, True), (0, 4),
                              (0x80000000, 0), (0x80000000, 1025), (0x801FFFFF, 2)]:
            with self.subTest(address=address, size=size):
                sock = FakeSocket("00")
                with self.assertRaises(ValueError): OBSERVER.ReadOnlyClient(sock).read(address, size)
                self.assertEqual([], sock.sent)
        for reply in ("00", "GGGGGGGG", "0011223344", "E01"):
            with self.subTest(reply=reply), self.assertRaises(ValueError):
                OBSERVER.ReadOnlyClient(FakeSocket(reply)).read(0x8007596C, 4)

    def test_valid_baseline_bounded_payload_and_non_authority(self):
        reader = fixture()
        sample = OBSERVER.capture(reader, "idle")
        self.assertTrue(sample["payload_valid"])
        self.assertTrue(sample["gate_reread_consistent"])
        self.assertTrue(sample["sequential_reads_not_atomic"])
        self.assertFalse(sample["runtime_pass"])
        self.assertFalse(sample["candidate_authenticated"])
        self.assertEqual(BIN_SHA, sample["expected_candidate_sha256_annotation"])
        self.assertEqual(0x78, sample["companion"]["class_id"])
        self.assertEqual(0xDEADBEEF, sample["companion"]["properties"])
        self.assertEqual([0], [r["index"] for r in sample["dynamic_rows"]["active"]])
        self.assertEqual(1, sample["dynamic_rows"]["terminal_slot"])
        self.assertEqual(0, sample["particles"]["active_count"])
        self.assertEqual("ffffffff", sample["particle_terminal_word_not_record257"])
        self.assertEqual(0xC00, sample["primitive_sampled_slack"])
        self.assertNotIn("wizard128", sample)
        self.assertNotIn("wizard_effect_samples", sample["particles"])
        self.assertNotIn((0xDEADBEEF, 4), reader.reads)
        self.assertEqual([(POOL + n, 1024) for n in range(0, 0x2000, 1024)],
                         [(a, n) for a, n in reader.reads if POOL <= a < PETE])
        self.assertEqual([(PETE, 4)], [(a, n) for a, n in reader.reads if a >= PETE])

    def test_every_fixed_gate_refuses_before_heap(self):
        for name, (_, _, value) in GATES.items():
            with self.subTest(name=name):
                reader = fixture(); reader.set(name, value ^ 1)
                sample = OBSERVER.capture(reader, name)
                self.assertFalse(sample["payload_valid"])
                self.assertTrue(sample["actor_scan_refused"])
                self.assertEqual([], heap_reads(reader))
        for state in (1, 12):
            reader = fixture(); reader.set("game_state", state)
            self.assertFalse(OBSERVER.capture(reader, "transition")["payload_valid"])
            self.assertEqual([], heap_reads(reader))

    def test_wrong_n1_and_textured_owners_refuse(self):
        for name, value in [("dynamic_capacity", 1), ("arena_end", BASE + 0xC8),
                            ("model_anchor", 0x8008A904), ("scene_anchor", 0x8009A104)]:
            reader = fixture(); reader.set(name, value)
            self.assertFalse(OBSERVER.capture(reader, "other-profile")["payload_valid"])
            self.assertEqual([], heap_reads(reader))

    def test_companion_is_live_bounded_not_raw_stale_zero_or_opaque_chase(self):
        for handle in (0, BASE - 88, BASE + 1, BASE + 16 * 88, 0xDEADBEEF):
            reader = fixture(); reader.set("companion_handle", handle)
            self.assertFalse(OBSERVER.capture(reader, "bad-handle")["payload_valid"])
            self.assertEqual([], heap_reads(reader))
        for bad in (bytes(88), row(0, 0x11B), row(0x80), row(0xFE), row(0xFF)):
            reader = fixture(); reader.map(BASE, bad)
            sample = OBSERVER.capture(reader, "raw-reload")
            self.assertFalse(sample["payload_valid"])
            self.assertEqual([(BASE, 88)], heap_reads(reader))
        reader = fixture(); reader.map(BASE, row(properties=0))
        self.assertTrue(OBSERVER.capture(reader, "opaque-null")["payload_valid"])
        self.assertNotIn((0, 4), reader.reads)

    def test_independent_cursor_grids_not_count_equations(self):
        for n in range(17):
            reader = fixture()
            reader.set("next_free_moby_not_active_end", BASE + n * 88)
            reader.set("dynamic_properties_cursor", ARENA_END - (16 - n) * 24)
            self.assertTrue(OBSERVER.capture(reader, "independent-grid")["payload_valid"])
        for name, value in [("dynamic_count", 0), ("dynamic_count", 17),
                            ("dynamic_properties_cursor", ARENA_END + 24),
                            ("dynamic_properties_cursor", ARENA_END - 17 * 24),
                            ("dynamic_properties_cursor", ARENA_END - 1),
                            ("next_free_moby_not_active_end", BASE - 88),
                            ("next_free_moby_not_active_end", BASE + 17 * 88),
                            ("next_free_moby_not_active_end", BASE + 1)]:
            reader = fixture(); reader.set(name, value)
            self.assertFalse(OBSERVER.capture(reader, "bad-grid")["payload_valid"])
            self.assertEqual([], heap_reads(reader))

    def test_all_signed_inactive_rows_and_last_terminal(self):
        # A first terminal is a decoder case, not a scan-eligible live baseline:
        # the separate mandatory companion gate would reject such a baseline.
        first = OBSERVER.scan_dynamic(iter([row(0xFF)]), BASE)
        self.assertEqual(0, first["terminal_slot"])
        self.assertEqual([], first["active"])
        for status in range(0x80, 0xFF):
            reader = fixture()
            reader.map(BASE + 88, row(status) + row(0xFF))
            sample = OBSERVER.capture(reader, "hole")
            self.assertEqual([0], [r["index"] for r in sample["dynamic_rows"]["active"]])
            self.assertEqual(2, sample["dynamic_rows"]["terminal_slot"])
        reader = fixture(); reader.map(BASE, row() * 15 + row(0xFF))
        sample = OBSERVER.capture(reader, "last-terminal")
        self.assertEqual(15, sample["dynamic_rows"]["terminal_slot"])
        self.assertEqual(list(range(15)), [r["index"] for r in sample["dynamic_rows"]["active"]])
        self.assertLessEqual(max(a + n for a, n in heap_reads(reader) if a < POOL), BASE + 16 * 88)

    def test_full_sixteen_never_reads_seventeenth_and_reports_attention(self):
        reader = fixture(); reader.map(BASE, row() * 16); reader.set("dynamic_count", 16)
        sample = OBSERVER.capture(reader, "full")
        self.assertEqual(16, len(sample["dynamic_rows"]["active"]))
        self.assertTrue(sample["dynamic_rows"]["no_in_range_terminal"])
        self.assertTrue(sample["attention"])
        self.assertNotIn((BASE + 16 * 88, 88), reader.reads)
        self.assertFalse(sample["runtime_pass"])

    def test_particle_kind_holes_selectors_samples_and_full_256(self):
        pool = bytearray(0x2000)
        for i in range(256):
            pool[i * 32] = 0x0C if i % 2 else 0x42
            pool[i * 32 + 1] = 2
        reader = fixture(); reader.map(POOL, pool)
        sample = OBSERVER.capture(reader, "full-pool")
        self.assertEqual(256, sample["particles"]["active_count"])
        self.assertTrue(sample["particles"]["all256_slots_visited"])
        self.assertEqual({"42/kind2": 128, "0C/kind2": 128}, sample["particles"]["selector_kind_counts"])
        self.assertEqual(16, len(sample["particles"]["companion_effect_samples"]))
        self.assertTrue(sample["attention"])
        for i, kind in enumerate(range(0x80, 0xFF)): pool[i * 32 + 1] = kind
        pool[127 * 32 + 1] = 0xFF
        reader = fixture(); reader.map(POOL, pool)
        sample = OBSERVER.capture(reader, "signed-holes")
        self.assertEqual(0, sample["particles"]["active_count"])
        self.assertEqual(127, sample["particles"]["terminal_slot"])
        self.assertNotIn((PETE, 32), reader.reads)

    def test_pete_word_attention_is_not_complete_pete_authentication(self):
        reader = fixture(); reader.map(PETE, bytes(4))
        sample = OBSERVER.capture(reader, "terminal-changed")
        self.assertTrue(sample["attention"])
        self.assertFalse(sample["runtime_pass"])
        self.assertNotIn((PETE + 0x20, 4), reader.reads)

    def test_postpayload_gate_changes_invalidate_all_interpretations(self):
        for name in tuple(GATES) + tuple(CURSORS):
            address, _, value = ALL_FIELDS[name]
            reader = fixture(); reader.flip_on_second_read[address] = value ^ 1
            sample = OBSERVER.capture(reader, "non-atomic-change")
            self.assertFalse(sample["gate_reread_consistent"], name)
            self.assertFalse(sample["payload_valid"], name)
            self.assertTrue(sample["sequential_reads_not_atomic"])
            self.assertTrue(sample["attention"])
            for key in ("companion", "dynamic_rows", "particles"):
                self.assertNotIn(key, sample, name)

    def test_widths_player_state_namespace_and_invalid_primitive_slack(self):
        reader = fixture()
        reader.map(0x80078A70, bytes([0xA5, 8, 0x5A, 0xEE, 0xDD]))
        reader.set("player_state", 12)  # Not the forbidden global transition12.
        sample = OBSERVER.capture(reader, "state-namespace")
        self.assertTrue(sample["payload_valid"])
        self.assertEqual(8, sample["player_animation_id"])
        self.assertIn((0x80078A71, 1), reader.reads)
        for cursor, limit in ((0, 0), (0x80189004, 0x80189000), (0x80188000, 0x80200004)):
            reader = fixture(); reader.set("primitive_cursor", cursor); reader.set("primitive_limit", limit)
            sample = OBSERVER.capture(reader, "primitive-invalid")
            self.assertIsNone(sample["primitive_sampled_slack"])
            self.assertTrue(sample["primitive_sampled_slack_unavailable_reason"])
        reader = fixture(); reader.set("renderer_exhaustion_flag", 1)
        self.assertTrue(OBSERVER.capture(reader, "exhaustion")["attention"])

    def test_original_wizard_still_rejects_level65_without_actor_reads(self):
        reader = MemoryReader()
        for name, (address, encoding) in WIZARD.FIELDS.items():
            count = len(struct.unpack(encoding, bytes(struct.calcsize(encoding))))
            value = 65 if name == "level" else 0
            reader.map(address, struct.pack(encoding, *([value] * count)))
        sample = WIZARD.capture(reader, "not-dark")
        self.assertIn("not Dark Hollow", sample["actor_scan"])
        self.assertNotIn("dynamic_rows", sample)
        self.assertFalse(sample["runtime_pass"])
        self.assertTrue(all(at < 0x80079000 for at, _ in reader.reads))

    def test_main_without_explicit_opt_in_does_not_connect(self):
        with patch.object(sys, "argv", ["Observe-SpyroFreshId65.py"]), patch.object(
                socket, "create_connection", side_effect=AssertionError("Unexpected connection.")) as connect:
            with self.assertRaises(SystemExit) as error: OBSERVER.main()
            self.assertNotEqual(0, error.exception.code)
            connect.assert_not_called()


if __name__ == "__main__":
    unittest.main()
