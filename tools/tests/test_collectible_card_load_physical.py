"""Offline physical adversarial fixtures; no BIN/CUE/card files are opened/written."""
import importlib.util
from pathlib import Path
import struct
import sys
from types import SimpleNamespace
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("card_load_physical", Path(__file__).parents[1] / "Verify-SpyroCollectibleCardLoad.py")
model = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = model
spec.loader.exec_module(model)


class MemoryImage:
    def __init__(self, data):
        self.data = bytes(data)
        self.initial = SimpleNamespace(st_size=len(data))

    def read(self, at, size):
        model.require(0 <= at <= at + size <= len(self.data), "Unowned fixture read")
        return self.data[at:at + size]


def executable():
    value = bytearray(model.EXE_SIZE)
    value[:8] = b"PS-X EXE"
    for at, word in ((0x10, 0x8005B8E0), (0x18, 0x80010000), (0x1C, 0x65800),
                     (0x30, 0x801FFFF0), (0x65DA4, 0x800)):
        struct.pack_into("<I", value, at, word)
    for at in model.ANCHOR_OFFSETS:
        struct.pack_into("<II", value, at, 0x3C028008, 0x2442DDE8)
    value[0x5550:0x5554] = b"KEEP"
    return bytes(value)


def overlay():
    value = bytearray(model.OLD_OVERLAY_SIZE)
    struct.pack_into("<I", value, 0x2060, 0x0C019D8A)
    struct.pack_into("<I", value, 0x21C8, 0x0C0198C7)
    value[-4:] = b"KEEP"
    return bytes(value)


def wad():
    header = bytearray(model.USER)
    payloads = [bytes([7]) * model.USER, overlay(), bytes([9]) * (2 * model.USER)]
    cursor = model.USER
    for index, payload in zip((0, 2, 5), payloads):
        struct.pack_into("<II", header, index * 8, cursor, len(payload))
        cursor += len(payload)
    return bytes(header) + b"".join(payloads)


def raw_sector(lba, payload=None, flags=8):
    raw = bytearray(model.RAW)
    raw[:12] = bytes.fromhex("00ffffffffffffffffffff00")
    minute, rest = divmod(lba + 150, 4500)
    second, frame = divmod(rest, 75)
    bcd = lambda v: (v // 10) * 16 + v % 10
    raw[12:16] = bytes((bcd(minute), bcd(second), bcd(frame), 2))
    raw[16:20] = raw[20:24] = bytes((0, 0, flags, 0))
    if flags == 32:
        struct.pack_into("<I", raw, 2348, model.oracle.edc(raw[16:2348]))
        return bytes(raw)
    raw[24:2072] = bytes(model.USER) if payload is None else payload
    return model.rebuild_sector(raw, lba)


def iso_row(name, lba, size, directory=False):
    encoded = name.encode("ascii")
    row = bytearray(34 + len(encoded) - len(encoded) % 2)
    row[0] = len(row)
    struct.pack_into("<I", row, 2, lba)
    struct.pack_into(">I", row, 6, lba)
    struct.pack_into("<I", row, 10, size)
    struct.pack_into(">I", row, 14, size)
    row[25] = 2 if directory else 0
    row[28:32] = b"\x01\x00\x00\x01"
    row[32] = len(encoded)
    row[33:33 + len(encoded)] = encoded
    return bytes(row)


def root_directory(extra=()):
    root = b"".join([iso_row("\0", 22, model.USER, True), iso_row("\1", 22, model.USER, True),
                    iso_row("WAD.WAD;1", model.WAD_LBA, model.WAD_SIZE),
                    iso_row("SCUS_942.28;1", model.EXE_LBA, model.EXE_SIZE),
                    iso_row("NEXT.DAT;1", 60000, 2048), *extra])
    return root + bytes(model.USER - len(root))


class CardLoadPhysicalCases(unittest.TestCase):
    def test_closed_layout_covers_complete_append_without_holes(self):
        owners = model.append_layout()
        cursor = model.OLD_OVERLAY_SIZE
        for _, at, size in owners:
            self.assertEqual(cursor, at)
            cursor += size
        self.assertEqual(0xA800, cursor)
        self.assertEqual(0x80085238, model.OVERLAY_BASE + cursor)
        self.assertEqual(("sector-padding", 0xA7C8, 0x38), owners[-1])

    def test_executable_three_anchors_bootstrap_and_complete_inverse(self):
        before = executable()
        after = model.fixed_executable(before)
        for at in model.ANCHOR_OFFSETS:
            self.assertEqual((0x3C028008, 0x24425238), struct.unpack_from("<II", after, at))
        self.assertEqual(0x80075640, model.u32(after, 0x10))
        self.assertEqual(0xA00, model.u32(after, 0x65DA4))
        self.assertEqual(model.STUB_WORDS, struct.unpack_from("<11I", after, 0x65E40))
        self.assertEqual(b"KEEP", after[0x5550:0x5554])
        self.assertEqual(bytes(0x194), after[0x65E6C:])
        self.assertEqual(before, model.inverse_executable(before, after))

    def test_executable_before_geometry_tail_and_anchor_mutations_refused(self):
        before = executable()
        for at in (0x10, 0x18, 0x1C, 0x30, 0x65DA4, 0x65E40, 0x65FFF, *model.ANCHOR_OFFSETS):
            bad = bytearray(before)
            bad[at] ^= 1
            with self.assertRaises(ValueError):
                model.fixed_executable(bytes(bad))
        for bad in (before[:-1], before + b"\0", bytearray(before)):
            with self.assertRaises(ValueError):
                model.fixed_executable(bad)

    def test_executable_afterimage_changes_inside_or_outside_patches_refused(self):
        before = executable()
        for at in (0x10, 0x65DA4, 0x65E40, 0x65FFF, 0x1234, *model.ANCHOR_OFFSETS):
            bad = bytearray(model.fixed_executable(before))
            bad[at] ^= 1
            with self.assertRaises(ValueError):
                model.inverse_executable(before, bytes(bad))

    def test_fixed_overlay_two_hooks_preserve_complete_native_prefix(self):
        before = overlay()
        after = model.fixed_overlay(before)
        self.assertEqual(model.NEW_OVERLAY_SIZE, len(after))
        self.assertEqual(model.jump(model.BEGIN_PC, True), model.u32(after, 0x2060))
        self.assertEqual(model.jump(model.GATE_PC), model.u32(after, 0x21C8))
        self.assertEqual(before, model.inverse_overlay(before, after))
        self.assertEqual(b"KEEP", after[0x37FC:0x3800])
        for name, at, size in model.append_layout():
            if name not in model.CODE_BLOCKS:
                self.assertEqual(bytes(size), after[at:at + size])

    def test_overlay_invalid_hook_delay_slot_extent_and_afterimage_refused(self):
        before = overlay()
        for at in (0x2060, 0x2064, 0x21C8, 0x21CC):
            bad = bytearray(before)
            bad[at] ^= 1
            with self.assertRaises(ValueError):
                model.fixed_overlay(bytes(bad))
        for at in (0x10, 0x2060, 0x37FF, 0x3800, 0x7008, 0xA7FF):
            bad = bytearray(model.fixed_overlay(before))
            bad[at] ^= 1
            with self.assertRaises(ValueError):
                model.inverse_overlay(before, bytes(bad))
        for bad in (before[:-1], before + b"\0", bytearray(before)):
            with self.assertRaises(ValueError):
                model.fixed_overlay(bad)

    def test_fixed_code_recipe_refuses_missing_swapped_corrupt_blocks_and_entries(self):
        original = dict(model.CODE_BLOCKS)
        missing = dict(original)
        del missing["sha"]
        swapped = dict(original, sha=original["loader"])
        digest, encoded = original["control"]
        wrong_hash = dict(original, control=("0" * 64, encoded))
        corrupt = dict(original, control=(digest, "invalid"))
        for bad in (missing, swapped, wrong_hash, corrupt):
            with patch.object(model, "CODE_BLOCKS", bad), self.assertRaises(ValueError):
                model.fixed_overlay(overlay())
        for pc in (None, True, 0x8007E238, 0x80080699, 0x80081A38):
            with patch.object(model, "GATE_PC", pc), self.assertRaises(ValueError):
                model.fixed_overlay(overlay())

    def test_packed_wad_only_entry_two_grows_and_inverse_preserves_all_other_bytes(self):
        before = wad()
        after, entries = model.packed_wad(before, model.fixed_overlay(overlay()))
        self.assertEqual(0x7000, len(after) - len(before))
        self.assertEqual(before, model.inverse_wad(before, after))
        self.assertEqual(3, len(entries))
        self.assertEqual(model.u32(before, 5 * 8) + 0x7000, model.u32(after, 5 * 8))
        self.assertEqual(before[-0x1000:], after[-0x1000:])

    def test_packed_wad_overlap_hole_unaligned_half_empty_and_omitted_owner_refused(self):
        before = wad()
        for field, value in ((16, 0x801), (16, 0x800), (16, 0x1800), (20, 0), (8, 0x800),
                             (5 * 8, model.u32(before, 5 * 8) + model.USER)):
            bad = bytearray(before)
            struct.pack_into("<I", bad, field, value)
            with self.assertRaises(ValueError):
                model.packed_wad(bytes(bad), model.fixed_overlay(overlay()))
        with self.assertRaises(ValueError):
            model.packed_wad(before + bytes(model.USER), model.fixed_overlay(overlay()))

    def test_wad_inverse_rejects_changed_unrelated_payload_header_or_overlay(self):
        before = wad()
        after, _ = model.packed_wad(before, model.fixed_overlay(overlay()))
        for at in (0, 8, 0x808, model.u32(after, 16) + 0x3800, len(after) - 1):
            bad = bytearray(after)
            bad[at] ^= 1
            with self.assertRaises(ValueError):
                model.inverse_wad(before, bytes(bad))

    def test_iso_relocation_is_derived_and_endian_duplicates_and_inverse_agree(self):
        before = root_directory()
        after = model.fixed_root(before, model.WAD_SIZE + 0x7000)
        records = model.iso_directory(after)
        self.assertEqual((53889, model.EXE_SIZE), records["SCUS_942.28"][:2])
        self.assertEqual((37, model.WAD_SIZE + 0x7000), records["WAD.WAD"][:2])
        self.assertEqual(before, model.inverse_root(before, after))

    def test_iso_duplicate_name_endian_tail_and_overlapping_other_file_refused(self):
        for root in (root_directory([iso_row("WAD.WAD;1", 37, model.WAD_SIZE)]),
                     root_directory([iso_row("OVERLAP;1", 54080, 2048)]),
                     root_directory([iso_row("INTERIOR;1", 500, 2048)])):
            with self.assertRaises(ValueError):
                model.fixed_root(root, model.WAD_SIZE + 0x7000)
        before = root_directory()
        record = model.iso_directory(before)["WAD.WAD"][2]
        for at in (record + 6, len(before) - 1):
            bad = bytearray(before)
            bad[at] ^= 1
            with self.assertRaises(ValueError):
                model.fixed_root(bytes(bad), model.WAD_SIZE + 0x7000)

    def test_padding_only_zero_valid_form_one_or_two_can_be_consumed(self):
        for flags in (8, 32):
            before = raw_sector(54079, flags=flags)
            result = model.prepare_padding(before, 54079)
            self.assertEqual(bytes(model.USER), result[24:2072])
            self.assertEqual(8, result[18])
            self.assertEqual(result, model.rebuild_sector(result, 54079))
            self.assertEqual(before[:16], result[:16])
        for offset in (0, 12, 16, 19, 20, 24, 2348):
            bad = bytearray(raw_sector(54079, flags=32))
            bad[offset] ^= 1
            with self.assertRaises(ValueError):
                model.prepare_padding(bytes(bad), 54079)

    def test_form_one_nonzero_padding_or_corrupt_parity_refused(self):
        for at in (24, 2072, 2076, 2248):
            bad = bytearray(raw_sector(54079))
            bad[at] ^= 1
            if at == 24:
                bad = model.rebuild_sector(bad, 54079)
            with self.assertRaises(ValueError):
                model.prepare_padding(bytes(bad), 54079)

    def test_relocated_raw_payloads_flags_parity_and_source_inverse(self):
        class SparseOwner:
            def __init__(self, size, fill):
                self.size, self.fill = size, fill
            def __len__(self):
                return self.size
            def __getitem__(self, key):
                return bytes((self.fill,)) * (key.stop - key.start)
        new_wad = SparseOwner(model.WAD_SIZE + model.GROWTH, 0x57)
        old_wad = SparseOwner(model.WAD_SIZE, 0x11)
        new_exe = bytes((0x45,)) * model.EXE_SIZE
        old_exe = bytes((0x22,)) * model.EXE_SIZE
        old_root, new_root = bytes((0x33,)) * model.USER, bytes((0x44,)) * model.USER
        for lba in (22, 37, 53874, 53875, 53888, 53889, 54078, 54079, 54092, 54093, 60000):
            if 54079 <= lba:
                before = raw_sector(lba, flags=32)
            else:
                payload = old_root if lba == 22 else old_wad[0:model.USER] if lba < 53875 else old_exe[:model.USER]
                before = raw_sector(lba, payload, 0x89 if lba in (53874, 54078) else 8)
            after = model.expected_sector(before, lba, new_wad, new_exe, new_root, 22, 0x81, 0x81)
            if lba in (54093, 60000):
                self.assertEqual(before, after)
            else:
                self.assertEqual(before[:16], after[:16])
                self.assertEqual(after, model.rebuild_sector(after, lba))
                self.assertEqual(0x89 if lba in (53888, 54092) else 8, after[18])
                self.assertEqual(new_root if lba == 22 else new_wad[0:model.USER] if lba < 53889 else new_exe[:model.USER],
                                 after[24:2072])
            self.assertEqual(before, model.inverse_sector(after, lba, before, old_wad, old_exe, old_root, 22))

    def test_relocation_authenticates_original_changed_sector_parity(self):
        wad = bytes(model.USER)
        root = bytes((1,)) * model.USER
        for at in (12, 20, 2072, 2076, 2248):
            bad = bytearray(raw_sector(22))
            bad[at] ^= 1
            with self.assertRaises(ValueError):
                model.expected_sector(bytes(bad), 22, wad, bytes(model.EXE_SIZE), root, 22, 0x81, 0x81)

    def test_raw_complement_and_full_inverse_are_streamed(self):
        before = b"".join(raw_sector(i, bytes((i,)) * model.USER) for i in range(4))
        def transform(raw, lba):
            if lba != 1:
                return raw
            result = bytearray(raw)
            result[30] ^= 3
            return model.rebuild_sector(result, lba)
        def inverse(raw, lba, source):
            if lba != 1:
                return raw
            result = bytearray(raw)
            result[30] ^= 3
            return model.rebuild_sector(result, lba)
        after = b"".join(transform(before[i * model.RAW:(i + 1) * model.RAW], i) for i in range(4))
        result = model.verify_raw(MemoryImage(before), MemoryImage(after), transform, inverse, model.sha(before))
        self.assertEqual(model.sha(after), result["expectedCandidateSha256"])
        self.assertEqual(model.sha(before), result["inverseOriginalSha256"])
        self.assertEqual(1, result["changedRawSectors"])
        for at in (12, 100, model.RAW + 30, model.RAW + 2072, model.RAW + 2248, len(after) - 1):
            bad = bytearray(after)
            bad[at] ^= 1
            with self.assertRaises(ValueError):
                model.verify_raw(MemoryImage(before), MemoryImage(bad), transform, inverse, model.sha(before))
        for bad in (after[:-1], after + b"\0"):
            with self.assertRaises(ValueError):
                model.verify_raw(MemoryImage(before), MemoryImage(bad), transform, inverse, model.sha(before))
        with self.assertRaises(ValueError):
            model.verify_raw(MemoryImage(before), MemoryImage(after), transform, lambda r, l, s: r, model.sha(before))
        with self.assertRaises(ValueError):
            model.verify_raw(MemoryImage(before), None, transform, inverse, "0" * 64)

    def test_fixed_cue_refuses_misbinding_path_escape_and_extra_tracks(self):
        cue = f'FILE "{model.IMAGE_NAME}" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n'.encode()
        model.require_cue(cue)
        for bad in (b"", b"\xff", cue + b"TRACK 02 AUDIO\n", cue.replace(b"MODE2/2352", b"MODE1/2048"),
                    cue.replace(b"00:00:00", b"00:02:00"), cue.replace(model.IMAGE_NAME.encode(), b"../bad.bin")):
            with self.assertRaises(ValueError):
                model.require_cue(bad)

    def test_explicit_inputs_required_before_open_and_candidate_name_closed(self):
        with patch.object(model, "ReadOnlyFile") as opened:
            for args in ((None,), ("unused.bin", None, "bad.cue"), ("unused.bin", "wrong.bin")):
                with self.assertRaises(ValueError):
                    model.verify(*args)
            opened.assert_not_called()

    def test_source_authority_rejects_wrong_whole_hash_or_extent_before_parsing(self):
        for size, digest in ((model.USA_SIZE - 1, model.USA_SHA), (model.USA_SIZE, "0" * 64)):
            with patch.object(model, "source_alias", return_value=("unused", lambda: None)), \
                 patch.object(model, "ReadOnlyFile") as opened, patch.object(model, "logical") as logical:
                held = opened.return_value.__enter__.return_value
                held.initial.st_size = size
                held.digest.return_value = digest
                with self.assertRaises(ValueError):
                    model.verify("unused")
                logical.assert_not_called()


if __name__ == "__main__":
    unittest.main(verbosity=2)
