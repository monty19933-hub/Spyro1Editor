"""Source-free independent-verifier refusals. No disc, socket or emulator use."""
import importlib.util
import json
from pathlib import Path
import struct
import sys
from types import SimpleNamespace
import unittest
from unittest import mock

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


class AuthoredColorTests(unittest.TestCase):
    def package(self):
        p = bytearray(0x808)
        for i in range(70):
            v.put(p, 0x45C + i * 4, 0xFF123456)
            v.put(p, 0x574 + i * 4, 0x00654321)
        return p

    def level(self):
        data = bytearray(0x384000)
        for at, value in ((0x10, 0x17C000), (0x14, 0x4E800), (0x18, 0x1CA800),
                          (0x1C, 0x14800), (0x20, 0x1DF000), (0x24, 0x1000),
                          (0x50 + 35 * 4, 0x1C9800)):
            v.put(data, at, value)
        data[0x1C9800:0x1CA008] = self.package()
        rows = []
        for i in range(2):
            at = 0x100 + i * 88
            struct.pack_into("<H", data, 0x1CA800 + at + 0x36, 0xF5)
            rows.append({"NativeIndex": 174 + i, "RowSceneOffset": at, "PrivateSceneOffset": 0x300 + i * 12,
                         "Placement": {"Id": "egg-a" if i == 0 else "egg-b"}})
        return data, rows

    def test_palette_is_low24_only_and_preserves_complete_complement(self):
        p = self.package()
        final = v.authored_palette(p, 0x332211, 0xCCBBAA)
        allowed = {start + 4 * i + j for start in (0x45C, 0x574) for i in range(70) for j in range(3)}
        self.assertTrue(all(final[i] == p[i] for i in range(len(p)) if i not in allowed))
        self.assertEqual(final[0x45C:0x460], bytes.fromhex("112233ff"))
        self.assertEqual(final[0x574:0x578], bytes.fromhex("aabbcc00"))
        self.assertEqual(v.u32(p, 0x45C), 0xFF123456)
        self.assertEqual(final, v.authored_palette(bytes(p), 0x332211, 0xCCBBAA))

    def test_palette_rejects_wrong_width_length_and_code(self):
        p = self.package()
        for color in (-1, 0x1000000, True, "green"):
            with self.assertRaises(ValueError):
                v.authored_palette(p, color, 0)
        with self.assertRaises(ValueError):
            v.authored_palette(p[:-1], 0, 0)
        for at in (0x45F, 0x577):
            bad = bytearray(p); bad[at] ^= 1
            with self.assertRaises(ValueError):
                v.authored_palette(bad, 0, 0)

    def test_two_native_registrations_row_classes_and_full_inverse(self):
        data, rows = self.level()
        with mock.patch.object(v, "level_afterimage", return_value=(bytes(data), rows)):
            final, revised, bindings = v.authored_level_afterimage(b"", b"")
        self.assertEqual(len(final), len(data) + 0x1800)
        self.assertEqual(v.u32(final, 0x14), 0x50000)
        self.assertEqual(v.u32(final, 0x18), 0x1CC000)
        self.assertEqual(v.u32(final, 0x20), 0x1E0800)
        self.assertEqual([b["ProjectedRoot"] for b in bindings], [0x80164D54, 0x8016555C])
        for i, (cls, _, _) in enumerate(v.AUTHORED_COLORS):
            self.assertEqual(v.u32(final, 0xE0 + i * 4), 0x1CA800 + i * 0x808)
            self.assertEqual(struct.unpack_from("<H", final, 0x198 + i * 2)[0], cls)
            self.assertEqual(struct.unpack_from("<H", final, 0x1CC000 + rows[i]["RowSceneOffset"] + 0x36)[0], cls)
            self.assertEqual(revised[i]["AuthoredDestinationClass"], cls)
        self.assertEqual(final[0x800:0x1CA800], data[0x800:0x1CA800])

    def test_occupied_registration_and_wrong_row_ancestry_refused(self):
        for offset in (0xE0, 0xE4, 0x198, 0x19A, 0x1CA800 + 0x136):
            data, rows = self.level(); data[offset] ^= 1
            with mock.patch.object(v, "level_afterimage", return_value=(bytes(data), rows)), self.assertRaises(ValueError):
                v.authored_level_afterimage(b"", b"")

    def test_second_dispatch_leaf_is_owned_and_only_guard_changes(self):
        current = bytearray(0xE800)
        current[0x32C4:0x32CC] = v.words((v.jump(0x80085780), 0))
        at = 0x80088C44 - 0x8007AA38
        current[at:at + 0x1C] = v.words((0x2463FF0B, 0x10600003, 0x246300F5, v.jump(0x80085780), 0, v.jump(0x80088BAC), 0))
        with mock.patch.object(v, "overlay_afterimage", return_value=bytes(current)):
            final = v.authored_overlay_afterimage(b"", b"")
        self.assertEqual(v.u32(final, 0x32C4), v.jump(0x80088C44))
        allowed = set(range(0x32C4, 0x32C8)) | set(range(at, at + 0x34))
        self.assertTrue(all(final[i] == current[i] for i in range(len(current)) if i not in allowed))
        for offset in (0x32C4, 0x32C8, at + 0x20):
            bad = bytearray(current); bad[offset] ^= 1
            with mock.patch.object(v, "overlay_afterimage", return_value=bytes(bad)), self.assertRaises(ValueError):
                v.authored_overlay_afterimage(b"", b"")

    def test_authored_iso_owner_keeps_legacy_profile_closed(self):
        root = bytearray(2048)
        at = 0
        for name, lba, size in (("WAD.WAD", 37, 0x6927000), ("SCUS_942.28", 53875, 0x66000), ("NEXT", 55000, 2048)):
            row = iso_record(name, lba, size); root[at:at + len(row)] = row; at += len(row)
        records = v.iso_directory(root)
        final = v.root_afterimage(root, records, 0x692A000, True)
        self.assertEqual(v.iso_directory(final)["SCUS_942.28"][:2], (53881, 0x66000))
        for size, authored in ((0x692A000, False), (0x6928800, True), (0x692A000, 1)):
            with self.assertRaises(ValueError):
                v.root_afterimage(root, records, size, authored)

    def test_authored_receipt_rejects_old_gap_shared_palette_and_pickup_claims(self):
        report, _, receipt = ReceiptTests().fixture()
        data, rows = self.level()
        with mock.patch.object(v, "level_afterimage", return_value=(bytes(data), rows)):
            _, rows, bindings = v.authored_level_afterimage(b"", b"")
        receipt.update(profile=v.AUTHORED_PROFILE, GrowthBytes=0x3000, RelocatedExecutableLba=53881,
                       FinalExecutableEndLba=54085, Rows=rows, bindings=bindings, IndependentColorVariants=True,
                       sharedModelColors=False, memory={"SceneEnd": 0x8017AD54, "NormalReservedStart": 0x80187BB0,
                       "NormalGap": 0xCE5C, "TransferPeakEnd": 0x8017AD54, "VisualTickStackLow": 0x801FFB20, "StackMargin": 0x320})
        for field in ("LitPixelsVerified", "GenericEffectsApproved", "DynamicSpawnApproved", "GlobalUnusedNamespaceProven"):
            receipt[field] = False
        def check(value):
            return v.verify_receipt(json.dumps(value).encode(), report, [], rows, "cue", "source-cue", True, bindings)
        check(receipt)
        mutations = (("memory", "NormalGap", 0xE65C), ("bindings", "AuthoredDestinationClass", 0xF5),
                     ("bindings", "nearRgb", True), ("bindings", "PackageSha256", "wrong"))
        for owner, field, value in mutations:
            bad = json.loads(json.dumps(receipt))
            (bad[owner][0] if owner == "bindings" else bad[owner])[field] = value
            with self.assertRaises(ValueError): check(bad)
        for field, value in (("sharedModelColors", True), ("IndependentColorVariants", False),
                             ("PickupEnabled", True), ("PersistentSaveChanges", True), ("RuntimeVerified", True),
                             ("LitPixelsVerified", True), ("GenericEffectsApproved", True), ("DynamicSpawnApproved", True)):
            bad = dict(receipt); bad[field] = value
            with self.assertRaises(ValueError): check(bad)


if __name__ == "__main__":
    unittest.main()
