"""Source-free, in-memory fault tests; no BIN/CUE files are written or opened."""
import hashlib
import importlib.util
from pathlib import Path
import struct
import sys
from types import SimpleNamespace
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("collectible_startup", Path(__file__).parents[1] / "Verify-SpyroCollectibleStartup.py")
model = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = model
spec.loader.exec_module(model)


class MemoryImage:
    def __init__(self, data):
        self.data = bytes(data)
        self.initial = SimpleNamespace(st_size=len(data))

    def read(self, offset, length):
        if offset < 0 or length < 0 or offset + length > len(self.data):
            raise ValueError("Unowned fixture read")
        return self.data[offset:offset + length]


def executable():
    result = bytearray(model.EXE_SIZE)
    result[:8] = b"PS-X EXE"
    for offset, value in ((0x10, 0x8005B8E0), (0x18, 0x80010000), (0x1C, 0x65800),
                          (0x30, 0x801FFFF0), (0x65DA4, 0x800)):
        struct.pack_into("<I", result, offset, value)
    result[0x1234:0x1238] = b"KEEP"
    return bytes(result)


def sector(lba, payload=None, flags=0x08):
    raw = bytearray(model.RAW)
    raw[:12] = bytes.fromhex("00ffffffffffffffffffff00")
    minute, remainder = divmod(lba + 150, 4500)
    second, frame = divmod(remainder, 75)
    bcd = lambda value: (value // 10) * 16 + value % 10
    raw[12:16] = bytes((bcd(minute), bcd(second), bcd(frame), 2))
    raw[16:20] = raw[20:24] = bytes((0, 0, flags, 0))
    raw[24:2072] = bytes(model.USER) if payload is None else payload
    return model.rebuild_sector(raw, lba)


def physical_fixture():
    before = b"".join(sector(lba, bytes((lba,)) * model.USER) for lba in range(4))
    replacements = {}
    for lba in (0, 3):
        raw = bytearray(before[lba * model.RAW:(lba + 1) * model.RAW])
        raw[48] ^= 0x55
        replacements[lba] = model.rebuild_sector(raw, lba)
    after = bytearray(before)
    for lba, raw in replacements.items():
        after[lba * model.RAW:(lba + 1) * model.RAW] = raw
    return before, bytes(after), replacements


class CollectibleStartupPhysicalCases(unittest.TestCase):
    def test_fixed_executable_edits_and_full_inverse(self):
        before = executable()
        after = model.fixed_executable(before)
        self.assertEqual(0x80075640, struct.unpack_from("<I", after, 0x10)[0])
        self.assertEqual(0xA00, struct.unpack_from("<I", after, 0x65DA4)[0])
        self.assertEqual((0x3C02801F, 0x3442F600, 0x3C03801F, 0x3463F800, 0xAC400000,
                          0x24420004, 0x0043082B, 0x1420FFFC, 0, 0x08016E38, 0),
                         struct.unpack_from("<11I", after, 0x65E40))
        inverse = bytearray(after)
        for offset, size in model.EDIT_EXTENTS:
            inverse[offset:offset + size] = before[offset:offset + size]
        self.assertEqual(before, bytes(inverse))
        self.assertEqual(b"KEEP", after[0x1234:0x1238])
        self.assertFalse(any(after[0x65E6C:]))

    def test_wrong_before_fields_and_unpaired_reserve_refused(self):
        for offset, value in ((0x10, 0x80075640), (0x65DA4, 0xA00), (0x65DA4, 0x600),
                              (0x18, 0x80010004), (0x1C, 0x65600), (0x30, 0x801FF000)):
            bad = bytearray(executable())
            struct.pack_into("<I", bad, offset, value)
            with self.assertRaises(ValueError):
                model.fixed_executable(bytes(bad))

    def test_nonzero_tail_and_wrong_executable_extent_refused(self):
        bad = bytearray(executable())
        bad[-1] = 1
        for data in (bytes(bad), executable()[:-1], executable() + b"\0", bytearray(executable())):
            with self.assertRaises(ValueError):
                model.fixed_executable(data)

    def test_two_fixed_raw_sectors_preserve_header_and_subheaders(self):
        before, after = executable(), model.fixed_executable(executable())
        self.assertEqual((53875, 54078), model.TOUCHED_LBAS)
        for lba in model.TOUCHED_LBAS:
            offset = (lba - model.EXE_LBA) * model.USER
            raw = sector(lba, before[offset:offset + model.USER], 0x89)
            final = model.executable_sector(raw, lba, before, after)
            self.assertEqual(raw[:24], final[:24])
            self.assertEqual(after[offset:offset + model.USER], final[24:2072])
            self.assertEqual(final, model.rebuild_sector(final, lba))
            inverse = bytearray(final)
            inverse[24:2072] = raw[24:2072]
            self.assertEqual(raw, model.rebuild_sector(inverse, lba))

    def test_corrupt_original_sector_parity_refused(self):
        before, after = executable(), model.fixed_executable(executable())
        raw = bytearray(sector(model.EXE_LBA, before[:model.USER]))
        raw[2248] ^= 1
        with self.assertRaises(ValueError):
            model.executable_sector(bytes(raw), model.EXE_LBA, before, after)

    def test_wrong_raw_header_or_unowned_sector_refused(self):
        before, after = executable(), model.fixed_executable(executable())
        raw = sector(model.EXE_LBA, before[:model.USER])
        for offset in (0, 12, 15, 20):
            bad = bytearray(raw)
            bad[offset] ^= 1
            with self.assertRaises(ValueError):
                model.executable_sector(bytes(bad), model.EXE_LBA, before, after)
        with self.assertRaises(ValueError):
            model.executable_sector(raw, model.EXE_LBA + 1, before, after)

    def test_unexpected_executable_afterimage_refused(self):
        before = executable()
        after = bytearray(model.fixed_executable(before))
        after[0x1234] ^= 1
        with self.assertRaises(ValueError):
            model.executable_sector(sector(model.EXE_LBA, before[:model.USER]), model.EXE_LBA, before, bytes(after))

    def test_complete_physical_complement_and_inverse(self):
        before, after, replacements = physical_fixture()
        result = model.verify_raw_complement(MemoryImage(before), MemoryImage(after), replacements, model.sha(before))
        self.assertEqual(model.sha(after), result["expectedCandidateSha256"])
        self.assertEqual(model.sha(before), result["inverseOriginalSha256"])
        self.assertEqual([0, 3], result["changedLbas"])
        self.assertTrue(result["candidateVerified"])

    def test_prediction_uses_only_original_and_no_candidate(self):
        before, after, replacements = physical_fixture()
        result = model.verify_raw_complement(MemoryImage(before), None, replacements, model.sha(before))
        self.assertEqual(model.sha(after), result["expectedCandidateSha256"])
        self.assertFalse(result["candidateVerified"])

    def test_mutation_of_payload_parity_header_and_whole_complement_refused(self):
        before, after, replacements = physical_fixture()
        for offset in (24, 2072, 2248, 12, model.RAW + 80, 2 * model.RAW + 2351, len(after) - 1):
            bad = bytearray(after)
            bad[offset] ^= 1
            with self.assertRaises(ValueError):
                model.verify_raw_complement(MemoryImage(before), MemoryImage(bad), replacements, model.sha(before))

    def test_candidate_truncation_extension_and_wrong_source_hash_refused(self):
        before, after, replacements = physical_fixture()
        for bad in (after[:-1], after + b"\0"):
            with self.assertRaises(ValueError):
                model.verify_raw_complement(MemoryImage(before), MemoryImage(bad), replacements, model.sha(before))
        with self.assertRaises(ValueError):
            model.verify_raw_complement(MemoryImage(before), MemoryImage(after), replacements, "0" * 64)

    def test_unowned_or_malformed_replacement_map_refused(self):
        before, after, replacements = physical_fixture()
        for bad in ({}, {4: bytes(model.RAW)}, {0: bytes(model.RAW - 1)}, {True: bytes(model.RAW)}):
            with self.assertRaises(ValueError):
                model.verify_raw_complement(MemoryImage(before), MemoryImage(after), bad, model.sha(before))

    def test_optional_cue_binds_only_exact_single_track_name(self):
        cue = f'FILE "{model.IMAGE_NAME}" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n'.encode()
        model.require_cue(cue)
        for bad in (cue.replace(model.IMAGE_NAME.encode(), b"original.bin"), cue + b"TRACK 02 AUDIO\n",
                    cue.replace(b"00:00:00", b"00:02:00"), cue.replace(b"MODE2/2352", b"MODE1/2048"),
                    cue.replace(model.IMAGE_NAME.encode(), b"../" + model.IMAGE_NAME.encode()), b"", b"\xff"):
            with self.assertRaises(ValueError):
                model.require_cue(bad)

    def test_no_implicit_open_or_cue_without_candidate(self):
        with patch.object(model, "ReadOnlyFile") as opened:
            with self.assertRaises(ValueError):
                model.verify(None)
            with self.assertRaises(ValueError):
                model.verify("unused-original.bin", cue="unexpected.cue")
            opened.assert_not_called()


if __name__ == "__main__":
    unittest.main(verbosity=2)
