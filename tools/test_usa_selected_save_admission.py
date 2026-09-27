"""Source-free instruction/refusal tests; exact original-image evidence is separate."""
import importlib.util
from pathlib import Path
import struct
import sys
import unittest

spec = importlib.util.spec_from_file_location("selected_save_admission", Path(__file__).with_name("Inspect-SpyroUsaSelectedSaveAdmission.py"))
model = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = model
spec.loader.exec_module(model)


def words(*items):
    return struct.pack(f"<{len(items)}I", *items)


class SelectedSaveAdmissionCases(unittest.TestCase):
    def test_exact_instruction_contract_records_pc_and_word(self):
        result = model.require_instructions(words(0xAC228D18, 0xAC238D1C), 0x8007CA84,
                                            ((0x8007CA84, 0xAC228D18), (0x8007CA88, 0xAC238D1C)))
        self.assertEqual([0x8007CA84, 0x8007CA88], [r["pc"] for r in result])
        self.assertEqual(["AC228D18", "AC238D1C"], [r["word"] for r in result])

    def test_instruction_change_is_refused(self):
        with self.assertRaises(ValueError):
            model.require_instructions(words(0xAC228D1C), 0x8007CA84, ((0x8007CA84, 0xAC228D18),))

    def test_mutable_truncated_and_unaligned_windows_refused(self):
        for data, base in ((bytearray(4), 0x8007CA84), (b"\0" * 3, 0x8007CA84), (bytes(4), 0x8007CA85)):
            with self.assertRaises(ValueError):
                model.require_instructions(data, base, ((0x8007CA84, 0),))

    def test_contract_outside_owner_refused(self):
        for pc in (0x8007CA80, 0x8007CA88, 0x8007CA85):
            with self.assertRaises(ValueError):
                model.require_instructions(bytes(4), 0x8007CA84, ((pc, 0),))

    def test_empty_or_duplicate_contract_refused(self):
        for contract in ((), ((0x8007CA84, 0), (0x8007CA84, 0))):
            with self.assertRaises(ValueError):
                model.require_instructions(bytes(4), 0x8007CA84, contract)

    def test_jal_and_jump_targets_keep_pc_address_domain(self):
        self.assertEqual(0x80059594, model.direct_target(0x0C016565, 0x8007CA34))
        self.assertEqual(0x8007CA80, model.direct_target(0x0801F2A0, 0x8007C7E0))

    def test_signed_conditional_branch_target(self):
        self.assertEqual(0x8007C798, model.branch_target(0x10400009, 0x8007C770))
        self.assertEqual(0x80010000, model.branch_target(0x1440FFFE, 0x80010004))

    def test_wrong_opcode_not_reported_as_branch_or_call(self):
        with self.assertRaises(ValueError):
            model.direct_target(0xAC228D18, 0x8007CA84)
        with self.assertRaises(ValueError):
            model.branch_target(0x0C016565, 0x8007CA34)

    def test_bad_word_and_pc_values_refused(self):
        for word, pc in ((True, 0x80010000), (-1, 0x80010000), (0x100000000, 0x80010000),
                         (0x0C016565, True), (0x0C016565, 0x80010001), (0x0C016565, 0x100000000)):
            with self.assertRaises(ValueError):
                model.direct_target(word, pc)

    def test_new_evidence_keeps_existing_owner_pins(self):
        self.assertEqual(30, len(model.OWNERSHIP.SPANS))
        self.assertEqual(8, len(model.SPANS))
        self.assertTrue(any(p[1] == "selected-slot-deserialize" for p in model.OWNERSHIP.SPANS))
        self.assertTrue(any(p[1] == "card-exit-transition" for p in model.OWNERSHIP.SPANS))

    def test_wrong_complete_source_refused_before_capture(self):
        with self.assertRaises(ValueError):
            model.capture(Path(__file__))

    def test_refusal_flags_never_issue_runtime_or_ready_authority(self):
        flags = model.evidence_limits()
        self.assertTrue(flags["staticSourceEvidenceOnly"])
        self.assertTrue(flags["legacyUnknownCorruptTailMustBePreservedOrRefused"])
        for key in ("mipsExecuted", "cardServicesExecuted", "readyAdmissionAuthorized", "initializerImplemented",
                    "fullIndirectDispatchClosed", "fullCardIoClosed", "writesCard", "writesImage", "publicationApproved"):
            self.assertFalse(flags[key], key)


if __name__ == "__main__":
    unittest.main(verbosity=2)
