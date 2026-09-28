"""Source-free numeric contracts only; source capture and service proof differ."""
import importlib.util
from pathlib import Path
import sys
import unittest

spec = importlib.util.spec_from_file_location("card_read_completion", Path(__file__).with_name("Inspect-SpyroUsaCardReadCompletion.py"))
model = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = model
spec.loader.exec_module(model)


class CardReadCompletionCases(unittest.TestCase):
    def test_idle_is_minus_one_and_leaves_outputs_untouched(self):
        result = model.poll_snapshot(1, 0, 0, 0, 3, 5)
        self.assertEqual(-1, result["returnValue"])
        self.assertIsNone(result["operation"])
        self.assertIsNone(result["result"])

    def test_pending_nonblocking_reports_current_operation_not_stale_completion(self):
        result = model.poll_snapshot(1, 3, 2, 0, 4, 5)
        self.assertEqual((0, 3, 2), tuple(result[k] for k in ("returnValue", "operation", "result")))

    def test_blocking_mode_reports_unbounded_wait_without_simulating_it(self):
        result = model.poll_snapshot(0, 3, 0, 0, 3, 0)
        self.assertTrue(result["wouldBlock"])
        self.assertIsNone(result["returnValue"])

    def test_completed_failure_still_returns_one_and_consumes_latch(self):
        result = model.poll_snapshot(1, 0, 0, 1, 3, 5)
        self.assertEqual((1, 3, 5, 0), tuple(result[k] for k in ("returnValue", "operation", "result", "completionAfter")))

    def test_completion_is_consumed_once(self):
        first = model.poll_snapshot(1, 0, 0, 1, 3, 0)
        second = model.poll_snapshot(1, 0, 0, first["completionAfter"], 3, 0)
        self.assertEqual(1, first["returnValue"])
        self.assertEqual(-1, second["returnValue"])

    def test_successful_other_operation_does_not_become_read(self):
        self.assertEqual(4, model.poll_snapshot(1, 0, 0, 1, 4, 0)["operation"])

    def test_any_nonzero_mode_selects_nonblocking_path(self):
        self.assertEqual(0, model.poll_snapshot(0xFFFFFFFF, 3, 0, 0, 0, 0)["returnValue"])

    def test_native_read_rejects_busy_and_unaligned_counts(self):
        self.assertFalse(model.native_read_accepts(3, 0xC00))
        self.assertFalse(model.native_read_accepts(0, 0xC01))
        self.assertTrue(model.native_read_accepts(0, 0xC00))

    def test_native_read_does_not_check_positive_length(self):
        self.assertTrue(model.native_read_accepts(0, 0))
        self.assertTrue(model.native_read_accepts(0, 0xFFFFFF80))

    def test_result_mapping_and_unknown_preservation(self):
        self.assertEqual([0, 2, 1, 0x8003, 3, 0x8008], [model.mapped_event_result(n) for n in (0, 1, 2, 3, 4, 8)])

    def test_bad_scalar_fixtures_refused(self):
        for invalid in (True, -1, 0x100000000, "1"):
            with self.assertRaises(ValueError):
                model.poll_snapshot(invalid, 0, 0, 0, 0, 0)

    def test_source_windows_extend_existing_selection_and_ownership_pins(self):
        self.assertEqual((30, 8, 13), tuple(len(x) for x in (model.OWNERSHIP.SPANS, model.SELECTED.SPANS, model.SPANS)))

    def test_wrong_source_refused(self):
        with self.assertRaises(ValueError):
            model.capture(Path(__file__))

    def test_no_service_or_runtime_authority(self):
        flags = model.evidence_limits()
        self.assertTrue(flags["staticSourceEvidenceOnly"])
        self.assertTrue(flags["snapshotModelsAreNotCpuExecution"])
        for key in ("mipsExecuted", "biosExecuted", "cardServicesExecuted", "boundedCompletionProven",
                    "fullCardIoClosed", "hooksImplemented", "readyAdmissionAuthorized", "writesCard", "writesImage", "publicationApproved"):
            self.assertFalse(flags[key], key)


if __name__ == "__main__":
    unittest.main(verbosity=2)
