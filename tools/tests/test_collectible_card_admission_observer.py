"""Offline bounded RAM fixtures only; no emulator or card connection."""
import importlib.util
from pathlib import Path
import struct
import sys
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("admission_observer", Path(__file__).parents[1] / "Observe-SpyroCollectibleCardAdmission.py")
model = importlib.util.module_from_spec(spec); spec.loader.exec_module(model)


class Memory:
    def __init__(self): self.data, self.reads, self.flip, self.short = {}, [], None, None
    def map(self, at, data): self.data.update((at + i, b) for i, b in enumerate(data))
    def word(self, at, value): self.map(at, struct.pack("<I", value))
    def read(self, at, size):
        self.reads.append((at, size))
        if self.flip == at and self.reads.count((at, size)) == 2: self.data[at] ^= 1
        result = bytes(self.data[at + i] for i in range(size))
        return result[:-1] if self.short == at else result


def memory(phase=1):
    result = Memory()
    for at, data in model.GATES.items(): result.map(at, data)
    values = dict(bound=1, controller=0, slot=0, marker=50076, game_state=0, level=10)
    for name, at in model.FIELDS.items(): result.word(at, values[name])
    data = bytearray(model.reader.expected_shadow(0, 50076, 1)); data[0x16] = phase
    result.map(model.LEDGER, data)
    return result


class CardAdmissionObserverCases(unittest.TestCase):
    def test_complete_ready_snapshot_does_not_promote_save_inventory_or_lifetime(self):
        m = memory(); result = model.capture(m, "fixture")
        self.assertNotIn("sample_refused", result)
        self.assertTrue(result["live_ready_fixture_observed"])
        for name in ("collection_verified", "native_save_reload_verified", "inventory_verified",
                     "completion_percentage_verified", "all_transition_lifetime_verified", "runtime_pass",
                     "session_epoch_independently_authenticated"):
            self.assertIs(False, result[name])
        self.assertTrue(all(0 < size <= 1024 and 0x80000000 <= at <= 0x80200000 - size for at, size in m.reads))

    def test_native_guard_is_preserved_not_assumed_zero(self):
        m = memory(); m.map(model.LEDGER, bytes(range(16)))
        result = model.capture(m, "guard")
        self.assertTrue(result["live_ready_fixture_observed"])
        self.assertEqual(bytes(range(16)).hex(), result["guard_hex"])

    def test_not_loaded_never_promotes_stale_ready_metadata(self):
        for blank in (False, True):
            m = memory(0)
            if blank: m.map(model.LEDGER, bytes(512))
            result = model.capture(m, "not-loaded")
            self.assertNotIn("sample_refused", result)
            self.assertFalse(result["live_ready_fixture_observed"])

    def test_unknown_phase_refused(self):
        for phase in (2, 127, 255):
            result = model.capture(memory(phase), "phase")
            self.assertIn("sample_refused", result)
            self.assertFalse(result["live_ready_fixture_observed"])

    def test_every_ready_metadata_or_bitmap_byte_mutation_refused(self):
        for offset in range(0x10, 512):
            # Controller/marker match resident native fields. Epoch is merely
            # nonzero and stable in the repeated ledger, not independently
            # authenticated after the card overlay has been unloaded.
            if 0x8C <= offset < 0x90: continue
            m = memory(); m.data[model.LEDGER + offset] ^= 0x80
            with self.subTest(offset=offset):
                result = model.capture(m, "bad-ledger")
                self.assertIn("sample_refused", result)
                self.assertFalse(result["live_ready_fixture_observed"])
        m = memory(); m.word(model.LEDGER + 0x8C, 0)
        self.assertIn("sample_refused", model.capture(m, "zero-epoch"))

    def test_native_tuple_mutation_refused(self):
        for name in ("bound", "controller", "slot", "marker"):
            m = memory(); m.data[model.FIELDS[name]] ^= 1
            self.assertIn("sample_refused", model.capture(m, "tuple"))

    def test_resident_gates_refused_before_ledger_read(self):
        for at in model.GATES:
            m = memory(); m.data[at] ^= 1
            self.assertIn("sample_refused", model.capture(m, "gate"))
            self.assertNotIn((model.LEDGER, 512), m.reads)

    def test_sequential_changes_or_partial_reads_are_not_accepted(self):
        for at in (model.LEDGER, *model.FIELDS.values(), *model.GATES):
            m = memory(); m.flip = at
            self.assertIn("sample_refused", model.capture(m, "flip"))
            m = memory(); m.short = at
            self.assertIn("sample_refused", model.capture(m, "short"))

    def test_cli_requires_explicit_fixed_verified_candidate_before_connection(self):
        cases = ([], ["--connect-identified-test-session"],
                 ["--connect-identified-test-session", "--pid", "0", "--candidate-bin", "x"],
                 ["--connect-identified-test-session", "--pid", "1", "--candidate-bin", "x"])
        for args in cases:
            with patch.object(sys, "argv", ["observer", *args]), patch.object(model.CardAdmissionSession, "image_sha256", ""), \
                    patch.object(model.socket, "create_connection") as connect:
                with self.assertRaises(SystemExit): model.main()
                connect.assert_not_called()


if __name__ == "__main__": unittest.main()
