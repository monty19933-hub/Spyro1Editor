"""Source-free process ownership tests; no .NET builds or disc images."""

import importlib.util
import contextlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest
from unittest import mock


TOOLS = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("spyro_regression_runner", TOOLS / "Run-SpyroEditorRegression.py")
RUNNER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUNNER)
# Route-only fake commands deliberately emit only route markers. Keep that
# fixture boundary explicit when other mandatory additional modes are added.
ROUTE_ADDITIONAL_CASES = tuple(case for case in RUNNER.ADDITIONAL_CORE_CASES
                               if case[0] == "Id65AuthoringMobyDependencyBundleCompilerSmoke")


def process_is_running(pid):
    # A terminated orphan can remain a zombie until the platform's PID 1 reaps
    # it. It cannot execute or write, so do not confuse it with a live exporter.
    result = subprocess.run(["ps", "-o", "stat=", "-p", str(pid)],
                            capture_output=True, text=True, check=False)
    state = result.stdout.strip()
    return bool(state) and not state.startswith("Z")


@unittest.skipUnless(os.name == "posix", "POSIX process-group implementation; Windows is not exercised")
class OwnedCommandTests(unittest.TestCase):
    def test_success_and_nonzero_preserve_status_and_output(self):
        for status in (0, 7):
            with self.subTest(status=status), tempfile.TemporaryFile(mode="w+") as log:
                result = RUNNER.run_owned_command(
                    [sys.executable, "-c", f"import sys; print('stdout evidence'); print('stderr evidence', file=sys.stderr); sys.exit({status})"],
                    cwd=TOOLS, stdout=log, timeout=3)
                self.assertEqual(status, result.returncode)
                log.seek(0)
                contents = log.read()
                self.assertIn("stdout evidence", contents)
                self.assertIn("stderr evidence", contents)

    def test_timeout_stops_parent_and_term_ignoring_child_before_delayed_write(self):
        with tempfile.TemporaryDirectory(prefix="spyro-process-timeout-") as directory:
            root = Path(directory)
            parent_pid_path = root / "parent.pid"
            child_pid_path = root / "child.pid"
            sentinel = root / "orphan-write.txt"
            child_code = (
                "import os, pathlib, signal, sys, time; "
                "signal.signal(signal.SIGTERM, signal.SIG_IGN); "
                "pathlib.Path(sys.argv[1]).write_text(str(os.getpid())); "
                "time.sleep(2); pathlib.Path(sys.argv[2]).write_text('orphan exporter')"
            )
            parent_code = (
                "import os, pathlib, signal, subprocess, sys, time; "
                "signal.signal(signal.SIGTERM, signal.SIG_IGN); "
                "pathlib.Path(sys.argv[1]).write_text(str(os.getpid())); "
                "child = subprocess.Popen([sys.executable, '-c', sys.argv[4], sys.argv[2], sys.argv[3]]); "
                "time.sleep(3); child.wait()"
            )
            with tempfile.TemporaryFile(mode="w+") as log:
                with self.assertRaises(subprocess.TimeoutExpired):
                    RUNNER.run_owned_command(
                        [sys.executable, "-c", parent_code, str(parent_pid_path), str(child_pid_path), str(sentinel), child_code],
                        cwd=root, stdout=log, timeout=0.8, termination_grace=0.1)
                parent_pid = int(parent_pid_path.read_text())
                child_pid = int(child_pid_path.read_text())
                # Keep the fixture bounded even with the old buggy runner: an
                # orphan exits naturally after its attempted two-second write.
                time.sleep(1.5)
                self.assertFalse(sentinel.exists(), "Timed-out descendant wrote after the runner returned")
                self.assertFalse(process_is_running(parent_pid), "Timed-out parent survived")
                self.assertFalse(process_is_running(child_pid), "Timed-out child survived")


class SourcePythonOracleTests(unittest.TestCase):
    EXPECTED = (
        ("fresh-wizard-audio-audit", "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_fresh_wizard_audio_audit.py",
         "FRESH_WIZARD_GLOBAL1F_SOURCE_AUDIT_OK"),
        ("fresh-wizard-deferred-damage", "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_fresh_wizard_deferred_damage_oracle.py",
         "FRESH_WIZARD_DEFERRED_DAMAGE_ORACLE_OK"),
        ("fresh-wizard-collider-lifetime", "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_fresh_wizard_collider_lifetime_oracle.py",
         "NATIVE_COLLIDER_LIFETIME_SLICES_OK"),
        ("fresh-wizard-p1-scene", "src/Spyro.Editor.NativeId65FreshWizardSceneSmoke/source_p1_scene_oracle.py",
         "FRESH_P1_SCENE_NATIVE_SLICES_OK"),
        ("fresh-wizard-staging", "src/Spyro.Editor.NativeId65FreshWizardStagingSmoke/source_fresh_wizard_staging_oracle.py",
         "FRESH_WIZARD_STAGING_RAW_ORACLE_OK"),
        ("fresh-wizard-fallback-resources", "src/Spyro.Editor.NativeId65FreshWizardFallbackResourceSmoke/source_fallback_resource_oracle.py",
         "SOURCE_TOWN_FALLBACK_ASSETS_OK"),
    )

    def test_exact_source_only_python_registry(self):
        self.assertEqual(self.EXPECTED, RUNNER.SOURCE_PYTHON_CASES)
        self.assertFalse(any(name in dict(RUNNER.CORE_CASES) for name, _, _ in self.EXPECTED))

    def test_python_oracles_are_absent_from_default_and_list_runs(self):
        self.assertEqual(self.EXPECTED, RUNNER.SOURCE_PYTHON_CASES)
        for arguments in ([], ["--list"], ["--list", "--with-source-qa"]):
            calls = []
            def fake_run(command, **kwargs):
                calls.append(command)
                return subprocess.CompletedProcess(command, 0)
            with self.subTest(arguments=arguments), tempfile.TemporaryDirectory(prefix="spyro-source-python-default-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", ()), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("default source read")), \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("unowned CUE read")), \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin), *arguments]), \
                        contextlib.redirect_stdout(io.StringIO()) as shown:
                    self.assertEqual(0, RUNNER.main())
                self.assertFalse(any(command[1:2] == ["-B"] for command in calls))
                self.assertFalse(any(marker in shown.getvalue() for _, _, marker in self.EXPECTED))

    def test_source_python_commands_markers_failures_and_shared_deadline(self):
        self.assertEqual(self.EXPECTED, RUNNER.SOURCE_PYTHON_CASES)
        scenarios = {
            "success": (0, len(self.EXPECTED)), "source-resolution": (1, 0), "offline-failure": (1, 0),
            "upstream-source-failure": (1, 0), "expired-after-offline": (1, 0),
        }
        # Every source oracle gets the same failure/marker/deadline controls.
        # Extending the registry must not silently omit a later case or require
        # another hand-written ordinal branch in the fake process.
        for index in range(len(self.EXPECTED)):
            scenarios[f"missing-marker-{index}"] = (1, index + 1)
            scenarios[f"nonzero-{index}"] = (1, index + 1)
            if index + 1 < len(self.EXPECTED):
                scenarios[f"expired-after-{index}"] = (1, index + 1)
        for scenario, (status, script_count) in scenarios.items():
            calls, clock = [], [100.0]
            def fake_run(command, **kwargs):
                calls.append((command, kwargs["timeout"]))
                if command[0] == "env":
                    kwargs["stdout"].write("Offline QA passed.\n")
                    if scenario == "expired-after-offline": clock[0] += 6
                    if scenario == "offline-failure": return subprocess.CompletedProcess(command, 1)
                if command[:2] == ["dotnet", "run"]:
                    return subprocess.CompletedProcess(command, 1)
                if command[1:2] == ["-B"]:
                    index = next(i for i, (_, relative, _) in enumerate(self.EXPECTED) if command[2] == str(RUNNER.ROOT / relative))
                    if scenario != f"missing-marker-{index}":
                        kwargs["stdout"].write(self.EXPECTED[index][2] + "\n")
                    if scenario == f"expired-after-{index}":
                        clock[0] += 6
                    if scenario == f"nonzero-{index}":
                        return subprocess.CompletedProcess(command, 7)
                return subprocess.CompletedProcess(command, 0)
            with self.subTest(scenario=scenario), tempfile.TemporaryDirectory(prefix="spyro-source-python-opt-in-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", (("SourceGuard", "UNREACHED", []),) if scenario == "upstream-source-failure" else ()), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", return_value="/selected original.bin",
                                          side_effect=FileNotFoundError("missing selected original") if scenario == "source-resolution" else None), \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("unowned CUE read")), \
                        mock.patch.object(RUNNER.time, "monotonic", side_effect=lambda: clock[0]), \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin), "--with-source-qa", "--source-qa-timeout", "5"]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(status, RUNNER.main())
                summary = json.loads((Path(output) / "summary.json").read_text())
            scripts = [(command, timeout) for command, timeout in calls if command[1:2] == ["-B"]]
            self.assertEqual(script_count, len(scripts))
            for index, (command, timeout) in enumerate(scripts):
                self.assertEqual([sys.executable, "-B", str(RUNNER.ROOT / self.EXPECTED[index][1]), "/selected original.bin"], command)
                self.assertGreater(timeout, 0)
                self.assertLessEqual(timeout, 5)
            self.assertEqual(len(summary), len({row["name"] for row in summary}))
            if scenario == "success":
                self.assertTrue(all(row["passed"] for row in summary))
                self.assertEqual(["source-" + name for name, _, _ in self.EXPECTED],
                                 [row["name"] for row in summary[-len(self.EXPECTED):]])
            else:
                self.assertFalse(summary[-1]["passed"])
                if scenario.startswith("missing-"): self.assertEqual("missing success marker", summary[-1]["detail"])
                if scenario.startswith("expired-"): self.assertIn("no command was started", summary[-1]["detail"])
                if scenario == "source-resolution": self.assertIsNone(summary[-1]["command"])


class RunnerOptionsTests(unittest.TestCase):
    def setUp(self):
        # Existing project-only fake commands/markers intentionally isolate
        # that registry. SourcePythonOracleTests covers the real new registry.
        patcher = mock.patch.object(RUNNER, "SOURCE_PYTHON_CASES", ())
        patcher.start()
        self.addCleanup(patcher.stop)

    def test_variable_data_is_additional_default_sourcefree_mode_without_source_authority(self):
        name = "NativeId65FreshDocumentSmoke"
        marker, flag = "NATIVE_ID65_VARIABLE_DATA_FREE_OK", "--variable-data-free"
        self.assertEqual([(name, "NATIVE_ID65_FRESH_DOCUMENT_SMOKE_OK")],
                         [row for row in RUNNER.CORE_CASES if row[0] == name])
        self.assertEqual([(name, marker, [flag])],
                         [row for row in RUNNER.ADDITIONAL_CORE_CASES if row[0] == name])
        self.assertNotIn(name, RUNNER.CASE_ARGUMENTS, "Original no-argument document grammar remains a distinct test")
        self.assertFalse(any(row[0] == name for row in RUNNER.SOURCE_QA_CASES))
        self.assertNotIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)
        with mock.patch.object(sys, "argv", [str(SPEC.origin), "--list"]), \
                mock.patch.object(RUNNER, "run_owned_command", side_effect=AssertionError("list executed a command")), \
                mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("default source read")), \
                mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("default CUE read")), \
                contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(0, RUNNER.main())
        lines = [line for line in output.getvalue().splitlines() if "Spyro.Editor." + name + "/" in line]
        self.assertEqual(2, len(lines))
        self.assertTrue(any(flag + " | " + marker in line for line in lines))
        self.assertFalse(any("--variable-data-source" in line or "--variable-data-red" in line for line in lines))

    def test_variable_data_marker_and_result_suffix_preserve_old_mode_and_single_build(self):
        name, old_marker = "NativeId65FreshDocumentSmoke", "NATIVE_ID65_FRESH_DOCUMENT_SMOKE_OK"
        marker, flag = "NATIVE_ID65_VARIABLE_DATA_FREE_OK", "--variable-data-free"
        for emit_marker in (False, True):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                if command[:2] == ["dotnet", "run"]:
                    kwargs["stdout"].write(marker if flag in command and emit_marker else old_marker)
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(emit_marker=emit_marker), tempfile.TemporaryDirectory(prefix="spyro-variable-data-runner-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ((name, old_marker),)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ((name, marker, [flag]),)), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", ()), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("default source read")), \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("default CUE read")), \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin)]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if emit_marker else 1, RUNNER.main())
                summary = json.loads((Path(output) / "summary.json").read_text())
            builds = [command for command in calls if command[:2] == ["dotnet", "build"]]
            self.assertEqual(2, len(builds), "Shared document project builds once, plus App")
            runs = [command for command in calls if command[:2] == ["dotnet", "run"]]
            self.assertEqual(2, len(runs))
            self.assertNotIn("--", runs[0])
            self.assertEqual([flag], runs[1][runs[1].index("--") + 1:])
            self.assertTrue(all("--no-build" in command and "--no-restore" in command for command in runs))
            self.assertEqual(["run-Spyro.Editor." + name, "run-Spyro.Editor." + name + "-variable-data"],
                             [row["name"] for row in summary[-2:]])
            self.assertEqual(len(summary), len({row["name"] for row in summary}))
            self.assertEqual(len(summary), len({row["log"] for row in summary}))
            self.assertTrue(summary[-2]["passed"])
            self.assertEqual(emit_marker, summary[-1]["passed"], "Old marker cannot stand in for variable-data regression")
            self.assertFalse(any(command[0] == "env" for command in calls))

    def test_fresh_ui_uses_only_the_isolated_headless_mode_and_its_own_marker(self):
        name, marker, flag = "UiSmoke", "FRESH_NATIVE_DOCUMENT_UI_OK", "--fresh-native-document-only"
        self.assertEqual([(name, marker)], [c for c in RUNNER.CORE_CASES if c[0] == name])
        self.assertEqual([flag], RUNNER.CASE_ARGUMENTS.get(name))
        self.assertFalse(any(c[0] == name for c in RUNNER.SOURCE_QA_CASES))
        for emit_marker in (False, True):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                if command[:2] == ["dotnet", "run"]:
                    kwargs["stdout"].write(marker if emit_marker else "some other UI test passed")
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(emit_marker=emit_marker), tempfile.TemporaryDirectory(prefix="spyro-fresh-ui-runner-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ((name, marker),)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", ()), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("neutral UI source read")), \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("neutral UI CUE read")), \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin)]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if emit_marker else 1, RUNNER.main())
                summary = json.loads((Path(output) / "summary.json").read_text())
            runs = [c for c in calls if c[:2] == ["dotnet", "run"]]
            self.assertEqual(1, len(runs))
            self.assertEqual([flag], runs[0][runs[0].index("--") + 1:])
            self.assertIn("--no-build", runs[0])
            self.assertIn("--no-restore", runs[0])
            self.assertEqual(emit_marker, summary[-1]["passed"])
            self.assertFalse(any(c[0] == "env" for c in calls))

    def test_fresh_document_is_sourcefree_only_and_requires_its_own_marker(self):
        name = "NativeId65FreshDocumentSmoke"
        marker = "NATIVE_ID65_FRESH_DOCUMENT_SMOKE_OK"
        self.assertEqual([(name, marker)], [c for c in RUNNER.CORE_CASES if c[0] == name])
        self.assertNotIn(name, RUNNER.CASE_ARGUMENTS)
        self.assertFalse(any(c[0] == name for c in RUNNER.SOURCE_QA_CASES))
        self.assertNotIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)
        for emit_marker in (False, True):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                if command[:2] == ["dotnet", "run"]:
                    kwargs["stdout"].write(marker if emit_marker else "NATIVE_ID65_FRESH_DISC_RESEARCH_SMOKE_OK")
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(emit_marker=emit_marker), tempfile.TemporaryDirectory(prefix="spyro-fresh-document-runner-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ((name, marker),)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", ()), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("document source read")), \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("document CUE read")), \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin)]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if emit_marker else 1, RUNNER.main())
                summary = json.loads((Path(output) / "summary.json").read_text())
            runs = [c for c in calls if c[:2] == ["dotnet", "run"]]
            self.assertEqual(1, len(runs))
            self.assertNotIn("--", runs[0], "neutral document must receive no source/output/publish arguments")
            self.assertIn("--no-build", runs[0])
            self.assertIn("--no-restore", runs[0])
            self.assertEqual(emit_marker, summary[-1]["passed"])
            self.assertFalse(any(c[0] == "env" for c in calls))

    def test_fixture_lookup_registers_only_the_noninvoking_source_free_mode(self):
        name = "Id65AuthoringRewardStagingRuntimeCandidatePublisherSmoke"
        marker = "V3_FIXTURE_LOOKUP_SOURCE_FREE_OK"
        flag = "--source-free-fixture-lookup"
        self.assertEqual([(name, marker)], [c for c in RUNNER.CORE_CASES if c[0] == name])
        self.assertEqual([flag], RUNNER.CASE_ARGUMENTS.get(name))
        self.assertFalse(any(c[0] == name for c in RUNNER.SOURCE_QA_CASES))
        for emit_marker in (False, True):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                if command[:2] == ["dotnet", "run"]:
                    kwargs["stdout"].write(marker if emit_marker else "legacy publication marker is not sufficient")
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(emit_marker=emit_marker), tempfile.TemporaryDirectory(prefix="spyro-fixture-lookup-runner-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ((name, marker),)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", ()), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("unowned source read")), \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("unowned CUE read")), \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin)]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if emit_marker else 1, RUNNER.main())
                summary = json.loads((Path(output) / "summary.json").read_text())
            runs = [c for c in calls if c[:2] == ["dotnet", "run"]]
            self.assertEqual(1, len(runs))
            self.assertEqual([flag], runs[0][runs[0].index("--") + 1:])
            self.assertIn("--no-build", runs[0])
            self.assertIn("--no-restore", runs[0])
            self.assertEqual(emit_marker, summary[-1]["passed"])
            self.assertFalse(any(c[0] == "env" for c in calls))

    def test_count_placement_modes_keep_exact_markers_and_opt_in_workspace(self):
        self.assert_count_placement_modes("NativeWizardAppendPlacementSmoke",
                                         "NATIVE_WIZARD_APPEND_PLACEMENT_FREE_OK", "NATIVE_WIZARD_APPEND_PLACEMENT_SOURCE_OK")

    def test_count_composition_modes_keep_exact_markers_and_opt_in_workspace(self):
        self.assert_count_placement_modes("NativeWizardCompositionSmoke",
                                         "NATIVE_WIZARD_COMPOSITION_FREE_OK", "NATIVE_WIZARD_COMPOSITION_SOURCE_OK")

    def test_two_wizard_research_modes_require_selected_bin_cue_workspace_and_never_publish(self):
        self.assert_count_placement_modes("NativeTwoWizardResearchSmoke",
                                         "NATIVE_TWO_WIZARD_RESEARCH_FREE_OK", "NATIVE_TWO_WIZARD_RESEARCH_SOURCE_OK",
                                         needs_cue=True)

    def assert_count_placement_modes(self, name, free_marker, source_marker, needs_cue=False):
        self.assertEqual([(name, free_marker)], [c for c in RUNNER.CORE_CASES if c[0] == name])
        self.assertEqual([(name, source_marker, ["--source"])],
                         [c for c in RUNNER.SOURCE_QA_CASES if c[0] == name])
        self.assertIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)
        self.assertEqual(needs_cue, name in RUNNER.SOURCE_QA_CUE_CASES)
        self.assertNotIn(name, RUNNER.CASE_ARGUMENTS)
        for scenario, opted_in, free_ok, source_ok in (
                ("default", False, True, True), ("opt-in", True, True, True),
                ("missing-free-marker", True, False, True), ("missing-source-marker", True, True, False)):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                kwargs["stdout"].write("Offline QA passed.\n")
                if command[:2] == ["dotnet", "run"]:
                    is_source = "--source" in command
                    if is_source:
                        kwargs["stdout"].write(free_marker + "\n")
                    if (source_ok if is_source else free_ok):
                        kwargs["stdout"].write((source_marker if is_source else free_marker) + "\n")
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(scenario=scenario), tempfile.TemporaryDirectory(prefix="spyro-runner-count-placement-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", tuple(c for c in RUNNER.CORE_CASES if c[0] == name)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", tuple(c for c in RUNNER.SOURCE_QA_CASES if c[0] == name)), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", return_value="/selected original.bin") as source, \
                        mock.patch.object(RUNNER, "configured_source_cue", return_value="/selected original.cue",
                                          side_effect=None if needs_cue else AssertionError("unowned CUE read")) as cue, \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin), *(["--with-source-qa"] if opted_in else [])]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if free_ok and (not opted_in or source_ok) else 1, RUNNER.main())
                    self.assertEqual(int(opted_in and free_ok), source.call_count)
                    self.assertEqual(int(opted_in and free_ok and needs_cue), cue.call_count)
                summary = json.loads((Path(output) / "summary.json").read_text())
            self.assertEqual(2, len([c for c in calls if c[:2] == ["dotnet", "build"]]))
            runs = [c for c in calls if c[:2] == ["dotnet", "run"]]
            self.assertEqual(1 + int(opted_in and free_ok), len(runs))
            self.assertNotIn("--", runs[0], "Default placement mode cannot receive source/workspace arguments")
            if opted_in and free_ok:
                self.assertEqual(["--source", "/selected original.bin",
                                  *(["/selected original.cue"] if needs_cue else []), str(RUNNER.ROOT)],
                                 runs[1][runs[1].index("--") + 1:])
                self.assertEqual("source-" + name, summary[-1]["name"])
                self.assertEqual(source_ok, summary[-1]["passed"])
            if not free_ok:
                self.assertFalse(any(c[0] == "env" for c in calls), "Missing free marker must prevent source QA")
                self.assertFalse(summary[-1]["passed"])
            for command in runs:
                self.assertIn("--no-build", command)
                self.assertIn("--no-restore", command)
                self.assertFalse(any(flag in command for flag in ("--publish", "--verify", "--faults", "--exercise-staging")))
            self.assertEqual(len(summary), len({row["name"] for row in summary}))

    def test_static_texture_modes_use_only_explicit_source_image_and_exact_markers(self):
        self.assert_texture_modes("NativeId65StaticTerrainTextureSmoke", "NATIVE_ID65_STATIC_TERRAIN_TEXTURE")

    def test_fresh_texture_modes_use_only_explicit_source_image_and_exact_markers(self):
        self.assert_texture_modes("NativeId65FreshTerrainTextureSmoke", "NATIVE_ID65_FRESH_TERRAIN_TEXTURE")

    def test_textured_body_modes_use_only_exact_read_only_source_and_separate_markers(self):
        self.assert_texture_modes("NativeId65TexturedBodySmoke", "NATIVE_ID65_TEXTURED_BODY")

    def test_fresh_disc_modes_require_their_own_markers_and_only_the_selected_image(self):
        self.assert_texture_modes("NativeId65FreshDiscSmoke", "NATIVE_ID65_FRESH_DISC", (), "SMOKE")

    def test_fresh_disc_stream_modes_build_independently_and_require_their_own_markers(self):
        self.assert_texture_modes("NativeId65FreshDiscStreamSmoke", "NATIVE_ID65_FRESH_DISC_STREAM", (), "SMOKE")
        project = RUNNER.ROOT / "src/Spyro.Editor.NativeId65FreshDiscStreamSmoke/Spyro.Editor.NativeId65FreshDiscStreamSmoke.csproj"
        self.assertIn('../Spyro.Editor.NativeId65FreshDiscSmoke/StreamReaderProbe.cs', project.read_text())

    def test_fresh_disc_research_modes_bind_only_selected_bin_cue_and_never_publish(self):
        self.assert_texture_modes("NativeId65FreshDiscResearchSmoke", "NATIVE_ID65_FRESH_DISC_RESEARCH",
                                  ("--source",), "SMOKE", needs_cue=True)

    def test_fresh_edited_document_modes_bind_only_selected_bin_cue_and_never_publish(self):
        self.assert_texture_modes("NativeId65FreshEditedDocumentResearchSmoke", "NATIVE_ID65_FRESH_EDITED_DOCUMENT_RESEARCH",
                                  ("--source",), "SMOKE", needs_cue=True)

    def test_saved_document_plan_modes_never_activate_publication(self):
        self.assert_texture_modes("NativeId65FreshDocumentResearchSmoke", "NATIVE_ID65_FRESH_DOCUMENT_RESEARCH",
                                  ("--source",), "SMOKE", needs_cue=True)

    def test_detached_wizard_asset_modes_bind_only_selected_source(self):
        self.assert_texture_modes("NativeWizardFamilyAssetSmoke", "NATIVE_WIZARD_FAMILY_ASSET")

    def test_fresh_wizard_resource_modes_bind_only_selected_bin_and_never_output(self):
        self.assert_texture_modes("NativeId65FreshWizardResourceSmoke", "FRESH_WIZARD_RESOURCE")

    def test_fresh_wizard_scene_modes_keep_source_optional_and_require_distinct_markers(self):
        self.assert_texture_modes("NativeId65FreshWizardSceneSmoke", "FRESH_WIZARD_SCENE")

    def test_fresh_wizard_staging_modes_keep_source_optional_and_never_publish(self):
        self.assert_texture_modes("NativeId65FreshWizardStagingSmoke", "FRESH_WIZARD_STAGING")

    def test_fresh_wizard_fallback_resources_require_selected_bin_and_distinct_markers(self):
        self.assert_texture_modes("NativeId65FreshWizardFallbackResourceSmoke", "FRESH_WIZARD_FALLBACK_RESOURCE")

    def test_fresh_wizard_host_modes_require_same_selected_bin_without_publication(self):
        self.assert_texture_modes("NativeId65FreshWizardHostSmoke", "FRESH_WIZARD_HOST")

    def test_fresh_wizard_body_modes_remain_read_only_with_explicit_source_and_markers(self):
        self.assert_texture_modes("NativeId65FreshWizardBodySmoke", "FRESH_WIZARD_BODY")

    def test_fresh_wizard_executable_modes_remain_detached_without_candidate_output(self):
        self.assert_texture_modes("NativeId65FreshWizardExecutableSmoke", "FRESH_WIZARD_EXECUTABLE")

    def test_fresh_wizard_disc_modes_only_read_selected_source_without_publication(self):
        self.assert_texture_modes("NativeId65FreshWizardDiscSmoke", "FRESH_WIZARD_DISC")

    def test_fresh_wizard_research_default_and_source_never_publish(self):
        self.assert_texture_modes("NativeId65FreshWizardResearchSmoke", "FRESH_WIZARD_RESEARCH", needs_cue=True)

    def test_saved_document_publisher_modes_never_activate_output(self):
        self.assert_texture_modes("NativeId65FreshDocumentPublicationSmoke", "NATIVE_ID65_FRESH_DOCUMENT_PUBLICATION",
                                  ("--source",), "SMOKE", needs_cue=True)

    def assert_texture_modes(self, name, marker_prefix, source_flags=("--source",), free_kind="FREE", needs_cue=False):
        free_marker = marker_prefix + "_" + free_kind + "_OK"
        source_marker = marker_prefix + "_SOURCE_OK"
        self.assertEqual([(name, free_marker)], [c for c in RUNNER.CORE_CASES if c[0] == name])
        self.assertEqual([(name, source_marker, list(source_flags))],
                         [c for c in RUNNER.SOURCE_QA_CASES if c[0] == name])
        self.assertNotIn(name, RUNNER.CASE_ARGUMENTS)
        self.assertNotIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)
        self.assertEqual(needs_cue, name in RUNNER.SOURCE_QA_CUE_CASES)
        for scenario, opted_in, free_ok, source_ok in (
                ("default", False, True, True), ("opt-in", True, True, True),
                ("missing-free-marker", True, False, True), ("missing-source-marker", True, True, False)):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                kwargs["stdout"].write("Offline QA passed.\n")
                if command[:2] == ["dotnet", "run"]:
                    is_source = "--" in command
                    marker = source_marker if is_source else free_marker
                    marker_ok = source_ok if is_source else free_ok
                    # A source run may also emit the free marker. It cannot stand
                    # in for the distinct source success marker when missing.
                    if is_source:
                        kwargs["stdout"].write(free_marker + "\n")
                    if marker_ok:
                        kwargs["stdout"].write(marker + "\n")
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(scenario=scenario), tempfile.TemporaryDirectory(prefix="spyro-runner-static-texture-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", tuple(c for c in RUNNER.CORE_CASES if c[0] == name)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", tuple(c for c in RUNNER.SOURCE_QA_CASES if c[0] == name)), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", return_value="/selected original.bin") as source, \
                        mock.patch.object(RUNNER, "configured_source_cue", return_value="/selected original.cue") as cue, \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin), *(["--with-source-qa"] if opted_in else [])]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if free_ok and (not opted_in or source_ok) else 1, RUNNER.main())
                    self.assertEqual(int(opted_in and free_ok), source.call_count)
                    self.assertEqual(int(needs_cue and opted_in and free_ok), cue.call_count)
                summary = json.loads((Path(output) / "summary.json").read_text())
            self.assertEqual(2, len([c for c in calls if c[:2] == ["dotnet", "build"]]))
            runs = [c for c in calls if c[:2] == ["dotnet", "run"]]
            self.assertEqual(1 + int(opted_in and free_ok), len(runs))
            self.assertNotIn("--", runs[0], "Default mode cannot receive source, workspace or publication arguments")
            if opted_in and free_ok:
                self.assertEqual([*source_flags, "/selected original.bin", *(["/selected original.cue"] if needs_cue else [])],
                                 runs[1][runs[1].index("--") + 1:])
                self.assertEqual("source-" + name, summary[-1]["name"])
                self.assertEqual(source_ok, summary[-1]["passed"])
            if not free_ok:
                self.assertFalse(any(c[0] == "env" for c in calls), "Missing free marker must prevent all source QA")
                self.assertFalse(summary[-1]["passed"])
            for command in runs:
                self.assertIn("--no-build", command)
                self.assertIn("--no-restore", command)
                self.assertFalse(any(flag in command for flag in ("--publish", "--verify", "--faults", "--exercise-staging", "--stage-faults", "--exercise-publication")))
            self.assertEqual(len(summary), len({row["name"] for row in summary}))

    def test_wizard_append_modes_are_readonly_and_require_their_exact_markers(self):
        name = "NativeWizardAppendSmoke"
        free_marker = "NATIVE_WIZARD_APPEND_SOURCE_FREE_OK"
        source_marker = "NATIVE_WIZARD_APPEND_SOURCE_OK"
        self.assertEqual([(name, free_marker)], [c for c in RUNNER.CORE_CASES if c[0] == name])
        self.assertEqual([(name, source_marker, ["--source"])],
                         [c for c in RUNNER.SOURCE_QA_CASES if c[0] == name])
        self.assertIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)
        self.assertNotIn(name, RUNNER.CASE_ARGUMENTS)
        for scenario, opted_in, free_ok, source_ok in (
                ("default", False, True, True), ("opt-in", True, True, True),
                ("missing-free-marker", True, False, True), ("missing-source-marker", True, True, False)):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                kwargs["stdout"].write("Offline QA passed.\n")
                if command[:2] == ["dotnet", "run"]:
                    marker = source_marker if "--source" in command else free_marker
                    marker_ok = source_ok if "--source" in command else free_ok
                    if marker_ok:
                        kwargs["stdout"].write(marker + "\n")
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(scenario=scenario), tempfile.TemporaryDirectory(prefix="spyro-runner-wizard-append-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", tuple(c for c in RUNNER.CORE_CASES if c[0] == name)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", tuple(c for c in RUNNER.SOURCE_QA_CASES if c[0] == name)), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", return_value="/selected original.bin") as source, \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("unowned CUE read")) as cue, \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin), *(["--with-source-qa"] if opted_in else [])]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if free_ok and (not opted_in or source_ok) else 1, RUNNER.main())
                    self.assertEqual(int(opted_in and free_ok), source.call_count)
                    cue.assert_not_called()
                summary = json.loads((Path(output) / "summary.json").read_text())
            self.assertEqual(2, len([c for c in calls if c[:2] == ["dotnet", "build"]]))
            runs = [c for c in calls if c[:2] == ["dotnet", "run"]]
            self.assertEqual(1 + int(opted_in and free_ok), len(runs))
            self.assertNotIn("--", runs[0], "Default mode cannot receive a source image or workspace argument")
            if opted_in and free_ok:
                self.assertEqual(["--source", "/selected original.bin", str(RUNNER.ROOT)], runs[1][runs[1].index("--") + 1:])
                self.assertEqual("source-" + name, summary[-1]["name"])
                self.assertEqual(source_ok, summary[-1]["passed"])
            if not free_ok:
                self.assertFalse(any(c[0] == "env" for c in calls), "Missing source-free marker must stop all source QA")
                self.assertFalse(summary[-1]["passed"])
            for command in runs:
                self.assertIn("--no-build", command)
                self.assertIn("--no-restore", command)
                self.assertFalse(any(flag in command for flag in ("--publish", "--verify", "--faults", "--exercise-staging")))
            self.assertEqual(len(summary), len({row["name"] for row in summary}))

    def test_persistent_owner_modes_bind_source_and_workspace_only_when_opted_in(self):
        name = "NativeMobyPersistentOwnerSmoke"
        free_marker = "NATIVE_MOBY_PERSISTENT_OWNER_SOURCE_FREE_OK"
        source_marker = "NATIVE_MOBY_PERSISTENT_OWNER_SOURCE_OK"
        self.assertEqual([(name, free_marker)], [case for case in RUNNER.CORE_CASES if case[0] == name])
        self.assertEqual([(name, source_marker, ["--source"])],
                         [case for case in RUNNER.SOURCE_QA_CASES if case[0] == name])
        self.assertNotIn(name, RUNNER.CASE_ARGUMENTS)
        self.assertIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)
        for opted_in in (False, True):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                kwargs["stdout"].write("Offline QA passed.\n" + free_marker + "\n" + source_marker + "\n")
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(opted_in=opted_in), tempfile.TemporaryDirectory(prefix="spyro-runner-owner-ledger-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", tuple(c for c in RUNNER.CORE_CASES if c[0] == name)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", tuple(c for c in RUNNER.SOURCE_QA_CASES if c[0] == name)), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", return_value="/selected original.bin") as source, \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("unowned CUE read")) as cue, \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin), *(["--with-source-qa"] if opted_in else [])]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0, RUNNER.main())
                    self.assertEqual(int(opted_in), source.call_count)
                    cue.assert_not_called()
                summary = json.loads((Path(output) / "summary.json").read_text())
            builds = [c for c in calls if c[:2] == ["dotnet", "build"]]
            self.assertEqual(2, len(builds), "Owner project must build once, plus App")
            runs = [c for c in calls if c[:2] == ["dotnet", "run"]]
            self.assertEqual(1 + int(opted_in), len(runs))
            self.assertNotIn("--", runs[0], "Default owner mode must not receive source arguments")
            if opted_in:
                self.assertEqual(["--source", "/selected original.bin", str(RUNNER.ROOT)],
                                 runs[1][runs[1].index("--") + 1:])
                self.assertEqual("source-" + name, summary[-1]["name"])
            for command in runs:
                self.assertIn("--no-build", command)
                self.assertIn("--no-restore", command)
                self.assertFalse(any(flag in command for flag in ("--publish", "--verify", "--faults", "--exercise-staging")))
            self.assertTrue(all(row["passed"] for row in summary))
            self.assertEqual(len(summary), len({row["name"] for row in summary}))

    def test_native_resource_and_companion_modes_never_publish(self):
        expected = {
            "NativeResourceCompositionSmoke": ("NATIVE_RESOURCE_COMPOSITION_SOURCE_FREE_OK", "NATIVE_RESOURCE_COMPOSITION_SOURCE_OK", ["--source"]),
            "NativeId65CompanionAssetSmoke": ("NATIVE_ID65_COMPANION_ASSET_SOURCE_FREE_OK", "NATIVE_ID65_COMPANION_ASSET_SOURCE_OK", []),
            "NativeId65AuthoredSurfaceSmoke": ("NATIVE_ID65_AUTHORED_SURFACE_SMOKE_OK", "NATIVE_ID65_AUTHORED_SURFACE_SOURCE_OK", []),
            "NativeId65NormalResourceSmoke": ("NATIVE_ID65_NORMAL_RESOURCE_SMOKE_OK", "NATIVE_ID65_NORMAL_RESOURCE_SOURCE_OK", []),
            "NativeId65NormalBodySmoke": ("NATIVE_ID65_NORMAL_BODY_SMOKE_OK", "NATIVE_ID65_NORMAL_BODY_SOURCE_OK", []),
            "NativeWizardPlacementSmoke": ("NATIVE_WIZARD_PLACEMENT_SMOKE_OK", "NATIVE_WIZARD_PLACEMENT_SOURCE_OK", []),
        }
        for name, (free_marker, source_marker, arguments) in expected.items():
            with self.subTest(project=name):
                self.assertEqual(free_marker, dict(RUNNER.CORE_CASES).get(name))
                self.assertEqual([(name, source_marker, arguments)],
                                 [case for case in RUNNER.SOURCE_QA_CASES if case[0] == name])
                self.assertNotIn(name, RUNNER.CASE_ARGUMENTS)
                self.assertNotIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)
                self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)

    def test_native_id65_body_default_and_positional_source_only(self):
        name = "NativeId65NormalBodySmoke"
        free = "NATIVE_ID65_NORMAL_BODY_SMOKE_OK"
        source_marker = "NATIVE_ID65_NORMAL_BODY_SOURCE_OK"
        self.assertEqual(free, dict(RUNNER.CORE_CASES).get(name))
        self.assertEqual([(name, source_marker, [])],
                         [case for case in RUNNER.SOURCE_QA_CASES if case[0] == name])
        for opted_in in (False, True):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                kwargs["stdout"].write("Offline QA passed.\n" + free + "\n" + source_marker + "\n")
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(opted_in=opted_in), tempfile.TemporaryDirectory(prefix="spyro-runner-body-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ((name, free),)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", ((name, source_marker, []),)), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", return_value="/selected original.bin") as source, \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("body has no CUE owner")) as cue, \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin), *(["--with-source-qa"] if opted_in else [])]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0, RUNNER.main())
                    self.assertEqual(int(opted_in), source.call_count)
                    cue.assert_not_called()
                summary = json.loads((Path(output) / "summary.json").read_text())
            runs = [command for command in calls if command[:2] == ["dotnet", "run"]]
            self.assertEqual(1 + int(opted_in), len(runs))
            self.assertNotIn("--", runs[0])
            if opted_in:
                self.assertEqual(["/selected original.bin"], runs[1][runs[1].index("--") + 1:])
                self.assertEqual("source-" + name, summary[-1]["name"])
            self.assertEqual(2, sum(command[:2] == ["dotnet", "build"] for command in calls))
            self.assertTrue(all("--no-build" in command and "--no-restore" in command for command in runs))
            self.assertTrue(all(row["passed"] for row in summary))

    def test_native_id65_engine_profile_is_read_only_in_both_modes(self):
        name = "NativeId65EngineProfileSmoke"
        self.assertEqual("NATIVE_ID65_ENGINE_PROFILE_SOURCE_FREE_OK", dict(RUNNER.CORE_CASES).get(name))
        self.assertEqual([(name, "NATIVE_ID65_ENGINE_PROFILE_SOURCE_OK", [])],
                         [case for case in RUNNER.SOURCE_QA_CASES if case[0] == name])
        self.assertNotIn(name, RUNNER.CASE_ARGUMENTS)
        self.assertNotIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)

    def test_sound_and_research_candidate_use_separate_nonpublishing_modes(self):
        expected = {
            "NativeGlobalSoundSmoke": ("NATIVE_GLOBAL_SOUND_SOURCE_FREE_OK", "NATIVE_GLOBAL_SOUND_SOURCE_OK", ["--source"]),
            "NativeFreeFlightResearchSmoke": ("NATIVE_FREE_FLIGHT_RESEARCH_SOURCE_FREE_OK", "NATIVE_FREE_FLIGHT_RESEARCH_SOURCE_OK", []),
            "NativeWizardResearchSmoke": ("NATIVE_WIZARD_RESEARCH_SOURCE_FREE_OK", "NATIVE_WIZARD_RESEARCH_SOURCE_OK", []),
            "NativeTwoWizardResearchSmoke": ("NATIVE_TWO_WIZARD_RESEARCH_FREE_OK", "NATIVE_TWO_WIZARD_RESEARCH_SOURCE_OK", ["--source"]),
        }
        for name, (free_marker, source_marker, arguments) in expected.items():
            self.assertEqual(free_marker, dict(RUNNER.CORE_CASES).get(name))
            self.assertNotIn(name, RUNNER.CASE_ARGUMENTS)
            self.assertEqual([(name, source_marker, arguments)], [case for case in RUNNER.SOURCE_QA_CASES if case[0] == name])
        self.assertEqual(frozenset({"NativeFreeFlightResearchSmoke", "NativeWizardResearchSmoke", "NativeId65FreshDiscResearchSmoke", "NativeId65FreshEditedDocumentResearchSmoke", "NativeId65FreshDocumentResearchSmoke", "NativeId65FreshDocumentPublicationSmoke", "NativeTwoWizardResearchSmoke", "NativeId65FreshWizardResearchSmoke"}), RUNNER.SOURCE_QA_CUE_CASES)
        for name in RUNNER.SOURCE_QA_CUE_CASES - {"NativeId65FreshDiscResearchSmoke", "NativeId65FreshEditedDocumentResearchSmoke", "NativeId65FreshDocumentResearchSmoke", "NativeId65FreshDocumentPublicationSmoke", "NativeId65FreshWizardResearchSmoke"}:
            self.assertIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)

    def test_flight_preferences_are_sourcefree_and_never_a_rom_writer(self):
        self.assertEqual("FREE_FLIGHT_SETTINGS_SMOKE_OK", dict(RUNNER.CORE_CASES).get("FreeFlightSettingsSmoke"))
        self.assertFalse(any(case[0] == "FreeFlightSettingsSmoke" for case in RUNNER.SOURCE_QA_CASES))
        self.assertNotIn("FreeFlightSettingsSmoke", RUNNER.CASE_ARGUMENTS)

    def test_animation_and_flight_have_separate_readonly_source_gates(self):
        expected = {
            "NativePlayerAnimationSmoke": ("NATIVE_PLAYER_ANIMATION_SOURCE_FREE_OK", "NATIVE_PLAYER_ANIMATION_SOURCE_OK", ["--source"]),
            "NativeFreeFlightSmoke": ("NATIVE_FREE_FLIGHT_SOURCE_FREE_OK", "NATIVE_FREE_FLIGHT_SOURCE_OK", []),
        }
        core = dict(RUNNER.CORE_CASES)
        for name, (free_marker, source_marker, arguments) in expected.items():
            with self.subTest(project=name):
                self.assertEqual(free_marker, core.get(name))
                self.assertNotIn(name, RUNNER.CASE_ARGUMENTS)
                self.assertEqual([(name, source_marker, arguments)], [case for case in RUNNER.SOURCE_QA_CASES if case[0] == name])
                self.assertNotIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)

    def test_timeout_defaults_and_positive_validation(self):
        options = RUNNER.parse_options([])
        self.assertEqual(600, options.timeout)
        self.assertEqual(1800, options.source_qa_timeout)
        for flag in ("--timeout", "--source-qa-timeout"):
            for value in ("0", "-1"):
                with self.subTest(flag=flag, value=value), contextlib.redirect_stderr(io.StringIO()):
                    with self.assertRaises(SystemExit) as raised:
                        RUNNER.parse_options([flag, value])
                    self.assertEqual(2, raised.exception.code)

    def test_source_qa_uses_its_separate_timeout_without_running_dotnet(self):
        calls = []

        def fake_run(command, **kwargs):
            calls.append((command, kwargs["timeout"]))
            kwargs["stdout"].write("Offline QA passed.\n")
            return subprocess.CompletedProcess(command, 0)

        with tempfile.TemporaryDirectory(prefix="spyro-runner-options-") as output:
            with mock.patch.object(RUNNER, "CORE_CASES", ()), \
                    mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                    mock.patch.object(RUNNER, "SOURCE_QA_CASES", ()), \
                    mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                    mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                    mock.patch.object(sys, "argv", [str(SPEC.origin), "--with-source-qa", "--timeout", "123", "--source-qa-timeout", "456"]), \
                    contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0, RUNNER.main())
        self.assertEqual(3, len(calls))
        self.assertEqual(sys.executable, calls[0][0][0])
        self.assertEqual(123, calls[0][1])
        self.assertEqual("dotnet", calls[1][0][0])
        self.assertEqual(123, calls[1][1])
        self.assertEqual("env", calls[2][0][0])
        self.assertEqual(456, calls[2][1])

    def test_list_never_runs_commands_or_creates_logs(self):
        with mock.patch.object(RUNNER, "run_owned_command") as command, \
                mock.patch.object(RUNNER.tempfile, "mkdtemp") as temporary, \
                mock.patch.object(sys, "argv", [str(SPEC.origin), "--list"]), \
                contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, RUNNER.main())
        command.assert_not_called()
        temporary.assert_not_called()

    def test_timeout_remains_a_failed_gate(self):
        with tempfile.TemporaryDirectory(prefix="spyro-runner-failure-") as output:
            with mock.patch.object(RUNNER, "CORE_CASES", ()), \
                    mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                    mock.patch.object(RUNNER, "SOURCE_QA_CASES", ()), \
                    mock.patch.object(RUNNER, "run_owned_command", side_effect=subprocess.TimeoutExpired(["dotnet"], 1)), \
                    mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                    mock.patch.object(sys, "argv", [str(SPEC.origin), "--with-source-qa"]), \
                    contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1, RUNNER.main())

    def test_build_all_fails_for_a_missing_registered_project(self):
        calls = []

        def fake_run(command, **kwargs):
            calls.append(command)
            status = 0
            if command[:2] == ["dotnet", "build"]:
                status = 0 if Path(command[2]).exists() else 9
            return subprocess.CompletedProcess(command, status)

        with tempfile.TemporaryDirectory(prefix="spyro-runner-missing-project-") as directory:
            root = Path(directory)
            app = root / "src/Spyro.Editor.App/Spyro.Editor.App.csproj"
            discovered = root / "src/Other/Other.csproj"
            for project in (app, discovered):
                project.parent.mkdir(parents=True)
                project.write_text("<Project />\n")
            missing = root / "src/Spyro.Editor.MissingRegisteredSmoke/Spyro.Editor.MissingRegisteredSmoke.csproj"
            logs = root / "logs"
            logs.mkdir()
            with mock.patch.object(RUNNER, "ROOT", root), \
                    mock.patch.object(RUNNER, "CORE_CASES", (("MissingRegisteredSmoke", "unreachable success"),)), \
                    mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                    mock.patch.object(RUNNER, "SOURCE_QA_CASES", ()), \
                    mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                    mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=str(logs)), \
                    mock.patch.object(sys, "argv", [str(SPEC.origin), "--build-all", "--with-source-qa"]), \
                    contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1, RUNNER.main())
            built = [Path(command[2]) for command in calls if command[:2] == ["dotnet", "build"]]
            self.assertCountEqual([app, discovered, missing], built)
            self.assertFalse(any(command[:2] == ["dotnet", "run"] for command in calls))
            self.assertFalse(any(command[0] == "env" for command in calls), "Source QA ran despite missing regression build")

    def test_unsupported_platform_fails_before_starting_a_process(self):
        with mock.patch.object(RUNNER.os, "name", "nt"), \
                mock.patch.object(RUNNER.subprocess, "Popen") as popen:
            with self.assertRaisesRegex(OSError, "POSIX"):
                RUNNER.run_owned_command(["unused"], cwd=".", stdout=None, timeout=1)
            popen.assert_not_called()

    def test_route_modes_preserve_existing_mode_and_build_once(self):
        calls = []
        name = "Id65AuthoringMobyDependencyBundleCompilerSmoke"
        clock = [100.0]

        def fake_run(command, **kwargs):
            calls.append((command, kwargs["timeout"]))
            kwargs["stdout"].write("Offline QA passed.\nNATIVE_SCENE_PROPERTIES_LINKER_SMOKE_OK\n"
                                   "NATIVE_ROUTE_SURFACE_SMOKE_OK\nNATIVE_GREEN_WIZARD_ROUTE_SOURCE_SMOKE_OK\n")
            if command[0] == "env":
                clock[0] += 400
            return subprocess.CompletedProcess(command, 0)

        with tempfile.TemporaryDirectory(prefix="spyro-runner-route-modes-") as output:
            with mock.patch.object(RUNNER, "CORE_CASES", ((name, "NATIVE_SCENE_PROPERTIES_LINKER_SMOKE_OK"),)), \
                    mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ROUTE_ADDITIONAL_CASES), \
                    mock.patch.object(RUNNER, "SOURCE_QA_CASES", ((name, "NATIVE_GREEN_WIZARD_ROUTE_SOURCE_SMOKE_OK", ["--source-backed-route-surfaces"]),)), \
                    mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                    mock.patch.object(RUNNER, "configured_source_image", return_value="/selected original.bin") as source, \
                    mock.patch.object(RUNNER.time, "monotonic", side_effect=lambda: clock[0]), \
                    mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                    mock.patch.object(sys, "argv", [str(SPEC.origin), "--with-source-qa", "--configuration", "Debug",
                                                    "--timeout", "123", "--source-qa-timeout", "456"]), \
                    contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0, RUNNER.main())
                source.assert_called_once_with()
            summary = json.loads((Path(output) / "summary.json").read_text())
        builds = [c for c, _ in calls if c[:2] == ["dotnet", "build"]]
        self.assertEqual(2, len(builds), "The shared smoke project must build once, plus App")
        runs = [(c, timeout) for c, timeout in calls if c[:2] == ["dotnet", "run"]]
        self.assertEqual([["--source-free-properties-linker"], ["--source-free-route-surfaces"],
                          ["--source-backed-route-surfaces", "/selected original.bin"]],
                         [c[c.index("--") + 1:] for c, _ in runs])
        self.assertEqual([123, 123, 56], [timeout for _, timeout in runs])
        self.assertTrue(all(c[c.index("--configuration") + 1] == "Debug" for c, _ in runs))
        self.assertTrue(all("--no-build" in c and "--no-restore" in c for c, _ in runs))
        self.assertTrue(all(r["passed"] for r in summary))
        self.assertEqual(len(summary), len({r["name"] for r in summary}))

    def test_inventory_source_case_binds_selected_image_and_current_workspace(self):
        calls = []
        name = "NativeMobyInventorySmoke"

        def fake_run(command, **kwargs):
            calls.append(command)
            kwargs["stdout"].write("Offline QA passed.\nNATIVE_MOBY_INVENTORY_SMOKE_OK\nNATIVE_MOBY_INVENTORY_SOURCE_SMOKE_OK\n")
            return subprocess.CompletedProcess(command, 0)

        with tempfile.TemporaryDirectory(prefix="spyro-runner-native-inventory-") as output:
            with mock.patch.object(RUNNER, "CORE_CASES", ((name, "NATIVE_MOBY_INVENTORY_SMOKE_OK"),)), \
                    mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                    mock.patch.object(RUNNER, "SOURCE_QA_CASES", ((name, "NATIVE_MOBY_INVENTORY_SOURCE_SMOKE_OK", ["--source"]),)), \
                    mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                    mock.patch.object(RUNNER, "configured_source_image", return_value="/selected original.bin") as source, \
                    mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                    mock.patch.object(sys, "argv", [str(SPEC.origin), "--with-source-qa"]), \
                    contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0, RUNNER.main())
                source.assert_called_once_with()
            summary = json.loads((Path(output) / "summary.json").read_text())
        builds = [c for c in calls if c[:2] == ["dotnet", "build"]]
        self.assertEqual(2, len(builds), "Inventory project builds once, plus App")
        source_runs = [c for c in calls if "--source" in c]
        self.assertEqual(1, len(source_runs))
        command = source_runs[0]
        self.assertEqual(["--source", "/selected original.bin", str(RUNNER.ROOT)], command[command.index("--") + 1:])
        self.assertTrue(all(r["passed"] for r in summary))
        self.assertEqual("source-" + name, summary[-1]["name"])

    def test_native_source_checks_use_read_only_modes_and_distinct_logs(self):
        cases = tuple(c for c in RUNNER.SOURCE_QA_CASES if c[0].startswith("Native"))
        expected = {
            "NativeMobyDependencyClosureSmoke": ["--source-v3", "/selected original.bin"],
            "NativeMobyInventorySmoke": ["--source", "/selected original.bin", str(RUNNER.ROOT)],
            "NativeMobyPersistentOwnerSmoke": ["--source", "/selected original.bin", str(RUNNER.ROOT)],
            "NativeWizardAppendSmoke": ["--source", "/selected original.bin", str(RUNNER.ROOT)],
            "NativeWizardAppendPlacementSmoke": ["--source", "/selected original.bin", str(RUNNER.ROOT)],
            "NativeWizardCompositionSmoke": ["--source", "/selected original.bin", str(RUNNER.ROOT)],
            "NativeWizardFamilyAssetSmoke": ["--source", "/selected original.bin"],
            "NativeId65FreshWizardResourceSmoke": ["--source", "/selected original.bin"],
            "NativeId65FreshWizardSceneSmoke": ["--source", "/selected original.bin"],
            "NativeId65FreshWizardStagingSmoke": ["--source", "/selected original.bin"],
            "NativeId65FreshWizardFallbackResourceSmoke": ["--source", "/selected original.bin"],
            "NativeId65FreshWizardHostSmoke": ["--source", "/selected original.bin"],
            "NativeId65FreshWizardBodySmoke": ["--source", "/selected original.bin"],
            "NativeId65FreshWizardExecutableSmoke": ["--source", "/selected original.bin"],
            "NativeId65FreshWizardDiscSmoke": ["--source", "/selected original.bin"],
            "NativeId65FreshWizardResearchSmoke": ["--source", "/selected original.bin", "/selected original.cue"],
            "NativeGreenWizardMoverSmoke": ["/selected original.bin"],
            "NativeLevelSlotOwnershipSmoke": ["--source", "/selected original.bin"],
            "NativePlayerAnimationSmoke": ["--source", "/selected original.bin"],
            "NativeFreeFlightSmoke": ["/selected original.bin"],
            "NativeGlobalSoundSmoke": ["--source", "/selected original.bin"],
            "NativeId65EngineProfileSmoke": ["/selected original.bin"],
            "NativeId65CompanionAssetSmoke": ["/selected original.bin"],
            "NativeId65AuthoredSurfaceSmoke": ["/selected original.bin"],
            "NativeId65NormalResourceSmoke": ["/selected original.bin"],
            "NativeId65NormalBodySmoke": ["/selected original.bin"],
            "NativeId65StaticTerrainTextureSmoke": ["--source", "/selected original.bin"],
            "NativeId65FreshTerrainTextureSmoke": ["--source", "/selected original.bin"],
            "NativeId65TexturedBodySmoke": ["--source", "/selected original.bin"],
            "NativeId65FreshDiscSmoke": ["/selected original.bin"],
            "NativeId65FreshDiscStreamSmoke": ["/selected original.bin"],
            "NativeId65FreshDiscResearchSmoke": ["--source", "/selected original.bin", "/selected original.cue"],
            "NativeId65FreshEditedDocumentResearchSmoke": ["--source", "/selected original.bin", "/selected original.cue"],
            "NativeId65FreshDocumentResearchSmoke": ["--source", "/selected original.bin", "/selected original.cue"],
            "NativeId65FreshDocumentPublicationSmoke": ["--source", "/selected original.bin", "/selected original.cue"],
            "NativeWizardPlacementSmoke": ["/selected original.bin"],
            "NativeResourceCompositionSmoke": ["--source", "/selected original.bin"],
            "NativeFreeFlightResearchSmoke": ["/selected original.bin", "/selected original.cue", str(RUNNER.ROOT)],
            "NativeWizardResearchSmoke": ["/selected original.bin", "/selected original.cue", str(RUNNER.ROOT)],
            "NativeTwoWizardResearchSmoke": ["--source", "/selected original.bin", "/selected original.cue", str(RUNNER.ROOT)],
        }
        self.assertEqual(set(expected), {c[0] for c in cases})
        calls = []

        def fake_run(command, **kwargs):
            calls.append(command)
            kwargs["stdout"].write("Offline QA passed.\n" + "\n".join(marker for _, marker, _ in cases))
            return subprocess.CompletedProcess(command, 0)

        with tempfile.TemporaryDirectory(prefix="spyro-runner-native-source-modes-") as output:
            with mock.patch.object(RUNNER, "CORE_CASES", ()), \
                    mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                    mock.patch.object(RUNNER, "SOURCE_QA_CASES", cases), \
                    mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                    mock.patch.object(RUNNER, "configured_source_image", return_value="/selected original.bin") as source, \
                    mock.patch.object(RUNNER, "configured_source_cue", return_value="/selected original.cue") as cue, \
                    mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                    mock.patch.object(sys, "argv", [str(SPEC.origin), "--with-source-qa"]), \
                    contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0, RUNNER.main())
                self.assertEqual(len(cases), source.call_count)
                self.assertEqual(len([c for c in cases if c[0] in RUNNER.SOURCE_QA_CUE_CASES]), cue.call_count)
                self.assertTrue(all(call == mock.call() for call in cue.call_args_list))
            summary = json.loads((Path(output) / "summary.json").read_text())
        runs = [c for c in calls if c[:2] == ["dotnet", "run"]]
        self.assertEqual(len(cases), len(runs))
        for command in runs:
            name = Path(command[command.index("--project") + 1]).stem.removeprefix("Spyro.Editor.")
            self.assertEqual(expected[name], command[command.index("--") + 1:])
            self.assertNotIn("--publish", command)
            self.assertNotIn("--faults", command)
            self.assertNotIn("--exercise-staging", command)
            self.assertNotIn("--stage-faults", command)
            self.assertNotIn("--exercise-publication", command)
        self.assertEqual(len(summary), len({r["name"] for r in summary}))

    def test_default_and_list_never_resolve_source_for_route_modes(self):
        for arguments in ([], ["--list"], ["--list", "--with-source-qa"]):
            with self.subTest(arguments=arguments), tempfile.TemporaryDirectory(prefix="spyro-runner-route-default-") as output:
                calls = []

                def fake_run(command, **kwargs):
                    calls.append(command)
                    kwargs["stdout"].write("NATIVE_ROUTE_SURFACE_SMOKE_OK\n")
                    return subprocess.CompletedProcess(command, 0)

                with mock.patch.object(RUNNER, "CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ROUTE_ADDITIONAL_CASES), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("source read")) as source, \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("cue read")) as cue, \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin), *arguments]), \
                        contextlib.redirect_stdout(io.StringIO()) as text:
                    self.assertEqual(0, RUNNER.main())
                    source.assert_not_called()
                    cue.assert_not_called()
                self.assertFalse(any("--source-backed-route-surfaces" in c for c in calls))
                if "--list" in arguments:
                    self.assertEqual([], calls)
                    self.assertIn("--source-free-route-surfaces", text.getvalue())
                else:
                    self.assertEqual(1, sum("--source-free-route-surfaces" in c for c in calls))

    def test_route_marker_failure_prevents_source_qa(self):
        calls = []

        def fake_run(command, **kwargs):
            calls.append(command)
            return subprocess.CompletedProcess(command, 0)

        with tempfile.TemporaryDirectory(prefix="spyro-runner-route-marker-") as output:
            with mock.patch.object(RUNNER, "CORE_CASES", ()), \
                    mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ROUTE_ADDITIONAL_CASES), \
                    mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                    mock.patch.object(RUNNER, "configured_source_image") as source, \
                    mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                    mock.patch.object(sys, "argv", [str(SPEC.origin), "--with-source-qa"]), \
                    contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1, RUNNER.main())
                source.assert_not_called()
        self.assertFalse(any(c[0] == "env" for c in calls))

    def test_source_resolution_failure_is_recorded_without_running_source_command(self):
        calls = []

        def fake_run(command, **kwargs):
            calls.append(command)
            kwargs["stdout"].write("Offline QA passed.\nNATIVE_ROUTE_SURFACE_SMOKE_OK\n")
            return subprocess.CompletedProcess(command, 0)

        with tempfile.TemporaryDirectory(prefix="spyro-runner-route-source-failure-") as output:
            with mock.patch.object(RUNNER, "CORE_CASES", ()), \
                    mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ROUTE_ADDITIONAL_CASES), \
                    mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                    mock.patch.object(RUNNER, "configured_source_image", side_effect=FileNotFoundError("missing selection")), \
                    mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                    mock.patch.object(sys, "argv", [str(SPEC.origin), "--with-source-qa"]), \
                    contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1, RUNNER.main())
            summary = json.loads((Path(output) / "summary.json").read_text())
        self.assertFalse(summary[-1]["passed"])
        self.assertIn("missing selection", summary[-1]["detail"])
        self.assertIsNone(summary[-1]["command"])
        self.assertFalse(any("--source-backed-route-surfaces" in c for c in calls))

    def test_research_cue_failure_is_logged_without_starting_candidate_command(self):
        calls = []
        name = "NativeFreeFlightResearchSmoke"

        def fake_run(command, **kwargs):
            calls.append(command)
            kwargs["stdout"].write("Offline QA passed.\n")
            return subprocess.CompletedProcess(command, 0)

        with tempfile.TemporaryDirectory(prefix="spyro-runner-research-cue-failure-") as output:
            with mock.patch.object(RUNNER, "CORE_CASES", ()), \
                    mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                    mock.patch.object(RUNNER, "SOURCE_QA_CASES", ((name, "unreachable marker", []),)), \
                    mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                    mock.patch.object(RUNNER, "configured_source_image", return_value="/selected.bin"), \
                    mock.patch.object(RUNNER, "configured_source_cue", side_effect=FileNotFoundError("missing selected CUE")), \
                    mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                    mock.patch.object(sys, "argv", [str(SPEC.origin), "--with-source-qa"]), \
                    contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1, RUNNER.main())
            summary = json.loads((Path(output) / "summary.json").read_text())
        self.assertFalse(summary[-1]["passed"])
        self.assertEqual("source-" + name, summary[-1]["name"])
        self.assertIn("missing selected CUE", summary[-1]["detail"])
        self.assertIsNone(summary[-1]["command"])
        self.assertFalse(any(c[:2] == ["dotnet", "run"] for c in calls))

    def test_expired_shared_source_deadline_does_not_launch_route_command(self):
        for expires_in in ("offline", "source-resolution"):
            calls = []
            clock = [100.0]

            def fake_run(command, **kwargs):
                calls.append(command)
                kwargs["stdout"].write("Offline QA passed.\nNATIVE_ROUTE_SURFACE_SMOKE_OK\n")
                if command[0] == "env" and expires_in == "offline":
                    clock[0] += 1801
                return subprocess.CompletedProcess(command, 0)

            def fake_source():
                if expires_in == "source-resolution":
                    clock[0] += 1801
                return "/selected.bin"

            with self.subTest(expires_in=expires_in), tempfile.TemporaryDirectory(prefix="spyro-runner-route-deadline-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ROUTE_ADDITIONAL_CASES), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", side_effect=fake_source), \
                        mock.patch.object(RUNNER.time, "monotonic", side_effect=lambda: clock[0]), \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin), "--with-source-qa"]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(1, RUNNER.main())
                summary = json.loads((Path(output) / "summary.json").read_text())
                self.assertIn("no command was started", summary[-1]["detail"])
                self.assertFalse(any("--source-backed-route-surfaces" in c for c in calls))

    def test_configured_source_image_uses_only_selected_existing_image(self):
        with tempfile.TemporaryDirectory(prefix="spyro-runner-source-settings-") as directory:
            root = Path(directory)
            settings = root / "_local/settings/source-disc.json"
            settings.parent.mkdir(parents=True)
            image = root / "selected original.bin"
            image.write_bytes(b"not a ROM; resolver only checks the selected path")
            with mock.patch.object(RUNNER, "ROOT", root):
                with self.assertRaises(FileNotFoundError):
                    RUNNER.configured_source_image()
                for value in ({"imagePath": image.name}, {"imagePath": str(image)}):
                    settings.write_text(json.dumps(value))
                    self.assertEqual(str(image), RUNNER.configured_source_image())
                spaced = root / " selected original.bin "
                spaced.write_bytes(b"distinct selected file")
                settings.write_text(json.dumps({"imagePath": spaced.name}))
                self.assertEqual(str(spaced), RUNNER.configured_source_image())
                for value in ({}, [], {"imagePath": " "}, {"imagePath": 7}):
                    settings.write_text(json.dumps(value))
                    with self.assertRaises(ValueError):
                        RUNNER.configured_source_image()
                settings.write_text("{")
                with self.assertRaises(ValueError):
                    RUNNER.configured_source_image()
                settings.write_text(json.dumps({"imagePath": "missing.bin"}))
                with self.assertRaises(FileNotFoundError):
                    RUNNER.configured_source_image()

    def test_configured_cue_never_guesses_a_sibling_or_selected_path(self):
        with tempfile.TemporaryDirectory(prefix="spyro-runner-cue-settings-") as directory:
            root = Path(directory)
            settings = root / "_local/settings/source-disc.json"
            settings.parent.mkdir(parents=True)
            cue = root / " chosen original.cue "
            cue.write_text("resolver fixture only")
            with mock.patch.object(RUNNER, "ROOT", root):
                for value in ({"cuePath": cue.name}, {"cuePath": str(cue)}):
                    settings.write_text(json.dumps(value))
                    self.assertEqual(str(cue), RUNNER.configured_source_cue())
                for value in ({}, [], {"cuePath": " "}, {"cuePath": 7}, {"selectedPath": str(cue)},
                              {"imagePath": str(cue.with_suffix('.bin'))}):
                    settings.write_text(json.dumps(value))
                    with self.assertRaises(ValueError):
                        RUNNER.configured_source_cue()
                settings.write_text(json.dumps({"cuePath": "missing.cue"}))
                with self.assertRaises(FileNotFoundError):
                    RUNNER.configured_source_cue()


if __name__ == "__main__":
    unittest.main()
