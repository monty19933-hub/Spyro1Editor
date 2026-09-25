"""Source-free regression for the neutral observed JP state/ordinary save models."""
import hashlib
import importlib.util
from pathlib import Path
import struct
import sys
import unittest

spec = importlib.util.spec_from_file_location("jp_collectibles", Path(__file__).with_name("Inspect-SpyroJapaneseCollectibles.py"))
model = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = model
spec.loader.exec_module(model)


class JapaneseCollectibleCases(unittest.TestCase):
    def test_usa_card_code_uses_whole_overlay_base_not_state_zero_target(self):
        # Native table's first handler is at file +0x6B0, NOT first code +0x9C.
        for offset, pc in ((0, 0x8007AA38), (0x9C, 0x8007AAD4),
                           (0x680, 0x8007B0B8), (0x6B0, 0x8007B0E8),
                           (0x1FFC, 0x8007CA34)):
            self.assertEqual(pc, model.usa_card_runtime_address(offset))
        for offset in (-4, 1, 0x3800, True):
            with self.assertRaises(ValueError):
                model.usa_card_runtime_address(offset)

    def test_thirty_level_owners_two_banks(self):
        levels = [world * 10 + level for world in range(1, 7) for level in range(5)]
        self.assertEqual(list(range(30)), [model.native_level_slot(level) for level in levels])
        indices = [model.native_egg_byte(bank, level) for bank in range(2) for level in levels]
        self.assertEqual(60, len(set(indices)))
        self.assertEqual([4, 33, 36, 65], [indices[0], indices[29], indices[30], indices[-1]])

    def test_no_unproved_flight_new_level_or_save_slot(self):
        for level in (-1, 0, 9, 15, 25, 55, 65, 70, 255, True):
            with self.subTest(level=level), self.assertRaises(ValueError):
                model.native_level_slot(level)
        for bank in (-1, 2, 3, True):
            with self.subTest(bank=bank), self.assertRaises(ValueError):
                model.native_egg_byte(bank, 10)

    def test_nonzero_unknown_native_state_is_preserved_and_retired(self):
        data = bytearray(512)
        for state in (0, 1, 105, 2, 255):
            data[4] = state
            snapshot = model.DragonflyAuxiliarySnapshot(bytes(data))
            self.assertEqual(state != 0, snapshot.collection_suppressed(0, 10, True))
            self.assertTrue(snapshot.collection_suppressed(0, 10, False))
            self.assertEqual(state, snapshot.state(0, 10))

    def test_normalization_changes_only_pending_egg_bytes(self):
        data = bytes([105] * 4 + list(range(64)) + [105] * 444)
        changed = bytearray(data)
        changed[4] = changed[67] = 105
        snapshot = model.DragonflyAuxiliarySnapshot(bytes(changed))
        result = snapshot.normalized_for_observed_write()
        expected = changed.copy()
        expected[4] = expected[67] = 1
        self.assertEqual(bytes(expected), result.data)
        self.assertEqual(bytes(changed), snapshot.data)
        self.assertEqual(result, result.normalized_for_observed_write())

    def test_merge_is_pending_only_and_never_overwrites_loaded_state(self):
        data = bytearray([0xC7] * 512)
        data[4:68] = bytes([0, 1, 2, 105] * 16)
        old = bytes([105] * 64)
        result = model.DragonflyAuxiliarySnapshot(bytes(data)).merge_observed_pending(old)
        self.assertEqual(bytes([105, 1, 2, 105] * 16), result.data[4:68])
        self.assertEqual(data[:4], result.data[:4])
        self.assertEqual(data[68:], result.data[68:])
        zero = model.DragonflyAuxiliarySnapshot(bytes(512))
        self.assertEqual(zero, zero.merge_observed_pending(bytes([1, 2, 255, 0] * 16)))

    def test_exact_opaque_owner_and_immutable_input(self):
        for data in (b"", bytes(511), bytes(513), bytearray(512)):
            with self.assertRaises(ValueError):
                model.DragonflyAuxiliarySnapshot(data)
        snapshot = model.DragonflyAuxiliarySnapshot(bytes(512))
        for data in (bytes(63), bytes(65), bytearray(64)):
            with self.assertRaises(ValueError):
                snapshot.merge_observed_pending(data)

    def test_ordinary_percentage_goldens_and_bonus(self):
        for counters, expected in (((0, 0, 0), 0), ((12000, 80, 12), 100), ((14000, 80, 12), 120),
                                   ((12000, 0, 0), 50), ((0, 80, 0), 40), ((0, 0, 12), 10)):
            self.assertEqual(expected, model.ordinary_percent(*counters))
        with self.assertRaises(TypeError):
            model.ordinary_percent(0, 0, 0, 30)

    def test_ordinary_slot_only_eighteen_egg_bytes_count(self):
        data = bytearray(0x600)
        data[0x88] = 80
        struct.pack_into("<h", data, 0xAC, 12000)
        data[0xF4] = 12
        data[0x106] = 105  # Following field is not an ordinary egg owner.
        struct.pack_into("<I", data, 0x58C, sum(data[:0x58C]))
        report = model.inspect_ordinary_save(bytes(data))
        self.assertEqual((80, 12000, 12, 100), tuple(report[k] for k in ("dragons", "gems", "ordinaryEggs", "ordinaryPercent")))
        self.assertTrue(report["checksumMatches"])
        data[0x590:] = bytes([105] * 0x70)
        self.assertTrue(model.inspect_ordinary_save(bytes(data))["checksumMatches"])
        data[0x10C] ^= 1
        self.assertFalse(model.inspect_ordinary_save(bytes(data))["checksumMatches"])

    def test_checksum_is_exact_prefix_sum_not_whole_slot(self):
        data = bytes([255] * 0x600)
        self.assertEqual(0x58C * 255, model.ordinary_save_checksum(data))
        for data in (bytes(0x58C), bytes(0x601)):
            with self.assertRaises(ValueError):
                model.ordinary_save_checksum(data)

    def test_source_instruction_span_identity_and_bounds(self):
        data = struct.pack("<II", 0x24020069, 0xA02213EC)
        digest = hashlib.sha256(data).hexdigest()
        witness = model.describe_span(data, "fixture", 0, 8, 0x80010000, digest)
        self.assertEqual(["24020069", "A02213EC"], witness["words"])
        for offset, length, expected in ((1, 4, None), (0, 12, None), (0, 0, None), (0, 8, "0" * 64)):
            with self.assertRaises(ValueError):
                model.describe_span(data, "fixture", offset, length, 0x80010000, expected)

    def test_wrong_source_refuses_before_reads(self):
        with self.assertRaises(ValueError):
            model.AuthenticatedImage(Path(__file__), model.JP)


if __name__ == "__main__":
    unittest.main(verbosity=2)
