"""Offline, independently bounded tiled-family chest observer fixtures.

No ROMs, real sockets, emulator processes, settings, input or output files.
Literal moved owners match the frozen actual tiled-family native Scene witness.
"""
import contextlib
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
    "tiled_family_chest_observer", Path(__file__).parents[1] / "Observe-SpyroFreshChest.py")
observer = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = observer
_spec.loader.exec_module(observer)
PROFILE = "tiled-family-r01"
LEGACY_PROFILES = ("r01", "r02", "r03")
SCENE, END, ROWS, DYNAMIC, ARENA_END, BUCKET = (
    0x800A86F0, 0x800ABEF0, 0x800A87C4, 0x800A8ADC, 0x800A9934, 0x800A9A80)
POOL, PETE = 0x801C3BB8, 0x801C5BB8
OFFSETS = (0x1248, 0x1298, 0x12A0, 0x12A4, 0x12BC, 0x12E4, 0x130C, 0x1334, 0x135C)
LENGTHS = (0x50, 8, 4, 0x18, 0x28, 0x28, 0x28, 0x28, 0x28)
CLASSES = (0x11B, 0x1F5, 0xAD, 0xAE, 0xD, 0xD, 0xD, 0xD, 0xD)
MOVED = {"model_anchor": 0x8008D6F0, "scene_anchor": SCENE, "moby_base": ROWS,
         "dynamic_base": DYNAMIC, "arena_end": ARENA_END, "bucket_base": BUCKET}
ANNOTATIONS = {
    "scene_sha256": "4f560dcbfa2d997a3f02554cf297d95c1778f67fc5d8fcf8ff47f64d609a89e8",
    "body_sha256": "c8c85e71b633e29ce573dddccb04a4a0adcd6696d292658304328e435dfaa9ca",
    "executable_sha256": "81501f07ec9ce336089ec5a92f81bf51569aed723ac4f8ba1cf85ba0ef69a431",
    "candidate_sha256": "689fcf296e26d635d9c5ce6fc3fc687c2b47decce10ade2a0903f0c6bd877192",
}
# Filled from the unmodified observer before implementation. UTC is excluded;
# the JSON member order and exact bounded read sequence are part of this pin.
LEGACY_DIGESTS = {
    "r01": "5629098a1a95e95159a3c79e09d95e4864556660200b84dcace3726375e0f478",
    "r02": "e3a6d6ef9e621f89d0c389aaa8c881e17752751822d5f49f3c2e274fad23beb8",
    "r03": "164da7b5b5d5934155f19118933ea58d280a05d37f92c9ed6421ff34eeffa36c",
}


class Memory:
    def __init__(self):
        self.data, self.reads, self.flip, self.short = {}, [], None, None

    def map(self, address, data):
        self.data.update((address + index, value) for index, value in enumerate(data))

    def word(self, address, value):
        self.map(address, struct.pack("<I", value))

    def read(self, address, length):
        if type(address) is not int or not 1 <= length <= 1024:
            raise AssertionError("Unbounded fixture read")
        self.reads.append((address, length))
        if self.flip and address == self.flip[0] and self.reads.count((address, length)) == 2:
            self.word(address, self.flip[1])
        if any(address + index not in self.data for index in range(length)):
            raise AssertionError(f"Read escaped independently mapped owner: {address:08X}+{length:X}")
        data = bytes(self.data[address + index] for index in range(length))
        return data[:-1] if self.short == (address, length) else data


def row(index, scene, state=0):
    data = bytearray(88)
    struct.pack_into("<I", data, 0, scene + OFFSETS[index])
    struct.pack_into("<H", data, 0x36, CLASSES[index])
    data[0x48] = state
    return data


def fixture(tiled=True, full=False):
    memory = Memory()
    scene, rows, dynamic, arena = (SCENE, ROWS, DYNAMIC, ARENA_END) if tiled else (
        0x800A84A4, 0x800A8578, 0x800A8890, 0x800A96E8)
    for name, (address, fmt) in observer.FIELDS.items():
        values = [0] * len(struct.unpack(fmt, bytes(struct.calcsize(fmt))))
        values[0] = observer.GATES[name][2] if name in observer.GATES else 0
        if tiled and name in MOVED: values[0] = MOVED[name]
        if name == "dynamic_properties_cursor": values[0] = arena
        if name == "next_free_moby_not_active_end": values[0] = dynamic
        memory.map(address, struct.pack(fmt, *values))
    for index in range(9):
        memory.map(rows + index * 88, row(index, scene))
        memory.map(scene + OFFSETS[index], bytes(LENGTHS[index]))
    memory.map(dynamic, bytes(32 * 88))
    memory.map(POOL, bytes(0x2000))
    if not full:
        memory.map(dynamic + 0x48, b"\xff")
        memory.map(POOL + 1, b"\xff")
    memory.map(PETE, b"\xff" * 4)
    return memory


def digest(sample, memory):
    stable = {key: value for key, value in sample.items() if key != "utc"}
    return hashlib.sha256(json.dumps([stable, memory.reads], separators=(",", ":")).encode()).hexdigest()


class TiledFamilyTests(unittest.TestCase):
    def setUp(self):
        self.no_socket = patch.object(socket, "create_connection", side_effect=AssertionError("Offline only"))
        self.no_socket.start()
        self.addCleanup(self.no_socket.stop)

    def capture(self, memory=None):
        return observer.capture(memory or fixture(), "offline", PROFILE)

    def assert_refused(self, sample):
        self.assertFalse(sample["bounded_sample_valid"])
        self.assertFalse(sample["gate_reread_consistent"])
        for key in ("placed_rows", "dynamic_rows", "particles", "particle_terminal_word_not_record257"):
            self.assertNotIn(key, sample)
        self.assertTrue(all(sample[key] is False for key in observer.AUTHORITY_FLAGS))

    def test_legacy_fields_and_read_sequences_are_frozen(self):
        for profile in LEGACY_PROFILES:
            memory = fixture(tiled=False)
            sample = observer.capture(memory, "offline", profile)
            self.assertEqual(LEGACY_DIGESTS[profile], digest(sample, memory))
        memory = fixture(tiled=False)
        self.assertEqual(LEGACY_DIGESTS["r01"], digest(observer.capture(memory, "offline"), memory))

    def test_literal_layout_and_all_complete_static_owners(self):
        memory = fixture(); sample = self.capture(memory)
        self.assertTrue(sample["bounded_sample_valid"])
        self.assertTrue(sample["gate_reread_consistent"])
        self.assertEqual(0x8008CA38 + 0xCB8, sample["model_anchor"])
        self.assertEqual(0x8008D6F0 + 0x1B000, sample["scene_anchor"])
        self.assertEqual(END, sample["scene_anchor"] + sample["scene_disk_length"])
        self.assertEqual(ANNOTATIONS, sample["expected_payload_annotations"])
        self.assertEqual("private-tiled-family-chest-p9-runtime-experiment-v1", sample["expected_profile_annotation"])
        self.assertTrue(all(sample[key] is False for key in observer.AUTHORITY_FLAGS))
        expected = []
        for index in range(9):
            expected += [(ROWS + index * 88, 88), (SCENE + OFFSETS[index], LENGTHS[index])]
            self.assertEqual(SCENE + OFFSETS[index], sample["placed_rows"][index]["private_owner_sample"]["address"])
        expected += [(DYNAMIC, 88)] + [(POOL + at, 1024) for at in range(0, 0x2000, 1024)] + [(PETE, 4)]
        self.assertEqual(expected, [(at, length) for at, length in memory.reads if at >= 0x80079000])

    def test_all32_and_pool_owners_never_read_lookahead_or_record257(self):
        memory = fixture(full=True); sample = self.capture(memory)
        self.assertTrue(sample["bounded_sample_valid"])
        self.assertTrue(sample["dynamic_rows"]["no_in_range_terminal"])
        self.assertTrue(sample["particles"]["all256_slots_visited"])
        self.assertEqual([(DYNAMIC + index * 88, 88) for index in range(32)],
                         [(at, n) for at, n in memory.reads if DYNAMIC <= at < DYNAMIC + 33 * 88])
        self.assertEqual([DYNAMIC + index * 88 for index in range(32)],
                         [entry["address"] for entry in sample["dynamic_rows"]["active"]])
        self.assertEqual([(POOL + at, 1024) for at in range(0, 0x2000, 1024)] + [(PETE, 4)],
                         [(at, n) for at, n in memory.reads if at >= POOL])
        self.assertNotIn((DYNAMIC + 32 * 88, 88), memory.reads)
        self.assertNotIn((PETE, 32), memory.reads)

    def test_each_wrong_gate_and_old_anchor_refuses_before_payload(self):
        scalars = {(at, struct.calcsize(fmt)) for at, fmt in observer.FIELDS.values()}
        for name, (address, _, old) in observer.GATES.items():
            expected = MOVED.get(name, old)
            for wrong in {expected ^ 1, old} - {expected}:
                with self.subTest(name=name, wrong=wrong):
                    memory = fixture(); memory.word(address, wrong)
                    self.assert_refused(self.capture(memory))
                    self.assertTrue(all(read in scalars for read in memory.reads))
        for profile in LEGACY_PROFILES:
            memory = fixture()
            self.assert_refused(observer.capture(memory, "new-is-not-old", profile))
            self.assertTrue(all(read in scalars for read in memory.reads))

    def test_cursor_bounds_and_reread_every_gate_and_cursor(self):
        for name, values in (("dynamic_count", (33, 0xFFFFFFFF)),
                            ("dynamic_properties_cursor", (ARENA_END + 4, ARENA_END - 1, ARENA_END - 32 * 24 - 24))):
            for value in values:
                memory = fixture(); memory.word(observer.FIELDS[name][0], value)
                self.assert_refused(self.capture(memory))
                self.assertFalse(any(n == 88 for _, n in memory.reads))
        for name in observer.REREAD:
            memory = fixture(); address, _ = observer.FIELDS[name]
            expected = struct.unpack("<I", bytes(memory.data[address + n] for n in range(4)))[0]
            memory.flip = (address, expected ^ 1)
            sample = self.capture(memory); self.assert_refused(sample)
            self.assertEqual([name], sample["changed_gate_fields"])

    def test_truncated_scalar_row_private_dynamic_pool_and_separate_pete_refuse(self):
        owners = [(0x800785E4, 4), (PETE, 4)]
        owners += [(ROWS + index * 88, 88) for index in range(9)]
        owners += [(SCENE + offset, length) for offset, length in zip(OFFSETS, LENGTHS)]
        owners += [(DYNAMIC + index * 88, 88) for index in range(32)]
        owners += [(POOL + at, 1024) for at in range(0, 0x2000, 1024)]
        for short in owners:
            with self.subTest(short=short):
                memory = fixture(full=True); memory.short = short
                self.assert_refused(self.capture(memory))

    def test_foreign_relative_crossrow_old_deleted_owners_and_opaque_pointers_not_chased(self):
        for pointer in (0, 0xDEADBEEF, OFFSETS[3], SCENE + OFFSETS[4],
                        SCENE + OFFSETS[3] + 4, 0x800A84A4 + OFFSETS[3]):
            memory = fixture(); memory.word(ROWS + 3 * 88, pointer)
            sample = self.capture(memory)
            self.assertTrue(sample["bounded_sample_valid"])
            self.assertIn("private_scan_skipped", sample["placed_rows"][3])
            self.assertNotIn((SCENE + OFFSETS[3], LENGTHS[3]), memory.reads)
            self.assertNotIn((pointer, LENGTHS[3]), memory.reads)
        memory = fixture(); memory.map(ROWS + 2 * 88, row(2, SCENE, 0xFD))
        for name in ("key_state_raw_u32", "tracked_chest_gem_raw_u32", "companion_handle_raw_u32",
                     "next_free_moby_not_active_end", "next_free_particle_not_active_end"):
            memory.word(observer.FIELDS[name][0], 0xDEADBEEF)
        self.assertTrue(self.capture(memory)["bounded_sample_valid"])
        self.assertNotIn((SCENE + OFFSETS[2], LENGTHS[2]), memory.reads)
        self.assertFalse(any(at == 0xDEADBEEF for at, _ in memory.reads))

    def test_closed_immutable_layout_is_separate_from_detached_annotations(self):
        self.assertEqual({*LEGACY_PROFILES, PROFILE}, set(observer._LAYOUTS))
        layout = observer._LAYOUTS[PROFILE]
        self.assertEqual((SCENE, END, ROWS, DYNAMIC, ARENA_END, POOL, PETE),
                         tuple(layout[key] for key in ("scene", "scene_end", "rows", "dynamic", "arena_end", "pool", "pete")))
        for mapping in (observer._LAYOUTS, layout, layout["gates"], observer.PROFILES[PROFILE],
                        observer.PROFILES[PROFILE]["payload"]):
            with self.assertRaises(TypeError): mapping["caller"] = 0xDEADBEEF
        sample = self.capture(); sample["expected_payload_annotations"]["scene_sha256"] = "caller"
        memory = fixture()
        with patch.dict(observer.__dict__, {"PROFILES": {PROFILE: {
                "profile": "untrusted label", "payload": {"scene": 0xDEADBEEF}, "geometry": None}}}):
            self.assertTrue(self.capture(memory)["bounded_sample_valid"])
        self.assertFalse(any(at == 0xDEADBEEF for at, _ in memory.reads))
        memory = fixture()
        with patch.dict(observer.__dict__, {"PROFILES": {"caller": {
                "profile": "forged", "payload": ANNOTATIONS, "geometry": None}}}):
            with self.assertRaises(ValueError): observer.capture(memory, "forged", "caller")
        self.assertEqual([], memory.reads)
        for profile in (None, True, [], {}, "", "TILED-FAMILY-R01", "tiled-family-r02"):
            memory = fixture()
            with self.assertRaises(ValueError): observer.capture(memory, "bad", profile)
            self.assertEqual([], memory.reads)
        with self.assertRaises(TypeError): observer.capture(fixture(), "caller", PROFILE, layout={"scene": 0xDEADBEEF})

    def test_cli_uses_same_capture_and_read_packets_only(self):
        memory, output = fixture(full=True), io.StringIO()
        with patch.object(socket, "create_connection") as connection, \
                patch.object(sys, "stdin", io.StringIO("p offline\nq\n")), \
                patch.object(observer, "ReadOnlyClient", return_value=memory), contextlib.redirect_stdout(output):
            observer.main(["--connect-identified-test-session", "--profile", PROFILE])
        connection.assert_called_once_with(("127.0.0.1", 2345), timeout=2)
        sample = [json.loads(line) for line in output.getvalue().splitlines() if line.startswith("{")][0]
        self.assertTrue(sample["bounded_sample_valid"])
        class Wire:
            def __init__(self): self.reply, self.sent = bytearray(b"+" + observer.wizard.rsp.frame_payload("00000000")), []
            def recv(self, length):
                data = bytes(self.reply[:length]); del self.reply[:length]; return data
            def sendall(self, data): self.sent.append(data)
        wire = Wire()
        self.assertEqual(bytes(4), observer.ReadOnlyClient(wire).read(SCENE, 4))
        self.assertEqual([observer.wizard.rsp.frame_payload("m800a86f0,4"), b"+"], wire.sent)


if __name__ == "__main__":
    unittest.main(verbosity=2)
