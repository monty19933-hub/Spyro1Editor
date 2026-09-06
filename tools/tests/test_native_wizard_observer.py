"""No emulator, game connection or source image is used by these tests."""
import importlib.util
import hashlib
from pathlib import Path
import re
import struct
import sys
import unittest

SPEC = importlib.util.spec_from_file_location("wizard_observer", Path(__file__).resolve().parents[1] / "Observe-SpyroNativeWizard.py")
OBSERVER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = OBSERVER
SPEC.loader.exec_module(OBSERVER)


class FakeSocket:
    def __init__(self, reply):
        self.reply = bytearray(b"+" + OBSERVER.rsp.frame_payload(reply))
        self.sent = []

    def recv(self, count):
        result = bytes(self.reply[:count]); del self.reply[:count]; return result

    def sendall(self, value):
        self.sent.append(value)


class MemoryReader:
    def __init__(self): self.memory = {}; self.reads = []

    def map(self, address, value):
        for i, byte in enumerate(value): self.memory[address + i] = byte

    def read(self, address, length):
        self.reads.append((address, length))
        return bytes(self.memory[address + i] for i in range(length))


def memory_fixture(level):
    reader = MemoryReader()
    values = dict(OBSERVER.EXPECTED_ARENA)
    values.update(level=level, dynamic_count=0, dynamic_properties_cursor=0x8014021C,
                  particle_base=0x801C3BB8)
    for name, (address, encoding) in OBSERVER.FIELDS.items():
        count = len(struct.unpack(encoding, bytes(struct.calcsize(encoding))))
        reader.map(address, struct.pack(encoding, *([values.get(name, 0)] * count)))
    return reader


class ObserverTests(unittest.TestCase):
    def test_only_bounded_main_ram_reads_and_acknowledgements(self):
        sock = FakeSocket("01020304")
        self.assertEqual(bytes([1, 2, 3, 4]), OBSERVER.ReadOnlyClient(sock).read(0x80075828, 4))
        self.assertTrue(re.fullmatch(rb"\$m80075828,4#[0-9a-f]{2}", sock.sent[0]))
        self.assertEqual([b"+"], sock.sent[1:])
        for address, length in [(0x1F800024, 4), (0x7FFFFFFF, 4), (0x801FFFFF, 2),
                                (0x80000000, 0), (0x80000000, 1025), (True, 4)]:
            with self.subTest(address=address, length=length):
                target = FakeSocket("00")
                with self.assertRaises(ValueError): OBSERVER.ReadOnlyClient(target).read(address, length)
                self.assertEqual([], target.sent)

    def test_reply_is_exact_length_hex(self):
        for reply in ["00", "GGGGGGGG", "0102030405", "E01"]:
            with self.subTest(reply=reply), self.assertRaises(ValueError):
                OBSERVER.ReadOnlyClient(FakeSocket(reply)).read(0x80075828, 4)

    def test_actual_row_field_widths(self):
        row = bytearray(88)
        struct.pack_into("<I", row, 0, 0x801410B8)
        struct.pack_into("<iii", row, 12, 92832, 25248, -2)
        struct.pack_into("<H", row, 0x36, 0x11B)
        row[0x48] = 2; row[0x49] = 3; row[0x53] = 0x56
        decoded = OBSERVER.decode_moby(bytes(row), 128, 0x8013DA1C)
        self.assertEqual([92832, 25248, -2], decoded["xyz"])
        self.assertEqual(0x11B, decoded["class_id"])
        self.assertEqual([2, 3], decoded["state_bytes"])
        self.assertEqual(0x56, decoded["reward_selector"])
        with self.assertRaises(ValueError): OBSERVER.decode_moby(row[:-1], 128, 0)

    def test_player_animation_byte_matches_exact_native_source_window(self):
        # Retail EXE SHA a533d75c...b998; PC8003CC30..8003CC50, file+2D430.
        # This is an exact instruction fixture, not native CPU execution.
        window = bytes.fromhex("0880053cd08aa58c0780023c70c442242130a2000000c2900880013c718a22a0")
        self.assertEqual("2b99e9cee5b15975753d57b867d14096d518ce125e7bd22fdc1f65ec9b716411",
                         hashlib.sha256(window).hexdigest())
        words = struct.unpack("<8I", window)
        self.assertEqual((0x3C058008, 0x8CA58AD0, 0x3C028007, 0x2442C470,
                          0x00A23021, 0x90C20000, 0x3C018008, 0xA0228A71), words)
        signed_low = lambda word: struct.unpack("<h", struct.pack("<H", word & 0xFFFF))[0]
        self.assertEqual(OBSERVER.FIELDS["player_state"][0], ((words[0] & 0xFFFF) << 16) + signed_low(words[1]))
        self.assertEqual(0x8006C470, ((words[2] & 0xFFFF) << 16) + signed_low(words[3]))
        animation_address = ((words[6] & 0xFFFF) << 16) + signed_low(words[7])
        self.assertEqual((animation_address, "<B"), OBSERVER.FIELDS["player_animation_id"])
        table = bytes.fromhex("0001020304050608")  # Original state-table first8 bytes.
        self.assertEqual("bbfa8ae605ba3331a95204b71cb1d0454610815b50dd87334bce09124a38689f", hashlib.sha256(table).hexdigest())
        self.assertEqual(8, table[7])
        reader = memory_fixture(11)
        reader.map(animation_address, bytes([8]))
        sample = OBSERVER.capture(reader, "animation-byte")
        self.assertEqual(8, sample["player_animation_id"])
        self.assertIn((animation_address, 1), reader.reads)
        self.assertFalse(sample["runtime_pass"])

    def test_player_heading_is_one_unsigned_byte_not_camera_direction(self):
        self.assertEqual((0x80078A66, "<B"), OBSERVER.FIELDS["player_heading_u8"])
        for heading in [0, 64, 128, 192, 255]:
            reader = memory_fixture(10)
            reader.map(0x80078A65, bytes([0xA5, heading, 0x5A]))
            sample = OBSERVER.capture(reader, "heading-navigation-only")
            self.assertEqual(heading, sample["player_heading_u8"])
            self.assertIn((0x80078A66, 1), reader.reads)
            self.assertFalse(sample["runtime_pass"])

    def test_primitive_slack_requires_ordered_main_ram_pointers(self):
        self.assertEqual((0xC00, None), OBSERVER.primitive_slack(0x80190000, 0x80191000))
        self.assertEqual((-0x400, None), OBSERVER.primitive_slack(0x80191000, 0x80191000))
        self.assertEqual((0, None), OBSERVER.primitive_slack(0x801FFC00, 0x80200000))
        for cursor, limit in [(0, 0), (True, 0x80191000), (0x7FFFFFFF, 0x80191000),
                              (0x80200001, 0x80200001), (0x80191004, 0x80191000),
                              (0x80190000, 0x80200004), (0x80190000, 0x1F800000)]:
            with self.subTest(cursor=cursor, limit=limit):
                slack, reason = OBSERVER.primitive_slack(cursor, limit)
                self.assertIsNone(slack)
                self.assertTrue(reason)

    def test_camera_control_binding_matches_source_instruction_fixture(self):
        window = bytes.fromhex("0400628c0780033c206e6384c31202001e0042300780013c21082200d0c522840880013c248b22ac2110430043f50008ff0f4230")
        self.assertEqual("d7774168e1a050f09bbf3f06effbaa916c0f95686ec34991ccf3143e369aa5d1", hashlib.sha256(window).hexdigest())
        words = struct.unpack("<13I", window)
        self.assertEqual((0x3C038007, 0x84636E20), words[1:3])  # LUI / signed LH.
        self.assertEqual((0x00431021, 0x0800F543, 0x30420FFF), words[10:13])
        self.assertEqual((0x80076E20, "<h"), OBSERVER.FIELDS["camera_control_angle_s16"])
        store = bytes.fromhex("0880013c248b22ac")
        self.assertEqual("438bf2e51ba07bfc2d44cf02c84a6645b3228b7c14f7ae7bb2f30f25b9fe8987", hashlib.sha256(store).hexdigest())
        high, low = struct.unpack("<2I", store)
        signed_low = struct.unpack("<h", struct.pack("<H", low & 0xFFFF))[0]
        self.assertEqual((((high & 0xFFFF) << 16) + signed_low, "<I"), OBSERVER.FIELDS["player_desired_bearing_u32"])
        table = bytes.fromhex("00000000000c000e00080000000a000c00040002000000000006000400080000")
        self.assertEqual("9520ce65b9e72a77a5f9335e3e76a1d1a0caf97a58ee1942663a68a6be100556", hashlib.sha256(table).hexdigest())
        for pad, offset in [(0x1000, 0), (0x2000, 0xC00), (0x4000, 0x800), (0x8000, 0x400)]:
            self.assertEqual(offset, struct.unpack_from("<H", table, (pad >> 11) & 0x1E)[0])

    def test_navigation_fields_preserve_signed_widths_without_runtime_promotion(self):
        for camera in [-32768, -1, 0, 4095, 32767]:
            reader = memory_fixture(10)
            reader.map(0x80076E1F, b"\xa5" + struct.pack("<h", camera) + b"\x5a")
            reader.map(0x80078B24, struct.pack("<I", 0xABC))
            sample = OBSERVER.capture(reader, "navigation-only")
            self.assertEqual(camera, sample["camera_control_angle_s16"])
            self.assertEqual(0xABC, sample["player_desired_bearing_u32"])
            self.assertIn((0x80076E20, 2), reader.reads)
            self.assertIn((0x80078B24, 4), reader.reads)
            self.assertFalse(sample["runtime_pass"])

    def test_dynamic_negative_status_is_not_an_active_actor(self):
        rows = []
        for state in [0, 0x80, 0xFD, 0xFE, 4, 0xFF, 0]:
            row = bytearray(88); row[0x48] = state
            rows.append(bytes(row))
        result = OBSERVER.scan_dynamic(rows, 0x8013DA74)
        self.assertEqual([0, 4], [r["state_bytes"][0] for r in result["active"]])
        self.assertEqual(5, result["terminal_slot"])
        self.assertEqual(5, result["high_water_rows_before_terminal"])

    def test_particle_kind_not_selector_controls_holes_and_terminal(self):
        pool = bytearray(0x2000)
        for i, (selector, kind) in enumerate([(7, 2), (0x41, 1), (7, 0xFD), (7, 0xFE), (0, 0xFF)]):
            pool[i * 32] = selector; pool[i * 32 + 1] = kind
        result = OBSERVER.scan_particles(pool)
        self.assertEqual(2, result["active_count"])
        self.assertEqual(4, result["terminal_slot"])
        self.assertEqual([7, 0x41], [p["selector"] for p in result["wizard_effect_samples"]])
        with self.assertRaises(ValueError): OBSERVER.scan_particles(pool[:-1])

    def test_exact_profile_rejects_stale_or_exhausted_arena_before_scan(self):
        good = dict(OBSERVER.EXPECTED_ARENA)
        good["dynamic_count"] = 3
        good["dynamic_properties_cursor"] = 0x8014021C - 72
        OBSERVER.require_arena(good)
        for name, value in [("moby_base", 0), ("dynamic_base", 0x8013DA70),
                            ("arena_end", 0x80200000), ("dynamic_capacity", 91),
                            ("dynamic_count", 91), ("dynamic_properties_cursor", 0x80100000)]:
            bad = dict(good); bad[name] = value
            with self.subTest(name=name), self.assertRaises(ValueError): OBSERVER.require_arena(bad)
        for offset in range(91):
            good["dynamic_properties_cursor"] = good["arena_end"] - offset * 24
            OBSERVER.require_arena(good)
        good["dynamic_properties_cursor"] += 1
        with self.assertRaises(ValueError): OBSERVER.require_arena(good)

    def test_other_level_and_stale_profile_do_not_follow_heap_pointers(self):
        for level in [10, 11, 65]:
            reader = memory_fixture(level)
            result = OBSERVER.capture(reader, "other-level")
            self.assertFalse(result["runtime_pass"])
            self.assertIn("Skipped", result["actor_scan"])
            self.assertEqual(len(OBSERVER.FIELDS), len(reader.reads))
        reader = memory_fixture(12)
        reader.map(0x80075828, bytes(4))
        result = OBSERVER.capture(reader, "stale-arena")
        self.assertIn("moby_base", result["actor_scan_refused"])
        self.assertEqual(len(OBSERVER.FIELDS), len(reader.reads))

    def test_exact_empty_pool_snapshot_is_bounded_and_never_a_pass(self):
        reader = memory_fixture(12)
        row = bytearray(88); struct.pack_into("<H", row, 0x36, 0x11B)
        struct.pack_into("<I", row, 0, 0x801410B8)
        reader.map(0x8013DA1C, row)
        terminal = bytearray(88); terminal[0x48] = 0xFF
        reader.map(0x8013DA74, terminal)
        pool = bytearray(0x2000); pool[1] = 0xFF
        reader.map(0x801C3BB8, pool); reader.map(0x801C5BB8, bytes([255] * 4))
        result = OBSERVER.capture(reader, "bounded-empty")
        self.assertEqual([], result["dynamic_rows"]["active"])
        self.assertEqual(0, result["particles"]["active_count"])
        self.assertFalse(result["runtime_pass"])
        self.assertIsNone(result["primitive_sampled_slack"])
        self.assertTrue(result["primitive_sampled_slack_unavailable_reason"])
        self.assertTrue(all(length <= 1024 for _, length in reader.reads))
        self.assertEqual(1, sum(address == 0x801C5BB8 and length == 4 for address, length in reader.reads))
        self.assertFalse(any(address >= 0x8013DA74 + 88 and address < 0x8014021C for address, _ in reader.reads))

    def test_raw_reload_row_cannot_lend_stale_arena_globals_live_scan_authority(self):
        # Actual R01 death sample retained exact arena globals and class11B,
        # but row128 still held scene-relative EED4; ninety zero rows were
        # not initialized live actors. Other wrong pointers also refuse,
        # without guessing whether the cause is reload or corruption.
        for properties in [0, 0xEED4, 0x801410BC, 0x80200000]:
            with self.subTest(properties=hex(properties)):
                reader = memory_fixture(12)
                row = bytearray(88)
                struct.pack_into("<I", row, 0, properties)
                struct.pack_into("<H", row, 0x36, 0x11B)
                reader.map(0x8013DA1C, row)
                reader.map(0x8013DA74, bytes(90 * 88))
                reader.map(0x801C3BB8, bytes(0x2004))
                sample = OBSERVER.capture(reader, "raw-reload-or-unknown-layout")
                self.assertNotIn("dynamic_rows", sample.keys())
                self.assertNotIn("particles", sample.keys())
                self.assertIn("properties", sample["actor_scan_refused"])
                self.assertIn("Pause in UI", sample["attention"])
                self.assertFalse(sample["runtime_pass"])
                self.assertEqual(len(OBSERVER.FIELDS) + 1, len(reader.reads))
                self.assertEqual(properties, sample["wizard128"]["properties"])


if __name__ == "__main__": unittest.main()
