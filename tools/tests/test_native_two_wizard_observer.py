"""Source-free two-owner observer bounds; no emulator connection or ROM writes."""
import importlib.util
import io
import re
import struct
import sys
from pathlib import Path
import unittest
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("two_wizard_observer", TOOLS / "Observe-SpyroNativeTwoWizards.py")
OBS = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = OBS
SPEC.loader.exec_module(OBS)


class Reader:
    def __init__(self):
        self.memory = {}
        self.reads = []
        self.mutate_after = None

    def map(self, at, data):
        self.memory.update((at + i, byte) for i, byte in enumerate(data))

    def word(self, at, value):
        self.map(at, struct.pack("<I", value))

    def read(self, at, size):
        self.reads.append((at, size))
        result = bytes(self.memory[at + i] for i in range(size))
        if self.mutate_after:
            self.mutate_after(self, at, size)
        return result


def fixture():
    r = Reader()
    values = {name: value for name, (_, _, value) in OBS.GATES.items()}
    values.update(dynamic_count=1, dynamic_properties_cursor=0x80140204,
                  next_free_moby_not_active_end=0x8013DB24,
                  primitive_cursor=0x80190000, primitive_limit=0x80191000)
    for name, (at, encoding) in OBS.FIELDS.items():
        count = len(struct.unpack(encoding, bytes(struct.calcsize(encoding))))
        r.map(at, struct.pack(encoding, *([values.get(name, 0)] * count)))
    for index, address, private in ((128, 0x8013DA1C, 0x801410B8), (129, 0x8013DA74, 0x80141108)):
        row = bytearray(88)
        struct.pack_into("<I", row, 0, private)
        struct.pack_into("<H", row, 0x36, 0x11B)
        row[0x53] = 0x56
        r.map(address, row)
        r.word(private, private + 0x28)
    rows = bytearray(89 * 88)
    rows[0x36] = 0x56
    rows[0x56] = 129
    rows[88 + 0x48] = 0xFF
    r.map(0x8013DACC, rows)
    pool = bytearray(0x2000)
    pool[1] = 0xFF
    r.map(0x801C3BB8, pool)
    r.map(0x801C5BB8, bytes([255] * 4))
    return r


class TwoWizardObserverTests(unittest.TestCase):
    def test_protocol_sends_only_bounded_read_and_ack(self):
        class Socket:
            def __init__(self):
                self.reply = bytearray(b"+" + OBS.one.rsp.frame_payload("01020304"))
                self.sent = []
            def recv(self, count):
                result = bytes(self.reply[:count]); del self.reply[:count]; return result
            def sendall(self, data): self.sent.append(data)
        sock = Socket()
        self.assertEqual(bytes([1, 2, 3, 4]), OBS.ReadOnlyClient(sock).read(0x800778C8, 4))
        self.assertTrue(re.fullmatch(rb"\$m800778c8,4#[0-9a-f]{2}", sock.sent[0]))
        self.assertEqual([b"+"], sock.sent[1:])
        for address, size in ((0x1F800024, 4), (0x801FFFFF, 4), (0x80000000, 0), (0x80000000, 1025), (True, 4)):
            sock = Socket()
            with self.assertRaises(ValueError): OBS.ReadOnlyClient(sock).read(address, size)
            self.assertEqual([], sock.sent)

    def test_cli_without_opt_in_or_with_unknown_option_never_connects(self):
        for arguments in ([], ["--profile", "anything"], ["--connect-identified-test-session", "--write"]):
            with patch.object(sys, "argv", ["observer"] + arguments), patch.object(OBS.socket, "create_connection") as connect:
                with patch.object(sys, "stderr", io.StringIO()), self.assertRaises(SystemExit) as stop:
                    OBS.main()
                self.assertEqual(2, stop.exception.code)
                connect.assert_not_called()

    def test_two_source_owners_and_dynamic_index_are_not_one_profile(self):
        r = fixture()
        sample = OBS.capture(r, "two-owner-synthetic")
        self.assertTrue(sample["payload_valid"])
        self.assertTrue(sample["gate_reread_consistent"])
        self.assertEqual(128, sample["wizard128"]["index"])
        self.assertEqual(129, sample["wizard129"]["index"])
        self.assertEqual(130, sample["dynamic_rows"]["active"][0]["index"])
        self.assertEqual(129, sample["dynamic_rows"]["active"][0]["native_reward_parent_index_u8"])
        self.assertEqual(0x801410E0, sample["wizard128_private_link"])
        self.assertEqual(0x80141130, sample["wizard129_private_link"])
        self.assertFalse(sample["runtime_pass"])
        self.assertFalse(sample["candidate_authenticated"])
        self.assertEqual(0xC00, sample["primitive_sampled_slack"])

    def test_every_wrong_gate_refuses_before_following_heap(self):
        for name, (at, encoding, expected) in OBS.GATES.items():
            with self.subTest(name=name):
                r = fixture()
                r.map(at, struct.pack(encoding, expected ^ 1))
                sample = OBS.capture(r, "wrong-gate")
                self.assertFalse(sample["payload_valid"])
                self.assertNotIn("dynamic_rows", sample)
                self.assertEqual(len(OBS.FIELDS), len(r.reads))

    def test_either_source_row_or_private_pointer_must_be_relocated(self):
        for index, address, private in ((128, 0x8013DA1C, 0x801410B8), (129, 0x8013DA74, 0x80141108)):
            for at, bad in ((address, 0), (address, private - 0x801321E4), (private, private - 4)):
                with self.subTest(index=index, at=at, bad=bad):
                    r = fixture()
                    r.word(at, bad)
                    sample = OBS.capture(r, "reload-or-bad-private")
                    self.assertFalse(sample["payload_valid"])
                    self.assertFalse(any(a == 0x8013DACC for a, _ in r.reads))
                    self.assertNotIn("particles", sample)

    def test_native_retired_rows_remain_observable_without_active_promotion(self):
        r = fixture()
        r.map(0x8013DA1C + 0x48, b"\xfd")
        r.map(0x8013DA74 + 0x48, b"\xfd")
        sample = OBS.capture(r, "retired-both")
        self.assertTrue(sample["payload_valid"])
        self.assertEqual([0xFD, 0], sample["wizard128"]["state_bytes"])

    def test_properties_count_and_next_free_rows_stay_inside89_slots(self):
        for name, bad in (("dynamic_count", 90), ("dynamic_properties_cursor", 0x8014021C - 90 * 24),
                          ("dynamic_properties_cursor", 0x8014021B),
                          ("next_free_moby_not_active_end", 0x8013DACC - 88),
                          ("next_free_moby_not_active_end", 0x8013DACC + 90 * 88),
                          ("next_free_moby_not_active_end", 0x8013DACD)):
            r = fixture()
            r.word(OBS.FIELDS[name][0], bad)
            self.assertFalse(OBS.capture(r, "bad-arena")["payload_valid"])

    def test_full_rows_bound_does_not_treat_next_free_as_active_end(self):
        r = fixture()
        r.map(0x8013DACC, bytes(89 * 88))
        sample = OBS.capture(r, "all-slots-visited")
        self.assertTrue(sample["payload_valid"])
        self.assertTrue(sample["dynamic_rows"]["no_in_range_terminal"])
        self.assertEqual(89, len(sample["dynamic_rows"]["active"]))
        self.assertEqual(218, sample["dynamic_rows"]["active"][-1]["index"])
        self.assertFalse(any(a == 0x8013DACC + 89 * 88 for a, _ in r.reads))
        self.assertTrue(sample["attention"])

    def test_changed_gate_or_row_invalidates_the_payload(self):
        for target in (0x80075890, 0x8013DA1C, 0x8013DA74, 0x80141108):
            r = fixture()
            def mutate(reader, at, size):
                if at == 0x801C5BB8:
                    reader.word(target, 0)
            r.mutate_after = mutate
            sample = OBS.capture(r, "changed-during-read")
            self.assertFalse(sample["payload_valid"])
            self.assertFalse(sample["gate_reread_consistent"])
            self.assertTrue(sample["attention"])

    def test_pool_and_independent_bitmap_reads_have_exact_owners(self):
        r = fixture()
        for at, value in ((0x800778C8, 1), (0x800778A8, 2), (0x80077958, 3)):
            r.word(at, value)
        sample = OBS.capture(r, "independent-bitmaps")
        self.assertEqual([1, 2, 3], [sample[n] for n in ("owners_live_word", "owners_checkpoint_word", "owners_durable_word")])
        self.assertTrue(all(1 <= size <= 1024 for _, size in r.reads))
        self.assertEqual(1, r.reads.count((0x801C5BB8, 4)))
        self.assertEqual(8, sum(size == 1024 for _, size in r.reads))

    def test_any_captured_owner_word_change_refuses_without_certification(self):
        for name in OBS.OWNERS:
            r = fixture()
            def mutate(reader, at, size):
                if at == OBS.PETE: reader.word(OBS.OWNERS[name][0], 1)
            r.mutate_after = mutate
            sample = OBS.capture(r, "owner-changed")
            self.assertFalse(sample["payload_valid"])
            self.assertFalse(sample["runtime_pass"])
            self.assertIn(name, sample["payload_refused"])

    def test_reclaimed_holes_and_parent_byte_do_not_alias_neighbor(self):
        r = fixture()
        rows = bytearray(6 * 88)
        for slot, state in enumerate((0xFD, 0, 0x80, 0xFE, 2, 0xFF)):
            rows[slot * 88 + 0x48] = state
            rows[slot * 88 + 0x36] = 0x56
            rows[slot * 88 + 0x56] = 128 if slot == 1 else 129
            rows[slot * 88 + 0x57] = 0xA5
        r.map(OBS.DYNAMIC, rows)
        sample = OBS.capture(r, "reused-holes")
        self.assertEqual([131, 134], [v["index"] for v in sample["dynamic_rows"]["active"]])
        self.assertEqual([128, 129], [v["native_reward_parent_index_u8"] for v in sample["dynamic_rows"]["active"]])
        self.assertEqual(5, sample["dynamic_rows"]["terminal_slot"])

    def test_unproved_class_byte_is_raw_not_a_reward_parent(self):
        r = fixture()
        r.map(OBS.DYNAMIC + 0x36, b"\x26\x00")
        r.map(OBS.DYNAMIC + 0x56, b"\xa5")
        sample = OBS.capture(r, "bolt-not-reward-parent")
        row = sample["dynamic_rows"]["active"][0]
        self.assertTrue(sample["payload_valid"])
        self.assertEqual(0xA5, row["raw_56_u8"])
        self.assertNotIn("native_parent_index_u8", row)
        self.assertNotIn("native_reward_parent_index_u8", row)

    def test_changed_native_terminal_owner_refuses(self):
        r = fixture()
        r.word(OBS.PETE, 0)
        sample = OBS.capture(r, "pete-terminal-changed")
        self.assertFalse(sample["payload_valid"])
        self.assertIn("terminal", sample["payload_refused"])
        self.assertTrue(sample["attention"])


if __name__ == "__main__":
    unittest.main()
