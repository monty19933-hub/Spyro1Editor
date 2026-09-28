"""Offline immutable recipe and physical geometry checks; no image/card I/O."""
import importlib.util
import base64
from pathlib import Path
import struct
import sys
import unittest
import zlib
from unittest.mock import patch


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); sys.modules[name] = result
    spec.loader.exec_module(result)
    return result


model = load("admission_physical", Path(__file__).parents[1] / "Verify-SpyroCollectibleCardAdmission.py")
fixtures = load("admission_physical_fixtures", Path(__file__).with_name("test_collectible_card_load_physical.py"))


def executable():
    result = bytearray(fixtures.executable())
    stores = (0xAC205860, 0xAC205750, 0xAC205810, 0xAC20587C, 0xAC2056C8,
              0xAC205830, 0xAC2058E8, 0xAC22580C, 0xAC205838, 0xAC20583C)
    struct.pack_into("<I", result, 0x2EFC, 0x2402FFFF)
    for i, store in enumerate(stores): struct.pack_into("<II", result, 0x2F00 + i * 8, 0x3C018007, store)
    return bytes(result)


def overlay():
    result = bytearray(fixtures.overlay())
    struct.pack_into("<II", result, 0x174, 0x3C028008, 0x8C428D78)
    return bytes(result)


def synthetic_control():
    # Mechanical owner fixture only; these NOP bytes are never code evidence
    # and never reach the CLI's immutable production recipe.
    data = bytes(0x2000)
    return patch.multiple(model, CONTROL_SHA=model.sha(data), CONTROL_B85=base64.b85encode(zlib.compress(data)).decode(),
                          ENTRY_PC=0x80085238, BEGIN_PC=0x8008523C, GATE_PC=0x80085240)


class CardAdmissionPhysicalCases(unittest.TestCase):
    def test_reviewed_production_control_is_frozen_and_reader_append_stays_exact(self):
        before = overlay(); after = model.fixed_overlay(before)
        self.assertEqual("4cc3477d0415c3d881a37231d603b71325c6d9ce17ecd6b779948119bc687c2e", model.sha(after[0xA800:0xC800]))
        self.assertEqual((0x80085238, 0x800852EC, 0x80085580), (model.ENTRY_PC, model.BEGIN_PC, model.GATE_PC))
        self.assertEqual(model.reader.fixed_overlay(before)[0x3800:], after[0x3800:0xA800])
        self.assertEqual(before, model.inverse_overlay(before, after))

    def test_append_preserves_entire_reader_and_covers_successor_extent(self):
        cursor = 0x3800
        owners = model.append_layout()
        self.assertEqual(model.reader.append_layout(), owners[:-3])
        for _, at, size in owners:
            self.assertEqual(cursor, at); cursor += size
        self.assertEqual((0xD000, 0x80087A38), (cursor, model.reader.OVERLAY_BASE + cursor))

    def test_reset_patch_pinned_separately_from_generated_admission_code(self):
        raw = struct.pack("<20I", *model.RESET_WORDS)
        self.assertEqual("54c681de7e529f6595bbcc497e06d7821bfad3eb0ceaf0efeb4d3f185f352205", model.sha(raw))
        self.assertEqual(model.RESET_ORIGINAL_SHA, model.sha(executable()[0x2F00:0x2F50]))

    def test_executable_anchors_reset_and_complete_inverse(self):
        before = executable(); after = model.fixed_executable(before)
        for at in model.reader.ANCHOR_OFFSETS:
            self.assertEqual((0x3C028008, 0x24427A38), struct.unpack_from("<II", after, at))
        self.assertEqual(model.RESET_WORDS, struct.unpack_from("<20I", after, 0x2F00))
        self.assertEqual(before, model.inverse_executable(before, after))
        self.assertEqual(model.reader.fixed_executable(before)[0x65DA4:], after[0x65DA4:])

    def test_source_reset_store_or_preceding_v0_changes_refused(self):
        for at in (0x2EFC, *range(0x2F00, 0x2F50)):
            bad = bytearray(executable()); bad[at] ^= 1
            with self.assertRaises(ValueError): model.fixed_executable(bytes(bad))

    def test_every_reset_afterimage_byte_and_complement_change_refused(self):
        before = executable(); after = model.fixed_executable(before)
        for at in (0x1234, 0x65FFF, *range(0x2F00, 0x2F50)):
            bad = bytearray(after); bad[at] ^= 1
            with self.assertRaises(ValueError): model.inverse_executable(before, bytes(bad))

    def test_unreviewed_control_cannot_supply_an_overlay(self):
        with patch.object(model, "CONTROL_SHA", ""), self.assertRaisesRegex(ValueError, "not frozen"):
            model.fixed_overlay(overlay())

    def test_card_entry_beforeimage_change_refused(self):
        for at in range(0x174, 0x17C):
            bad = bytearray(overlay()); bad[at] ^= 1
            with self.assertRaisesRegex(ValueError, "entry source seam"):
                model.fixed_overlay(bytes(bad))

    def test_new_iso_geometry_and_complete_inverse(self):
        before = fixtures.root_directory()
        after = model.fixed_root(before, model.reader.WAD_SIZE + model.GROWTH)
        records = model.reader.iso_directory(after)
        self.assertEqual((53894, 0x66000), records["SCUS_942.28"][:2])
        self.assertEqual(54098, records["SCUS_942.28"][0] + 0xCC)
        self.assertEqual(before, model.inverse_root(before, after))

    def test_relocation_must_not_overlap_file_in_successor_only_gap(self):
        before = fixtures.root_directory([fixtures.iso_row("COLLISION;1", 54096, 2048)])
        model.reader.fixed_root(before, model.reader.WAD_SIZE + model.reader.GROWTH)
        with self.assertRaises(ValueError): model.fixed_root(before, model.reader.WAD_SIZE + model.GROWTH)

    def test_new_padding_inverse_checks_all_successor_only_sectors(self):
        for lba in range(54093, 54098):
            before = fixtures.raw_sector(lba, flags=32)
            self.assertEqual(before, model.inverse_sector(bytes(2352), lba, before, b"", b"", b"", 22))
            bad = bytearray(before); bad[24] = 1
            with self.assertRaises(ValueError): model.inverse_sector(bytes(2352), lba, bytes(bad), b"", b"", b"", 22)

    def test_distinct_cue_profiles_cannot_be_substituted(self):
        cue = lambda name: f'FILE "{name}" BINARY\n TRACK 01 MODE2/2352\n INDEX 01 00:00:00\n'.encode()
        model.require_cue(cue(model.IMAGE_NAME)); model.reader.require_cue(cue(model.reader.IMAGE_NAME))
        with self.assertRaises(ValueError): model.require_cue(cue(model.reader.IMAGE_NAME))
        with self.assertRaises(ValueError): model.reader.require_cue(cue(model.IMAGE_NAME))

    def test_reader_recipe_not_mutated_by_successor_calls(self):
        before = executable(); first = model.reader.fixed_executable(before)
        model.fixed_executable(before)
        self.assertEqual(first, model.reader.fixed_executable(before))
        self.assertEqual((0xA800, 0x7000), (model.reader.NEW_OVERLAY_SIZE, model.reader.GROWTH))

    def test_synthetic_overlay_complete_inverse_and_unchanged_reader_append(self):
        before = overlay()
        with synthetic_control():
            after = model.fixed_overlay(before)
            self.assertEqual(0xD000, len(after))
            self.assertEqual(model.reader.fixed_overlay(before)[0x3800:], after[0x3800:0xA800])
            self.assertEqual(before, model.inverse_overlay(before, after))
            self.assertEqual(bytes(0x800), after[0xC800:])

    def test_synthetic_overlay_refuses_complete_afterimage_mutations(self):
        before = overlay()
        with synthetic_control():
            after = model.fixed_overlay(before)
            for at in (0, 0x174, 0x178, 0x2060, 0x2064, 0x21C8, 0x21CC, 0x37FF,
                       0x3800, 0xA7FF, 0xA800, 0xC7FF, 0xC800, 0xC87F, 0xC880, 0xCFFF):
                bad = bytearray(after); bad[at] ^= 1
                with self.assertRaises(ValueError): model.inverse_overlay(before, bytes(bad))

    def test_synthetic_wad_only_grows_card_owner_and_rejects_unrelated_mutations(self):
        before = bytearray(fixtures.wad())
        at = model.reader.u32(before, 16); before[at:at + 0x3800] = overlay(); before = bytes(before)
        with synthetic_control():
            after, entries = model.packed_wad(before, model.fixed_overlay(overlay()))
            self.assertEqual(0x9800, len(after) - len(before))
            self.assertEqual(before, model.inverse_wad(before, after))
            self.assertTrue(all(e['originalSha256'] == e['finalSha256'] for e in entries if e['index'] != 2))
            for at in (0, 8, 0x808, len(after) - 1):
                bad = bytearray(after); bad[at] ^= 1
                with self.assertRaises(ValueError): model.inverse_wad(before, bytes(bad))

    def test_corrupt_control_and_unowned_entries_fail_before_construction(self):
        with synthetic_control():
            for name, value in (("CONTROL_SHA", "0" * 64), ("CONTROL_B85", "invalid"),
                                ("GATE_PC", 0x80087238), ("GATE_PC", True), ("ENTRY_PC", 0x80085239)):
                with patch.object(model, name, value), self.assertRaises(ValueError): model.fixed_overlay(overlay())


if __name__ == "__main__": unittest.main()
