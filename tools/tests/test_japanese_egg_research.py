"""Source-free independent-verifier refusals. No disc, socket or emulator use."""
import importlib.util
import json
from pathlib import Path
import struct
import sys
from types import SimpleNamespace
import unittest

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("independent_egg_verifier", Path(__file__).parents[1] / "Verify-SpyroJapaneseEggResearch.py")
v = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = v
spec.loader.exec_module(v)


def scene_fixture(moby_length=0x200):
    scene = bytearray(0x800)
    at = 0x88
    for _ in range(7):
        v.put(scene, at, 8)
        at += 8
    moby = at
    v.put(scene, moby, moby_length)
    v.put(scene, moby + 4, 1)
    props = moby + moby_length
    v.put(scene, moby + 8, props + 4)
    v.put(scene, props, 16)
    scene[props + 4:props + 16] = b"originaldata"
    v.put(scene, props + 16, 8)
    v.put(scene, props + 24, 8)
    v.put(scene, props + 32, 1)
    v.put(scene, props + 36, moby + 8)
    row = bytearray(88)
    row[0x36:0x38] = b"\xf5\0"
    row[0x4F] = 1
    return scene, row


def packed_fixture():
    data = bytearray(4 * 2048)
    for i, entry in enumerate((9, 10, 11), 1):
        v.put(data, entry * 8, i * 2048)
        v.put(data, entry * 8 + 4, 2048)
        data[i * 2048:(i + 1) * 2048] = bytes([entry]) * 2048
    return data


def raw_fixture(lba, form2=False):
    raw = bytearray(2352)
    raw[:12] = bytes.fromhex("00ffffffffffffffffffff00")
    minute, remaining = divmod(lba + 150, 4500)
    second, frame = divmod(remaining, 75)
    bcd = lambda x: (x // 10) * 16 + x % 10
    raw[12:16] = bytes((bcd(minute), bcd(second), bcd(frame), 2))
    raw[18] = raw[22] = 32 if form2 else 8
    if form2:
        v.put(raw, 2348, v.oracle.edc(raw[16:2348]))
        return bytes(raw)
    return v.rebuild_sector(raw, lba)


class MemoryFile:
    def __init__(self, data):
        self.data = bytes(data)
        self.initial = SimpleNamespace(st_size=len(data))

    def read(self, offset, length):
        return self.data[offset:offset + length]


def iso_record(name, lba=37, length=2048):
    name = name.encode("ascii")
    row = bytearray(33 + len(name) + (len(name) % 2 == 0))
    row[0], row[32] = len(row), len(name)
    row[33:33 + len(name)] = name
    struct.pack_into("<I", row, 2, lba)
    struct.pack_into(">I", row, 6, lba)
    struct.pack_into("<I", row, 10, length)
    struct.pack_into(">I", row, 14, length)
    return row


class SceneTests(unittest.TestCase):
    def test_two_distinct_private_owners_and_preserved_original(self):
        scene, row = scene_fixture()
        result, reports = v.append_rows(scene, row, bytes(12))
        before, after = v.scene_layout(scene), v.scene_layout(result)
        self.assertEqual(after[2], 3)
        self.assertEqual(result[before[0] + 8:before[0] + 96], scene[before[0] + 8:before[0] + 96])
        self.assertEqual(after[6][:1], before[6])
        self.assertEqual(after[6][1:], [r["RowSceneOffset"] for r in reports])
        self.assertEqual(reports[1]["PrivateSceneOffset"] - reports[0]["PrivateSceneOffset"], 12)
        for index, report in enumerate(reports):
            at = report["RowSceneOffset"]
            self.assertEqual(v.u32(result, at), report["PrivateSceneOffset"])
            self.assertEqual(struct.unpack_from("<III", result, at + 12), v.POSITIONS[index][:3])
            self.assertEqual(result[at + 0x4F], 1)
            expected_c = 2896 if index == 0 else -2896
            self.assertEqual(struct.unpack_from("<9h", result, at + 0x20), (expected_c, 0, -2896, 0, 4096, 0, 2896, 0, expected_c))

    def test_unfixed_placement_rejected(self):
        scene, row = scene_fixture()
        for positions in (v.POSITIONS[::-1], v.POSITIONS[:1], ((1, 2, 3, 4), v.POSITIONS[1])):
            with self.assertRaises(ValueError):
                v.append_rows(scene, row, bytes(12), positions)

    def test_changed_donor_private_class_state_mode_rejected(self):
        scene, row = scene_fixture()
        for field in (0x36, 0x49, 0x4F):
            altered = bytearray(row); altered[field] ^= 1
            with self.assertRaises(ValueError):
                v.append_rows(scene, altered, bytes(12))
        for private in (bytes(11), bytes(13), b"x" + bytes(11)):
            with self.assertRaises(ValueError):
                v.append_rows(scene, row, private)

    def test_nonzero_dynamic_reservation_rejected(self):
        scene, row = scene_fixture(); scene[0xC8 + 88] = 1
        with self.assertRaisesRegex(ValueError, "reservation"):
            v.append_rows(scene, row, bytes(12))

    def test_dynamic_and_tail_capacity_rejected(self):
        scene, row = scene_fixture(96)
        with self.assertRaisesRegex(ValueError, "reservation"):
            v.append_rows(scene, row, bytes(12))
        scene, row = scene_fixture()
        with self.assertRaisesRegex(ValueError, "tail"):
            v.append_rows(scene[:v.scene_layout(scene)[5]], row, bytes(12))

    def test_nonzero_tail_rejected(self):
        scene, _ = scene_fixture(); scene[-1] = 1
        with self.assertRaisesRegex(ValueError, "tail"):
            v.scene_layout(scene)

    def test_duplicate_missing_and_unowned_fixups_rejected(self):
        for mutation in ("duplicate", "missing", "field", "target"):
            scene, _ = scene_fixture()
            moby, props, _, _, at, end, _ = v.scene_layout(scene)
            if mutation == "duplicate":
                v.put(scene, at, 2); v.put(scene, end, moby + 8)
            elif mutation == "missing":
                v.put(scene, at, 0); v.put(scene, at + 4, 0)
            elif mutation == "field":
                v.put(scene, at + 4, 3)
            else:
                v.put(scene, moby + 8, props + 16)
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                v.scene_layout(scene)

    def test_component_count_and_bounds_rejected(self):
        for value in (0, 3, 0xFFFFFFFF):
            scene, _ = scene_fixture(); v.put(scene, 0x88, value)
            with self.assertRaises(ValueError):
                v.scene_layout(scene)
        scene, _ = scene_fixture(); v.put(scene, 0xC4, 257)
        with self.assertRaises(ValueError):
            v.scene_layout(scene)


class PackedAndIsoTests(unittest.TestCase):
    def test_full_packed_inverse_and_untouched_entry(self):
        original = packed_fixture()
        final, entries = v.packed_wad(original, {9: bytes([19]) * 4096, 10: bytes([20]) * 4096})
        self.assertEqual(len(final), len(original) + 4096)
        self.assertEqual(final[v.u32(final, 88):], bytes([11]) * 2048)
        self.assertEqual([r["Index"] for r in entries], [9, 10, 11])
        self.assertEqual(entries[2]["OriginalSha256"], entries[2]["FinalSha256"])

    def test_packed_gap_overlap_and_partial_owner_rejected(self):
        for offset in (2048, 6144, 4097):
            original = packed_fixture(); v.put(original, 80, offset)
            with self.assertRaises(ValueError):
                v.packed_wad(original, {9: bytes(2048), 10: bytes(2048)})

    def test_packed_replacement_scope_and_alignment_rejected(self):
        for replacements in ({9: bytes(2048)}, {9: bytes(2048), 11: bytes(2048)}, {9: bytes(2047), 10: bytes(2048)}):
            with self.assertRaises(ValueError):
                v.packed_wad(packed_fixture(), replacements)

    def test_iso_dual_endian_unique_and_tail(self):
        record = iso_record("WAD.WAD;1")
        self.assertEqual(v.iso_directory(record + bytes(10))["WAD.WAD"][:2], (37, 2048))
        bad = bytearray(record); bad[6] ^= 1
        for data in (bad, record + record, record + b"\0\0x", bytes([1])):
            with self.assertRaises(ValueError):
                v.iso_directory(data)

    def test_source_authentication_refuses_arbitrary_payload(self):
        for action in (lambda: v.level_afterimage(bytes(2048), bytes(2048)),
                       lambda: v.overlay_afterimage(bytes(2048), bytes(2048)),
                       lambda: v.executable_afterimage(bytes(2048))):
            with self.assertRaises(ValueError):
                action()


class RawTests(unittest.TestCase):
    def test_independent_parity_and_owned_padding_conversion(self):
        for form2 in (False, True):
            source = raw_fixture(54079, form2)
            final = bytes(v.prepare_padding(source, 54079))
            self.assertEqual(final[18], 8)
            self.assertEqual(final, v.rebuild_sector(final, 54079))
            self.assertEqual(final[24:2072], bytes(2048))

    def test_padding_refuses_payload_metadata_edc_or_msf_changes(self):
        for form2 in (False, True):
            for offset in (24, 16, 20, 18, 2348 if form2 else 2072, 12):
                data = bytearray(raw_fixture(54079, form2)); data[offset] ^= 1
                with self.subTest(form2=form2, offset=offset), self.assertRaises(ValueError):
                    v.prepare_padding(data, 54079)

    def test_exact_full_raw_complement_and_hash(self):
        source = b"".join(raw_fixture(i) for i in range(4))
        replacement = bytearray(raw_fixture(1)); replacement[24] = 17
        replacement = v.rebuild_sector(replacement, 1)
        transform = lambda raw, lba: replacement if lba == 1 else raw
        candidate = source[:2352] + replacement + source[4704:]
        digest, sectors, changed = v.verify_complement(MemoryFile(source), MemoryFile(candidate), transform)
        self.assertEqual(digest, v.sha(candidate))
        self.assertEqual(sectors, 1)
        self.assertGreater(changed, 1)
        self.assertEqual(v.verify_complement(MemoryFile(source), None, transform), (digest, sectors, changed))

    def test_unplanned_payload_parity_metadata_and_audio_complement_fail(self):
        source = b"".join(raw_fixture(i) for i in range(4))
        for offset in (24, 18, 2072, len(source) - 1):
            candidate = bytearray(source); candidate[offset] ^= 1
            with self.assertRaises(ValueError):
                v.verify_complement(MemoryFile(source), MemoryFile(candidate), lambda raw, _: raw)

    def test_truncated_image_and_bad_transform_fail(self):
        source = raw_fixture(0)
        with self.assertRaises(ValueError):
            v.verify_complement(MemoryFile(source), MemoryFile(source[:-1]), lambda raw, _: raw)
        with self.assertRaises(ValueError):
            v.verify_complement(MemoryFile(source), None, lambda raw, _: raw[:-1])

    def test_flags_move_without_moving_physical_msf(self):
        wad, exe = bytes((53878 - 37) * 2048), bytes(0x66000)
        for lba, expected_flags in ((53874, 0), (53877, 0x81), (54078, 0), (54081, 1)):
            raw = raw_fixture(lba, lba == 54081)
            final = v.expected_sector(raw, lba, wad, exe, bytes(2048), 22, 0x81, 1)
            self.assertEqual(final[18] & 0x81, expected_flags)
            self.assertEqual(final[12:16], raw[12:16])
            self.assertEqual(final, v.rebuild_sector(final, lba))


class ReceiptTests(unittest.TestCase):
    def fixture(self):
        report = {key: "a" * 64 for key in ("expectedCandidateSha256", "originalWadSha256", "wadSha256", "rootSha256", "levelDataSha256", "overlaySha256", "executableSha256")}
        report["changedRawSectors"] = 123
        rows = [{"NativeIndex": 174}, {"NativeIndex": 175}]
        receipt = {"profile": v.PROFILE, "sourceUsaSha256": v.USA_SHA, "sourceJapanSha256": v.JP_SHA,
                   "outputImageSha256": report["expectedCandidateSha256"], "changedRawSectors": 123,
                   "outputCueSha256": "cue", "sourceCueSha256": "source-cue", "OriginalWadSha256": report["originalWadSha256"],
                   "WadSha256": report["wadSha256"], "RootSha256": report["rootSha256"], "GrowthBytes": 0x1800,
                   "RelocatedExecutableLba": 53878, "FinalExecutableEndLba": 54082, "Entries": [], "Rows": rows,
                   "LevelDataSha256": report["levelDataSha256"], "OverlaySha256": report["overlaySha256"],
                   "ExecutableSha256": report["executableSha256"], "opaqueRenderMode": 1, "ResearchOnly": True}
        for key in ("RuntimeVerified", "NormalCreateBinApproved", "CompleteNativeClosureProven", "PickupEnabled", "PersistentSaveChanges", "IndependentColorVariants", "TestLevelSelectorEnabled", "acceptedProjectHistory", "FullRuntimePeakProven"):
            receipt[key] = False
        return report, rows, receipt

    def test_descriptions_checked_but_positive_pass_flags_not_trusted(self):
        report, rows, receipt = self.fixture()
        receipt["wholeImagePhysicalReadback"] = False
        receipt["completeOriginalWadInverseVerified"] = False
        payload = json.dumps(receipt).encode()
        self.assertEqual(v.verify_receipt(payload, report, [], rows, "cue", "source-cue"), v.sha(payload))

    def test_mutated_identity_owner_or_promoted_authority_fails(self):
        for key, value in (("outputImageSha256", "wrong"), ("GrowthBytes", 0), ("Entries", [1]), ("Rows", []),
                           ("RuntimeVerified", True), ("opaqueRenderMode", True), ("sourceUsaSha256", v.JP_SHA)):
            report, rows, receipt = self.fixture(); receipt[key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                v.verify_receipt(json.dumps(receipt).encode(), report, [], rows, "cue", "source-cue")

    def test_duplicate_keys_including_nested_receipt_fail(self):
        for data in (b'{"x": 1, "x": 2}', b'{"nested": {"a": 1, "a": 2}}', b'[]'):
            with self.assertRaises(ValueError):
                v.unique_json(data)


if __name__ == "__main__":
    unittest.main()
