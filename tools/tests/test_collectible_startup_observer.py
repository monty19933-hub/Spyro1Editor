"""Offline fixtures only: no process, socket, ROM or card access."""
import importlib.util
from pathlib import Path
import struct
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("startup_observer", Path(__file__).parents[1] / "Observe-SpyroCollectibleStartup.py")
model = importlib.util.module_from_spec(spec)
spec.loader.exec_module(model)


class Memory:
    def __init__(self):
        self.data, self.reads, self.flip, self.short = {}, [], None, None

    def map(self, address, data):
        self.data.update((address + i, b) for i, b in enumerate(data))

    def word(self, address, value):
        self.map(address, struct.pack("<I", value))

    def read(self, address, length):
        self.reads.append((address, length))
        if self.flip and address == self.flip[0] and self.reads.count((address, length)) == 2:
            self.word(*self.flip)
        value = bytes(self.data[address + i] for i in range(length))
        return value[:-1] if self.short == (address, length) else value


def fixture():
    memory = Memory()
    for at, value in ((0x800755A4, 0xA00), (0x800755A8, 0x200000),
                      (0x800730C4, 0x184BC0), (0x8007596C, 10), (0x800757D8, 0),
                      (0x80075690, 0), (0x800785FC, 0x801C59B8),
                      (0x800785E8, 0x801879B0), (0x800785EC, 0x801A39B0),
                      (0x800785F0, 0x801BF9B0), (0x800785F4, 0x801BF9B8),
                      (0x800785F8, 0x801C39B8)):
        memory.word(at, value)
    for address, fmt in model.OBSERVATIONS.values():
        memory.map(address, bytes(struct.calcsize(fmt)))
    memory.map(0x801FF600, bytes(0x200))
    return memory


class StartupObserverTests(unittest.TestCase):
    def test_fixed_geometry_and_no_runtime_promotion(self):
        memory = fixture(); result = model.capture(memory, "x" * 200)
        self.assertNotIn("sample_refused", result)
        self.assertEqual(120, len(result["label"]))
        self.assertTrue(result["metadata_and_bitmap_region_zero"])
        self.assertEqual(0, result["metadata_nonzero_byte_count"])
        self.assertEqual("00" * 0x200, result["reservation_hex"])
        for name in ("clearing_execution_observed", "native_residence_approved", "save_verified", "runtime_pass"):
            self.assertIs(False, result[name])
        allowed = {(address, 4) for address, _ in model.GATES.values()}
        allowed |= {(address, struct.calcsize(fmt)) for address, fmt in model.OBSERVATIONS.values()}
        allowed.add((0x801FF600, 0x200))
        self.assertTrue(set(memory.reads) <= allowed)
        self.assertEqual(2, memory.reads.count((0x801FF600, 0x200)))

    def test_every_bad_gate_refuses_before_ledger(self):
        for address, expected in model.GATES.values():
            memory = fixture(); memory.word(address, expected ^ 1)
            result = model.capture(memory, "bad gate")
            self.assertIn("sample_refused", result)
            self.assertNotIn((0x801FF600, 0x200), memory.reads)

    def test_nonzero_metadata_is_evidence_not_silently_reset_or_approved(self):
        memory = fixture(); memory.word(0x801FF610, 0x524C4353)
        result = model.capture(memory, "unexpected")
        self.assertFalse(result["metadata_and_bitmap_region_zero"])
        self.assertEqual(4, result["metadata_nonzero_byte_count"])
        self.assertFalse(result["native_residence_approved"])

    def test_guard_distinguished_from_metadata(self):
        memory = fixture(); memory.word(0x801FF600, 0xFFFFFFFF)
        result = model.capture(memory, "sentinel")
        self.assertTrue(result["metadata_and_bitmap_region_zero"])
        self.assertEqual("ffffffff" + "00" * 12, result["guard_hex"])

    def test_changed_gate_or_reservation_drops_derived_result(self):
        for at in (0x800785FC, 0x800755A4, 0x801FF600):
            memory = fixture(); memory.flip = (at, 123)
            result = model.capture(memory, "transition")
            self.assertIn("sample_refused", result)
            self.assertNotIn("metadata_and_bitmap_region_zero", result)

    def test_short_read_refused(self):
        for extent in ((0x800755A4, 4), (0x801FF600, 0x200), (0x80078A58, 12)):
            memory = fixture(); memory.short = extent
            result = model.capture(memory, "short")
            self.assertIn("sample_refused", result)
            self.assertNotIn("metadata_and_bitmap_region_zero", result)

    def test_cli_explicit_identity_before_any_connection(self):
        for args in ([], ["--connect-identified-test-session"],
                     ["--connect-identified-test-session", "--pid", "123"],
                     ["--connect-identified-test-session", "--pid", "0", "--candidate-bin", "x"]):
            with patch.object(sys, "argv", ["observer", *args]), patch.object(model.socket, "create_connection") as connect:
                with self.assertRaises(SystemExit): model.main()
                connect.assert_not_called()

    def test_unknown_or_old_candidate_filename_refused(self):
        session = model.StartupSession("/private/test/collectible-startup-bootstrap-reservation-r01.bin", 123)
        self.assertEqual("895074e78abcf7de47faa15b62ee340239d996a486d1bc7ef5897c68cd4a06d3", session.image_sha256)
        self.assertIsNone(session.file)
        for name in ("original.bin", "artisans-native-japanese-eggs-authored-colors-r01.bin"):
            with self.assertRaises(ValueError): model.StartupSession("/private/test/" + name, 123)


if __name__ == "__main__":
    unittest.main()
