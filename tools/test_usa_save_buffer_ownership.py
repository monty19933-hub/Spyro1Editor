"""Source-free refusal/footprint tests; optional exact source capture is separate."""
import importlib.util
from pathlib import Path
import struct
import sys
import unittest

spec = importlib.util.spec_from_file_location("save_owner", Path(__file__).with_name("Inspect-SpyroUsaSaveBufferOwnership.py"))
model = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = model
spec.loader.exec_module(model)


def words(*items):
    return struct.pack(f"<{len(items)}I", *items)


class SaveBufferOwnershipCases(unittest.TestCase):
    def test_explicit_menu_extent_and_unread_tail_are_distinct(self):
        g = model.workspace_geometry(0x80180000, 0x801B8000)
        self.assertEqual([0x8017E000, 0x80180000], g["menuWorkspace"])
        self.assertEqual(0x1400, g["menuNativePrefix"][1] - g["menuNativePrefix"][0])
        self.assertEqual(0xC00, g["menuUnreadTail"][1] - g["menuUnreadTail"][0])
        self.assertFalse(g["persistentLedgerOwnerEstablished"])

    def test_intro_staging_overlaps_entire_menu_workspace(self):
        g = model.workspace_geometry(0x80180000, 0x801B8000)
        a, b = g["earlierIntroCdStaging"], g["menuWorkspace"]
        self.assertEqual(0x2000, min(a[1], b[1]) - max(a[0], b[0]))
        self.assertEqual(0x600, g["gameSlotScratch"][1] - g["gameSlotScratch"][0])
        self.assertNotEqual(g["gameSlotScratch"], g["menuWorkspace"])

    def test_invalid_geometry_never_grants_owner(self):
        for a, b in ((True, 0x80100000), (0x8003FFFC, 0x80100000),
                     (0x80180001, 0x801B8000), (0x80180000, 0x80180000),
                     (0x80180000, 0x80200004)):
            with self.assertRaises(ValueError):
                model.workspace_geometry(a, b)

    def test_native_jal_delay_slot_changes_argument_before_call(self):
        code = words(0x24020600, 0xAFA20010, 0x24070200, 0x0C019C39, 0x24E70600)
        result = model.CallSetup({6: 0x80170000}).run_call(code, 0x80010000, model.WRITE)
        self.assertEqual((0x800, 0x600, 0x80170000), (result["fileOffset"], result["byteCount"], result["buffer"]))
        self.assertFalse(result["serviceExecuted"])

    def test_native_load_delay_does_not_make_next_instruction_see_new_word(self):
        code = words(0x8E020000, 0x00403821, 0x24020600, 0xAFA20010, 0x0C019B8A, 0)
        result = model.CallSetup({16: 0x80078000, 2: 0x200}, {0x80078000: 0xE00}).run_call(code, 0x80010000, model.READ)
        self.assertEqual(0x200, result["fileOffset"])

    def test_unknown_truncated_or_nonterminal_calls_refused(self):
        for code in (words(0x0C019B8A), words(0x0C019C39, 0), words(0x0C019B8A, 0, 0),
                     words(0xFFFFFFFF, 0), words(0, 0), bytearray(8)):
            with self.assertRaises(ValueError):
                model.CallSetup().run_call(code, 0x80010000, model.READ)

    def test_unowned_load_and_store_refused(self):
        for code in (words(0x8E020000, 0, 0x0C019B8A, 0), words(0xAE020000, 0, 0x0C019B8A, 0)):
            with self.assertRaises(ValueError):
                model.CallSetup({16: 0x80078000}).run_call(code, 0x80010000, model.READ)

    def test_delay_slot_load_not_accepted_as_consumed_argument(self):
        with self.assertRaises(ValueError):
            model.CallSetup({16: 0x80078000}, {0x80078000: 3}).run_call(words(0x0C019B8A, 0x8E070000), 0x80010000, model.READ)

    def test_direct_call_census_uses_whole_overlay_base(self):
        calls = model.direct_io_calls(words(0, 0x0C019B8A, 0x0C019C39, 0x08019B8A), 0x8007AA38, 0)
        self.assertEqual([0x8007AA3C, 0x8007AA40], [c["pc"] for c in calls])

    def test_wrong_complete_source_fails_before_inspection(self):
        with self.assertRaises(ValueError):
            model.capture(Path(__file__))


if __name__ == "__main__":
    unittest.main(verbosity=2)
