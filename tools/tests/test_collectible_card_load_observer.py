"""Offline fixed-address RAM fixtures only; no emulator, card or ROM access."""
import hashlib
import importlib.util
from pathlib import Path
import struct
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("card_load_observer", Path(__file__).parents[1] / "Observe-SpyroCollectibleCardLoad.py")
model = importlib.util.module_from_spec(spec); spec.loader.exec_module(model)
PREFIX = bytes(0x1400)


class Memory:
    def __init__(self): self.data, self.reads, self.flip, self.short = {}, [], None, None
    def map(self, at, data): self.data.update((at + i, b) for i, b in enumerate(data))
    def word(self, at, value): self.map(at, struct.pack("<I", value))
    def read(self, at, size):
        self.reads.append((at, size))
        if self.flip == at and self.reads.count((at, size)) == 2: self.data[at] ^= 1
        result = bytes(self.data[at + i] for i in range(size))
        return result[:-1] if self.short == at else result


def memory(stage=2):
    m = Memory()
    for at, data in model.GATES.items(): m.map(at, data)
    values = {"bound": 1, "controller": 0, "slot": 0, "marker": 57527,
              "outer": 2, "substate": 5, "cursor_slot": 0, "intent": 1,
              "cursor_controller": 0, "menu_pointer": 0x80180000, "arena_end": 0x80182000,
              "busy": 0, "completion_latch": 0}
    for name, at in model.FIELDS.items(): m.word(at, values[name])
    state = bytearray(128); struct.pack_into("<6I", state, 0, stage, 1, 3, 0, 0, 0x80180000)
    struct.pack_into("<4I", state, 0x20, 0, 0, 57527, 1); m.map(model.STATE, state)
    m.map(model.SHADOW, model.expected_shadow(0, 57527, 1))
    m.map(model.READ_BUFFER, PREFIX); m.map(model.PREFIX, PREFIX)
    m.map(model.INPUT, model.fixture.envelope()[0]); m.map(0x801FF600, bytes(512))
    return m


def capture(m):
    with patch.object(model, "PREFIX_SHA", hashlib.sha256(PREFIX).hexdigest()): return model.capture(m, "fixture")


class CardLoadObserverTests(unittest.TestCase):
    def test_reference_shadow_matches_independent_csharp_codec(self):
        self.assertEqual(bytes(12), model.expected_shadow(0, 0, 1)[0x94:0xA0])
        self.assertEqual(3, model.expected_shadow(0, 0, 1)[0xA0])
        self.assertEqual("99547ff64dff5f65f4f65bf52a7e8b311febcc4c178932e96f66c55fe4b46e09",
                         hashlib.sha256(model.expected_shadow(0, 0, 1)).hexdigest())

    def test_prepared_observation_never_promotes_runtime_or_ready(self):
        m = memory(); result = capture(m)
        self.assertNotIn("sample_refused", result)
        self.assertTrue(result["prepared_fixture_observed"])
        for name in ("native_ready_issued", "collection_verified", "native_save_reload_verified", "runtime_pass"):
            self.assertIs(False, result[name])
        self.assertTrue(all(0 < n <= 1024 and 0x80000000 <= at <= 0x80200000 - n for at, n in m.reads))
        self.assertFalse(any(at == 0x80180000 for at, _ in m.reads))  # Never chase menu pointer.

    def test_pending_rejected_idle_do_not_read_or_report_prepared_shadow(self):
        for stage in (0, 1, 3):
            m = memory(stage); result = capture(m)
            self.assertNotIn("sample_refused", result)
            self.assertFalse(result["prepared_fixture_observed"])
            self.assertFalse(any(at == model.SHADOW for at, _ in m.reads))

    def test_gate_mutations_refused_before_state(self):
        for at in model.GATES:
            m = memory(); m.data[at] ^= 1; result = capture(m)
            self.assertIn("sample_refused", result)
            self.assertNotIn((model.STATE, 128), m.reads)

    def test_state_tuple_shadow_envelope_prefix_and_live_mutations_refuse(self):
        for at in (model.STATE, model.STATE + 8, model.STATE + 12, model.STATE + 16,
                   model.STATE + 32, model.STATE + 44, model.SHADOW + 0x16,
                   model.SHADOW + 0x1A, model.SHADOW + 0x90, model.SHADOW + 0x1FF,
                   model.INPUT + 96, model.PREFIX, model.READ_BUFFER, 0x801FF600,
                   model.FIELDS["bound"], model.FIELDS["marker"]):
            m = memory(); m.data[at] ^= 0x80
            with self.subTest(at=at):
                result = capture(m); self.assertIn("sample_refused", result)
                self.assertFalse(result["prepared_fixture_observed"])

    def test_sequential_changes_are_not_accepted(self):
        for at in (model.STATE, model.SHADOW, model.INPUT, model.FIELDS["bound"], 0x800755A4,
                   model.PREFIX, model.READ_BUFFER, 0x801FF600):
            m = memory(); m.flip = at; result = capture(m)
            self.assertIn("sample_refused", result)
            self.assertFalse(result["prepared_fixture_observed"])

    def test_native_buffer_bounds_and_consumed_latch_are_required(self):
        for value in (0, 0x80180001, 0x800C3234, 0x801FD604):
            m = memory(); m.word(model.STATE + 20, value); m.word(model.FIELDS["menu_pointer"], value)
            self.assertIn("sample_refused", capture(m))
        for name in ("arena_end", "completion_latch"):
            m = memory(); m.word(model.FIELDS[name], 1)
            self.assertIn("sample_refused", capture(m))

    def test_short_reads_refuse(self):
        for at in (model.STATE, model.SHADOW, model.INPUT, model.PREFIX):
            m = memory(); m.short = at
            self.assertIn("sample_refused", capture(m))

    def test_cli_requires_identity_before_connection(self):
        for args in ([], ["--connect-identified-test-session"],
                     ["--connect-identified-test-session", "--pid", "0", "--candidate-bin", "x"]):
            with patch.object(sys, "argv", ["observer", *args]), patch.object(model.socket, "create_connection") as connect:
                with self.assertRaises(SystemExit): model.main()
                connect.assert_not_called()


if __name__ == "__main__": unittest.main()
