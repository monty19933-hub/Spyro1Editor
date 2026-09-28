"""Offline RAM fixtures only; no emulator, image or card access."""
import hashlib
import importlib.util
from pathlib import Path
import struct
import sys
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("admitted_egg_observer", Path(__file__).parents[1] / "Observe-SpyroJapaneseEggAdmittedLifecycle.py")
model = importlib.util.module_from_spec(spec); spec.loader.exec_module(model)
fixture_spec = importlib.util.spec_from_file_location("admission_fixture", Path(__file__).with_name("test_collectible_card_admission_observer.py"))
fixture = importlib.util.module_from_spec(fixture_spec); fixture_spec.loader.exec_module(fixture)
GUARD = bytes((i * 17) & 255 for i in range(0x800))


def memory(bits=1, level=10, phase=1):
    m = fixture.memory(phase)
    for at, data in model.RESIDENT_GATES.items(): m.map(at, data)
    m.word(model.FIELDS["level"], level)
    for at, value in model.SCENE_GATES.items(): m.word(at, value)
    for at, data in model.SCENE_CODE_GATES.items(): m.map(at, data)
    m.map(model.GUARD_PC, GUARD)
    for index, at in enumerate(model.ROWS):
        row = bytearray(0x58)
        struct.pack_into("<I", row, 0, model.PRIVATE[index])
        struct.pack_into("<H", row, 0x36, model.CLASSES[index])
        row[0x48] = 0xFD if bits & (1 << index) else 0
        m.map(at, row)
    m.map(model.LEDGER, model.expected_ledger(0, 50076, 1, bits))
    if phase != 1: m.map(model.LEDGER + 0x16, bytes([phase]))
    return m


class AdmittedEggObserverCases(unittest.TestCase):
    def setUp(self):
        self.pin = patch.object(model, "GUARD_SHA256", hashlib.sha256(GUARD).hexdigest())
        self.pin.start(); self.addCleanup(self.pin.stop)

    def test_seeded_and_collected_states_are_distinct_not_durable_proof(self):
        for bits in (1, 3):
            m = memory(bits); result = model.capture(m, "fixture")
            self.assertNotIn("sample_refused", result)
            self.assertEqual(bits == 1, result["seeded_state_observed"])
            self.assertEqual(bits == 3, result["pickup_state_observed"])
            self.assertTrue(result["egg_rows_observed"])
            self.assertEqual(1 if bits == 1 else 2, result["selected_earned"])
            for name in ("collection_verified", "native_save_reload_verified", "inventory_verified",
                         "completion_percentage_verified", "all_transition_lifetime_verified", "runtime_pass"):
                self.assertIs(False, result[name])
            self.assertTrue(all(0 < n <= 1024 and 0x80000000 <= at <= 0x80200000 - n for at, n in m.reads))

    def test_every_unapproved_ledger_byte_change_is_refused(self):
        for bits in (1, 3):
            for offset in range(0x10, 512):
                if 0x8C <= offset < 0x90: continue
                m = memory(bits); m.data[model.LEDGER + offset] ^= 0x80
                self.assertIn("sample_refused", model.capture(m, "bad"), (bits, offset))

    def test_phase_zero_ignores_stale_rows_and_metadata(self):
        m = memory(phase=0); m.map(model.LEDGER, bytes(512)); m.data[model.GUARD_PC] ^= 1
        result = model.capture(m, "not-loaded")
        self.assertNotIn("sample_refused", result)
        self.assertFalse(result["egg_rows_observed"])
        self.assertNotIn((model.GUARD_PC, 1024), m.reads)
        forbidden = {*model.SCENE_GATES, *model.SCENE_CODE_GATES, *model.ROWS, model.GUARD_PC, model.GUARD_PC + 1024}
        self.assertTrue(all(at not in forbidden for at, _ in m.reads))

    def test_non_artisans_or_pause_never_reads_discarded_overlay(self):
        for level, state in ((11, 0), (10, 2), (0, 13)):
            m = memory(level=level); m.word(model.FIELDS["game_state"], state)
            result = model.capture(m, "other-phase")
            self.assertNotIn("sample_refused", result)
            self.assertFalse(result["egg_rows_observed"])
            self.assertNotIn((model.GUARD_PC, 1024), m.reads)
            forbidden = {*model.SCENE_GATES, *model.SCENE_CODE_GATES, *model.ROWS, model.GUARD_PC, model.GUARD_PC + 1024}
            self.assertTrue(all(at not in forbidden for at, _ in m.reads))

    def test_resident_scene_and_code_changes_refused(self):
        for at in (*model.RESIDENT_GATES, *model.SCENE_GATES, *model.SCENE_CODE_GATES, model.GUARD_PC, model.GUARD_PC + 0x7FF):
            m = memory(); m.data[at] ^= 1
            self.assertIn("sample_refused", model.capture(m, "bad-gate"))

    def test_row_pointer_class_or_retirement_mismatch_refused(self):
        for at in model.ROWS:
            for offset in (0, 0x36, 0x48):
                m = memory(); m.data[at + offset] ^= 1
                self.assertIn("sample_refused", model.capture(m, "bad-row"))
            m = memory(); m.word(at, 0xFFFFFFFF)
            self.assertIn("sample_refused", model.capture(m, "poisoned-pointer"))
            self.assertTrue(all(address != 0xFFFFFFFF for address, _ in m.reads))

    def test_zero_epoch_unknown_bitmap_and_inconsistent_dirty_refused(self):
        for offset, data in ((0x8C, bytes(4)), (0x90, b"\x00"), (0x90, b"\x02"), (0x19, b"\x01")):
            m = memory(); m.map(model.LEDGER + offset, data)
            self.assertIn("sample_refused", model.capture(m, "bad-ledger"))

    def test_native_tuple_changes_refused(self):
        for name in ("bound", "controller", "slot", "marker"):
            m = memory(); m.data[model.FIELDS[name]] ^= 1
            self.assertIn("sample_refused", model.capture(m, "bad-tuple"))

    def test_sequential_ledger_fields_gates_and_row_identity_changes_refused(self):
        for at in (model.LEDGER, *model.FIELDS.values(), *model.SCENE_GATES, *model.SCENE_CODE_GATES, *model.RESIDENT_GATES, *model.ROWS):
            m = memory(); m.flip = at
            self.assertIn("sample_refused", model.capture(m, "unstable"), hex(at))

    def test_short_reads_refused(self):
        for at in (model.LEDGER, *model.FIELDS.values(), model.GUARD_PC, *model.ROWS):
            m = memory(); m.short = at
            self.assertIn("sample_refused", model.capture(m, "short"))

    def test_normal_native_guard_not_assumed_zero(self):
        m = memory(); m.map(model.LEDGER, bytes(range(16)))
        self.assertNotIn("sample_refused", model.capture(m, "native-guard"))

    def test_cli_has_no_candidate_override_or_unverified_connection(self):
        for args in ([], ["--connect-identified-test-session", "--pid", "1", "--candidate-bin", "x"]):
            with patch.object(sys, "argv", ["observer", *args]), patch.object(model.AdmittedEggSession, "image_sha256", ""), \
                    patch.object(model.socket, "create_connection") as connect:
                with self.assertRaises(SystemExit): model.main()
                connect.assert_not_called()


if __name__ == "__main__": unittest.main()
