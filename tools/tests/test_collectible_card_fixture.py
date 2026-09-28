"""Synthetic, offline card fixtures; no access to the user's card."""
import hashlib
import importlib.util
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("card_fixture", Path(__file__).parents[1] / "Create-SpyroCollectibleCardFixture.py")
model = importlib.util.module_from_spec(spec)
spec.loader.exec_module(model)


def fix_xor(data, frame):
    at = frame * 128
    value = 0
    for b in data[at:at + 127]: value ^= b
    data[at + 127] = value


def card():
    data = bytearray([255]) * 131072
    data[:128] = bytes(128); data[:2] = b"MC"; fix_xor(data, 0)
    for i in range(1, 16):
        data[i * 128:(i + 1) * 128] = bytes(128)
        struct.pack_into("<IIH", data, i * 128, 0xA0, 0, 0xFFFF)
        fix_xor(data, i)
    struct.pack_into("<IIH", data, 128, 0x51, 0x2000, 0xFFFF)
    data[138:156] = b"BASCUS-94228SPYRO\0"; fix_xor(data, 1)
    data[0x2000:0x3400] = bytes(0x1400)
    data[0x2000:0x2004] = b"SC\x13\x01"
    data[0x2240] = 1
    struct.pack_into("<I", data, 0x2200 + 0x58C, 1)
    return data


class CardFixtureTests(unittest.TestCase):
    def test_changes_only_fixed_envelope_not_native_progress(self):
        original = bytes(card()); result, receipt = model.compose(original)
        self.assertEqual(original[:0x3400], result[:0x3400])
        self.assertEqual(original[0x3483:], result[0x3483:])
        self.assertEqual(b"SPYCSAV\0", result[0x3400:0x3408])
        self.assertEqual(bytes([1, 0, 0]), result[0x3460:0x3463])
        self.assertEqual(hashlib.sha256(result[0x3400:0x3463]).digest(), result[0x3463:0x3483])
        self.assertEqual(131072, len(result))
        self.assertTrue(receipt["native_prefix_preserved"])
        self.assertFalse(receipt["native_save_or_collection_proven"])

    def test_corrupt_card_header_directory_or_native_slot_refused(self):
        for offset in (0, 127, 128, 128 + 127, 0x2200, 0x2800 + 0x58C, 0x2E00 + 0x58C):
            data = card(); data[offset] ^= 1
            with self.subTest(offset=offset), self.assertRaises(ValueError): model.compose(bytes(data))

    def test_wrong_or_ambiguous_file_layout_refused(self):
        for offset, value in ((128 + 4, 0x4000), (128 + 8, 1), (256, 0x51)):
            data = card(); struct.pack_into("<I", data, offset, value)
            fix_xor(data, offset // 128)
            with self.subTest(offset=offset), self.assertRaises(ValueError): model.compose(bytes(data))

    def test_empty_slot_and_non_ff_tail_refused(self):
        data = card(); data[0x2240] = 0; struct.pack_into("<I", data, 0x2200 + 0x58C, 0)
        with self.assertRaises(ValueError): model.compose(bytes(data))
        for value in (0, 0x53):
            data = card(); data[0x3400] = value
            with self.assertRaises(ValueError): model.compose(bytes(data))

    def test_wrong_length_refused(self):
        for data in (bytes(card())[:-1], bytes(card()) + b"\0"):
            with self.assertRaises(ValueError): model.compose(data)

    def test_publication_refuses_unpinned_source_before_directory_creation(self):
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary); source = parent / "synthetic.mcd"
            source.write_bytes(card()); destination = parent / "new"
            with self.assertRaises(ValueError): model.publish(source, destination)
            self.assertFalse(destination.exists())
            self.assertEqual(card(), source.read_bytes())

    def test_new_directory_only_and_readback_with_synthetic_pin(self):
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary); source = parent / "synthetic.mcd"
            data = bytes(card()); source.write_bytes(data); destination = parent / "new"
            with patch.object(model, "SOURCE_SHA256", hashlib.sha256(data).hexdigest()):
                receipt = model.publish(source, destination)
                expected, _ = model.compose(data)
                self.assertEqual(expected, (destination / model.NAME).read_bytes())
                self.assertEqual(hashlib.sha256(expected).hexdigest(), receipt["fixture_sha256"])
                self.assertTrue((destination / "fixture-receipt.json").is_file())
                with self.assertRaises(FileExistsError): model.publish(source, destination)
                with self.assertRaises(FileExistsError): model.publish(source, source)
            self.assertEqual(data, source.read_bytes())

    def test_source_symlink_is_never_followed(self):
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary); source = parent / "synthetic.mcd"
            data = bytes(card()); source.write_bytes(data)
            link = parent / "link.mcd"; link.symlink_to(source)
            with patch.object(model, "SOURCE_SHA256", hashlib.sha256(data).hexdigest()):
                with self.assertRaises(OSError): model.publish(link, parent / "new")
            self.assertFalse((parent / "new").exists())
            self.assertEqual(data, source.read_bytes())


if __name__ == "__main__": unittest.main()
