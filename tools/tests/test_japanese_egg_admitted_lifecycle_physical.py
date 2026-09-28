"""Offline three-owner physical adversarial tests; no image/card writes."""
import base64
import importlib.util
from pathlib import Path
import struct
import sys
import unittest
from unittest.mock import patch
import zlib


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


model = load("admitted_egg_physical", Path(__file__).parents[1] / "Verify-SpyroJapaneseEggAdmittedLifecycle.py")
fixtures = load("admitted_egg_fixtures", Path(__file__).with_name("test_collectible_card_admission_physical.py"))
raw = fixtures.fixtures


def authored():
    data = bytearray(0xE800)
    for at, target in model.ARTISANS_HOOKS:
        struct.pack_into("<II", data, at, model.reader.jump(target), 0)
    data[0x5550:0x5554] = b"KEEP"
    return bytes(data)


def frozen():
    # NOP-filled mechanical ownership fixture, not native-code authority.
    blocks = (("fixture-code", 0xE800, 0x800, model.sha(bytes(0x800)),
               base64.b85encode(zlib.compress(bytes(0x800))).decode(), False),
              ("fixture-padding", 0xF000, 0x1000, model.sha(bytes(0x1000)), "", True))
    return patch.multiple(model, ARTISANS_SIZE=0x10000, BLOCKS=blocks,
                          BEFORE_PC=0x80089238, AFTER_PC=0x8008923C)


def executable():
    data = bytearray(fixtures.executable())
    struct.pack_into("<4I", data, 0x4AF18, 0x3C028009, 0x24428620, 0x3C018008, 0xAC2285D8)
    return bytes(data)


def wad():
    header = bytearray(0x800)
    entries = ((0, bytes([7])*0x800), (2, fixtures.overlay()), (9, bytes([9])*0xE000),
               (10, bytes([10])*0x1000), (11, bytes([11])*0x800))
    cursor = 0x800
    for index, payload in entries:
        struct.pack_into("<II", header, index*8, cursor, len(payload)); cursor += len(payload)
    return bytes(header) + b"".join(payload for _, payload in entries)


def replacements(original, jp_overlay, jp_data):
    rows = model.reader.wad_entries(original)
    data = {i: original[a:a+n] for a,n,i in rows}
    return {2: model.admission.fixed_overlay(data[2]), 9: model.append_authored(authored()),
            10: data[10] + bytes(0x2800)}


class AdmittedEggPhysicalCases(unittest.TestCase):
    def test_prepublication_source_compiled_append_is_frozen(self):
        blocks=model.frozen_blocks()
        self.assertEqual(9,len(blocks))
        self.assertEqual('d340afef978b0c8e30bf9a69a40129dd532103bd43c8e2341add87e42d103ad4',
                         model.sha(b''.join(data for _,_,data,_ in blocks)))
        self.assertEqual((0x8008A030,0x8008A060),(model.BEFORE_PC,model.AFTER_PC))
        self.assertEqual((0xFDF8,0x208,True),(blocks[-1][1],len(blocks[-1][2]),blocks[-1][3]))
        before=authored();after=model.append_authored(before)
        self.assertEqual(before,model.inverse_authored(before,after))

    def test_unfrozen_recipe_fails_closed(self):
        with patch.object(model, "BLOCKS", ()), self.assertRaisesRegex(ValueError, "not frozen"):
            model.append_authored(authored())

    def test_closed_append_and_two_hooks_preserve_authored_prefix(self):
        with frozen():
            before = authored(); after = model.append_authored(before)
            self.assertEqual(0x10000, len(after))
            self.assertEqual(before, model.inverse_authored(before, after))
            self.assertEqual(b"KEEP", after[0x5550:0x5554])
            self.assertEqual(bytes(0x1800), after[0xE800:])

    def test_hook_delays_original_extent_and_afterimage_complement_are_closed(self):
        with frozen():
            for at, _ in model.ARTISANS_HOOKS:
                for byte in range(at, at+8):
                    bad = bytearray(authored()); bad[byte] ^= 1
                    with self.assertRaises(ValueError): model.append_authored(bytes(bad))
            after = model.append_authored(authored())
            for at in (0, 0x5550, 0xE1AC, 0xE204, 0xE7FF, 0xE800, 0xEFFF, 0xF000, 0xFFFF):
                bad = bytearray(after); bad[at] ^= 1
                with self.assertRaises(ValueError): model.inverse_authored(authored(), bytes(bad))

    def test_blocks_refuse_overlap_gap_bad_hash_bad_encoding_and_unowned_entry(self):
        with frozen():
            valid = model.BLOCKS
            for blocks in (valid[::-1], valid[:-1], (valid[0], valid[0]),
                           ((valid[0][0], 0xE804, *valid[0][2:]), valid[1]),
                           ((*valid[0][:3], "0"*64, *valid[0][4:]), valid[1]),
                           ((*valid[0][:4], "broken", False), valid[1])):
                with patch.object(model, "BLOCKS", blocks), self.assertRaises(ValueError): model.append_authored(authored())
            for pc in (True, 0x80089239, 0x80089A38, 0x8007AA38):
                with patch.object(model, "BEFORE_PC", pc), self.assertRaises(ValueError): model.append_authored(authored())

    def test_exact_executable_merge_signed_low_and_inverse(self):
        with frozen():
            before = executable(); after = model.fixed_executable(before)
            self.assertEqual((0x3C028009, 0x2442AA38), struct.unpack_from("<II", after, 0x4AF18))
            self.assertEqual(before, model.inverse_executable(before, after))
            prior = model.admission.fixed_executable(before)
            self.assertEqual(prior[:0x4AF18], after[:0x4AF18]); self.assertEqual(prior[0x4AF20:], after[0x4AF20:])
            for at in (0, 0x10, 0x2F00, 0x4AF18, 0x4AF1F, 0x4AF20, 0x65FFF):
                bad = bytearray(after); bad[at] ^= 1
                with self.assertRaises(ValueError): model.inverse_executable(before, bytes(bad))

    def test_original_copy_anchor_owner_must_be_exact(self):
        with frozen():
            for at in range(0x4AF18, 0x4AF28):
                bad = bytearray(executable()); bad[at] ^= 1
                with self.assertRaises(ValueError): model.fixed_executable(bytes(bad))

    def test_three_owner_packing_and_complete_inverse(self):
        with frozen(), patch.object(model, "fixed_replacements", replacements), \
                patch.object(model.egg, "authored_overlay_afterimage", return_value=authored()):
            before = wad(); after, reports = model.packed_wad(before, b"jp-overlay", b"jp-data")
            self.assertEqual(0xE000, len(after)-len(before))
            self.assertEqual(before, model.inverse_wad(before, after, b"jp-overlay", b"jp-data"))
            self.assertEqual([2,9,10], [r['index'] for r in reports if r['originalSha256'] != r['finalSha256']])
            for at in (0, 16, 72, 80, 88, 0x800, len(after)-1):
                bad = bytearray(after); bad[at] ^= 1
                with self.assertRaises(ValueError): model.inverse_wad(before, bytes(bad), b"jp-overlay", b"jp-data")

    def test_source_wad_mapping_gap_alias_half_empty_or_missing_owner_refused(self):
        with frozen(), patch.object(model, "fixed_replacements", replacements):
            for at, value in ((16,0), (16,0x808), (72,0x800), (80,0), (88,0)):
                bad = bytearray(wad()); struct.pack_into("<I", bad, at, value)
                with self.assertRaises(ValueError): model.packed_wad(bytes(bad), b"", b"")

    def test_total_geometry_root_inverse_and_new_gap_collision(self):
        with frozen():
            before = raw.root_directory(); after = model.fixed_root(before, model.reader.WAD_SIZE+model.growth())
            self.assertEqual((53903,0x66000), model.reader.iso_directory(after)['SCUS_942.28'][:2])
            self.assertEqual(before, model.inverse_root(before, after))
            for lba in (54098,54106):
                collision = raw.root_directory([raw.iso_row('COLLISION;1',lba,2048)])
                model.admission.fixed_root(collision,model.reader.WAD_SIZE+0x9800)
                with self.assertRaises(ValueError): model.fixed_root(collision,model.reader.WAD_SIZE+model.growth())

    def test_successor_padding_and_boundary_eor_eof_and_parity(self):
        with frozen():
            wad_bytes = bytes(model.reader.WAD_SIZE + model.growth()); exe = bytes(model.reader.EXE_SIZE)
            for lba in range(54079,54107):
                before = raw.raw_sector(lba, flags=32)
                after = model.reader.expected_sector(before,lba,wad_bytes,exe,bytes(2048),22,0x81,0x81)
                self.assertEqual(0x89 if lba==54106 else 8, after[18])
                self.assertEqual(after,model.reader.rebuild_sector(after,lba))
                self.assertEqual(before,model.inverse_sector(after,lba,before,b"",b"",b"",22))
                bad = bytearray(before); bad[24]=1
                with self.assertRaises(ValueError): model.inverse_sector(after,lba,bytes(bad),b"",b"",b"",22)

    def test_distinct_cue_profile_rejects_predecessor_and_paths(self):
        cue=lambda n:f'FILE "{n}" BINARY\n TRACK 01 MODE2/2352\n INDEX 01 00:00:00\n'.encode()
        model.require_cue(cue(model.IMAGE_NAME))
        for name in (model.admission.IMAGE_NAME,model.egg.AUTHORED_IMAGE_NAME,'../'+model.IMAGE_NAME):
            with self.assertRaises(ValueError): model.require_cue(cue(name))

    def test_predecessor_recipes_are_not_mutated(self):
        before=executable(); card=fixtures.overlay()
        old_exe=model.admission.fixed_executable(before); old_card=model.admission.fixed_overlay(card)
        with frozen(): model.fixed_executable(before); model.append_authored(authored())
        self.assertEqual(old_exe,model.admission.fixed_executable(before))
        self.assertEqual(old_card,model.admission.fixed_overlay(card))
        self.assertEqual((0xD000,0x9800),(model.admission.NEW_OVERLAY_SIZE,model.admission.GROWTH))

    def test_complete_raw_prediction_inverse_and_hostile_candidate_complement(self):
        # Scaled placement, real raw-sector integrity and the actual28-sector
        # successor growth. Production constants are untouched outside this test.
        with frozen(), patch.multiple(model.reader,WAD_LBA=1,WAD_SIZE=3*2048,EXE_LBA=4,EXE_SIZE=2*2048):
            old_wad=bytes([7])*(3*2048); old_exe=bytes([9])*(2*2048); old_root=bytes([10])*2048
            sectors=[]
            for lba in range(36):
                payload=old_root if lba==0 else bytes([7])*2048 if lba<4 else bytes([9])*2048 if lba<6 else bytes([11])*2048
                sectors.append(raw.raw_sector(lba, payload=payload, flags=32 if 6<=lba<34 else 0x89 if lba in (3,5) else 8))
            source=raw.MemoryImage(b''.join(sectors)); new_wad=old_wad+bytes(model.growth()); new_root=bytes([12])*2048
            transform=lambda before,lba:model.reader.expected_sector(before,lba,new_wad,old_exe,new_root,0,0x81,0x81)
            inverse=lambda after,lba,before:model.inverse_sector(after,lba,before,old_wad,old_exe,old_root,0)
            predicted=b''.join(transform(before,lba) for lba,before in enumerate(sectors))
            report=model.reader.verify_raw(source,raw.MemoryImage(predicted),transform,inverse,model.sha(source.data))
            self.assertEqual(model.sha(predicted),report['expectedCandidateSha256'])
            self.assertEqual(model.sha(source.data),report['inverseOriginalSha256'])
            for lba,within in ((0,24),(1,24),(3,18),(5,18),(31,18),(32,24),(33,2348),(35,24)):
                bad=bytearray(predicted);bad[lba*2352+within]^=1
                with self.assertRaises(ValueError):
                    model.reader.verify_raw(source,raw.MemoryImage(bad),transform,inverse,model.sha(source.data))


if __name__ == '__main__': unittest.main()
