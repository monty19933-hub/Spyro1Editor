"""Offline exact-boundary observer fixtures; no ROM, socket, emulator or writes."""
import importlib.util
from pathlib import Path
import struct
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location("egg_observer", Path(__file__).parents[1] / "Observe-SpyroJapaneseEggs.py")
observer = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(observer)


class Memory:
    def __init__(self):
        self.data, self.reads, self.flip, self.short = {}, [], None, None

    def map(self, address, data):
        self.data.update((address + i, value) for i, value in enumerate(data))

    def word(self, address, value):
        self.map(address, struct.pack("<I", value))

    def read(self, address, length):
        assert 1 <= length <= 1024
        self.reads.append((address, length))
        if self.flip and address == self.flip[0] and self.reads.count((address, length)) == 2:
            self.word(address, self.flip[1])
        value = bytes(self.data[address + i] for i in range(length))
        return value[:-1] if self.short == (address, length) else value


def fixture():
    memory = Memory()
    for name, (address, fmt) in observer.FIELDS.items():
        values = [0] * len(struct.unpack(fmt, bytes(struct.calcsize(fmt))))
        values[0] = observer.GATES[name][2] if name in observer.GATES else 0
        memory.map(address, struct.pack(fmt, *values))
    for i, (address, private) in enumerate(zip((0x80172BD0, 0x80172C28), (0x80176C20, 0x80176C2C))):
        row = bytearray(88)
        struct.pack_into("<I", row, 0, private)
        struct.pack_into("<iii", row, 12, 72704, 47104 + 2048 * i, 6656)
        struct.pack_into("<H", row, 0x36, 245)
        row[0x4B], row[0x4F] = 0x94, 1
        memory.map(address, row)
        memory.map(private, bytes(12))
    memory.map(0x801641B0, bytes.fromhex("30926aff") * 70)
    memory.map(0x801642C8, bytes.fromhex("11223300") * 70)
    return memory


class EggObserverTests(unittest.TestCase):
    def test_fixed_two_rows_and_colors(self):
        memory = fixture()
        result = observer.capture(memory, "x" * 200)
        self.assertNotIn("actor_sample_refused", result)
        self.assertEqual([174, 175], [e["index"] for e in result["eggs"]])
        self.assertEqual(0xFF6A9230, result["native_model_color_arrays"][0]["first_packed_word"])
        self.assertEqual(120, len(result["label"]))
        for flag in ("candidate_authenticated", "runtime_pass", "pickup_verified", "save_verified"):
            self.assertIs(False, result[flag])

    def test_every_gate_refuses_before_actor_reads(self):
        for _, (address, _, expected) in observer.GATES.items():
            with self.subTest(address=address):
                memory = fixture(); memory.word(address, expected ^ 1)
                result = observer.capture(memory, "wrong gate")
                self.assertIn("actor_sample_refused", result)
                self.assertTrue(all(length < 88 for _, length in memory.reads))

    def test_dynamic_capacity_bounds(self):
        memory = fixture(); memory.word(0x800756A4, 90)
        self.assertIn("actor_sample_refused", observer.capture(memory, "capacity"))

    def test_unrelocated_properties_never_chased(self):
        for value in (0x11ECC, 0x80000000, 0x80176C2C, 0xFFFFFFFF):
            memory = fixture(); memory.word(0x80172BD0, value)
            self.assertIn("actor_sample_refused", observer.capture(memory, "pointer"))
            self.assertFalse(any(address in (0x80176C20, 0x80176C2C, 0x801641B0, 0x801642C8)
                                 for address, _ in memory.reads))

    def test_changed_row_identity_refused(self):
        for at in (0x36, 0x48, 0x4B, 0x4F, 12):
            memory = fixture(); memory.data[0x80172BD0 + at] ^= 1
            self.assertIn("actor_sample_refused", observer.capture(memory, "row"))

    def test_layout_transition_discards_actor_result(self):
        memory = fixture(); memory.flip = (0x800785E4, 0x80164D58)
        result = observer.capture(memory, "transition")
        self.assertIn("actor_sample_refused", result)
        self.assertNotIn("eggs", result)

    def test_identity_transition_discards_actor_result(self):
        memory = fixture(); memory.flip = (0x80172C28, 0x11ED8)
        result = observer.capture(memory, "row transition")
        self.assertIn("actor_sample_refused", result)
        self.assertNotIn("eggs", result)

    def test_short_private_read_refused(self):
        memory = fixture(); memory.short = (0x80176C20, 12)
        self.assertIn("actor_sample_refused", observer.capture(memory, "short"))

    def test_cli_requires_opt_in_without_connection(self):
        with patch.object(sys, "argv", ["observer"]), patch.object(observer.socket, "create_connection") as connect:
            with self.assertRaises(SystemExit):
                observer.main()
            connect.assert_not_called()

    def test_transport_bounds_refuse_before_packet(self):
        class NoSend:
            def sendall(self, _):
                raise AssertionError("Invalid read sent a packet")
        client = observer.ReadOnlyClient(NoSend())
        for address, length in ((0, 4), (0x80000000, 0), (0x80000000, 1025), (0x801FFFFF, 2)):
            with self.assertRaises(ValueError):
                client.read(address, length)


if __name__ == "__main__":
    unittest.main()
