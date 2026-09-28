"""Source-free process ownership tests; no .NET builds or disc images."""

import ast
import hashlib
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
    def test_denied_cleanup_retains_timeout_and_never_claims_stopped(self):
        process = mock.Mock(pid=123456, returncode=None)
        expired = subprocess.TimeoutExpired(["fixture"], 0.1)
        process.wait.side_effect = [expired, 0]
        with mock.patch.object(RUNNER.subprocess, "Popen", return_value=process), \
                mock.patch.object(RUNNER.os, "killpg", side_effect=PermissionError(1, "denied")) as signals, \
                mock.patch.object(RUNNER.time, "sleep"):
            with self.assertRaisesRegex(OSError, "timeout=0.1s; owned process group 123456 termination unverified") as caught:
                RUNNER.run_owned_command(["fixture"], cwd=".", stdout=None, timeout=0.1, termination_grace=0)
        self.assertIs(caught.exception.__cause__, expired)
        self.assertIn("SIGTERM", str(caught.exception))
        self.assertIn("SIGKILL", str(caught.exception))
        self.assertNotIn("group stopped", str(caught.exception))
        self.assertEqual([mock.call(123456, RUNNER.signal.SIGTERM), mock.call(123456, RUNNER.signal.SIGKILL)], signals.call_args_list)
        self.assertEqual([mock.call(timeout=0.1), mock.call(timeout=1.0)], process.wait.call_args_list)

    def test_cleanup_wait_is_bounded_even_if_group_signals_do_not_stop_leader(self):
        process = mock.Mock(pid=123456)
        process.wait.side_effect = subprocess.TimeoutExpired(["fixture"], 1)
        with mock.patch.object(RUNNER.os, "killpg"), mock.patch.object(RUNNER.time, "sleep"):
            with self.assertRaisesRegex(OSError, "leader did not exit"):
                RUNNER._stop_owned_process_group(process, 0)
        process.wait.assert_called_once_with(timeout=1.0)

    def test_interrupt_survives_denied_cleanup_with_recovery_note(self):
        process = mock.Mock(pid=123456, returncode=None)
        interrupted = KeyboardInterrupt()
        process.wait.side_effect = [interrupted, 0]
        with mock.patch.object(RUNNER.subprocess, "Popen", return_value=process), \
                mock.patch.object(RUNNER.os, "killpg", side_effect=PermissionError(1, "denied")), \
                mock.patch.object(RUNNER.time, "sleep"):
            with self.assertRaises(KeyboardInterrupt) as caught:
                RUNNER.run_owned_command(["fixture"], cwd=".", stdout=None, timeout=1, termination_grace=0)
        self.assertIs(caught.exception, interrupted)
        self.assertIn("termination unverified", interrupted.__notes__[0])

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


class SourceOracleHelperPinTests(unittest.TestCase):
    LOADER = "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_variable_native_loader_oracle.py"
    RESOURCE = "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_fresh_wizard_resource_oracle.py"
    HELPER_DICTIONARIES = (
        "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_fresh_wizard_collider_lifetime_oracle.py",
        RESOURCE,
    )
    HELPER_BINDINGS = (
        ("src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_fresh_wizard_deferred_damage_oracle.py",
         "MACHINE_PATH", "MACHINE_SHA", LOADER),
        ("src/Spyro.Editor.NativeId65FreshWizardSceneSmoke/source_p1_scene_oracle.py",
         "HELPER", "HELPER_SHA", LOADER),
        ("src/Spyro.Editor.NativeId65FreshWizardFallbackResourceSmoke/source_fallback_resource_oracle.py",
         "RESOURCE", "RESOURCE_SHA", RESOURCE),
        ("src/Spyro.Editor.NativeId65FreshWizardStagingSmoke/source_fresh_wizard_staging_oracle.py",
         "RESOURCE_HELPER", "RESOURCE_SHA", RESOURCE),
    )

    def literal_assignment(self, tree, name):
        assignments = [node for node in tree.body if isinstance(node, ast.Assign)
                       and any(isinstance(target, ast.Name) and target.id == name for target in node.targets)]
        self.assertEqual(1, len(assignments), f"Expected one top-level literal {name} assignment")
        self.assertEqual(1, len(assignments[0].targets), f"Expected a standalone {name} binding")
        try:
            return ast.literal_eval(assignments[0].value)
        except (ValueError, TypeError, SyntaxError) as error:
            self.fail(f"{name} must remain a static literal: {error}")

    def assert_source_hash(self, relative, expected):
        self.assertIsInstance(relative, str)
        self.assertIsInstance(expected, str)
        self.assertRegex(expected, r"\A[0-9a-f]{64}\Z")
        path = Path(relative)
        self.assertFalse(path.is_absolute(), "Helper must be repository-relative")
        self.assertNotIn("..", path.parts, "Helper cannot escape the source tree")
        self.assertEqual("src", path.parts[0])
        self.assertIn(path.suffix, (".py", ".cs"), "Only code files may be hashed")
        source = (RUNNER.ROOT / path).resolve()
        self.assertTrue(source.is_relative_to((RUNNER.ROOT / "src").resolve()),
                        "Helper must resolve inside the source tree")
        self.assertEqual(expected, hashlib.sha256(source.read_bytes()).hexdigest(),
                         f"Stale source helper pin: {relative}")

    def test_literal_helper_dictionaries_match_complete_source_hashes(self):
        # Parse declarations only: never import an oracle, call its helpers, or
        # resolve/read a disc image. Include every pin in both dictionaries.
        for relative in self.HELPER_DICTIONARIES:
            with self.subTest(oracle=relative):
                tree = ast.parse((RUNNER.ROOT / relative).read_text(), filename=relative)
                helpers = self.literal_assignment(tree, "HELPERS")
                self.assertIsInstance(helpers, dict)
                self.assertIn(self.LOADER, helpers, "Native loader pin must remain present")
                for helper, expected in helpers.items():
                    with self.subTest(helper=helper):
                        self.assert_source_hash(helper, expected)

    def test_named_helper_path_hash_bindings_match_complete_source_hashes(self):
        for relative, path_name, hash_name, expected_path in self.HELPER_BINDINGS:
            with self.subTest(oracle=relative, binding=path_name):
                tree = ast.parse((RUNNER.ROOT / relative).read_text(), filename=relative)
                helper = self.literal_assignment(tree, path_name)
                self.assertEqual(expected_path, helper, "Pinned helper identity must remain exact")
                self.assert_source_hash(helper, self.literal_assignment(tree, hash_name))


class ObserverPrebuildTests(unittest.TestCase):
    EXPECTED = (
        ("python-fresh-chest-observer", ("tools/Observe-SpyroFreshChest.py", "--self-test"),
         "FRESH_CHEST_OBSERVER_OFFLINE_OK tests=13 connections=0 romReads=0 runtime=false"),
        ("python-tiled-family-chest-observer",
         ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_tiled_family_chest_observer.py", "-v"),
         "Ran 9 tests"),
        ("python-japanese-object-contracts", ("tools/Test-Inspect-SpyroJapaneseObjects.py", "-v"),
         "Ran 9 tests"),
        ("python-japanese-collectible-contracts", ("tools/test_japanese_collectibles.py", "-v"),
         "Ran 12 tests"),
        ("python-japanese-font-contracts", ("tools/test_japanese_font.py", "-v"),
         "Ran 7 tests"),
        ("python-japanese-sign-reference-verifier", ("tools/Test-Verify-SpyroJapaneseSignReference.py", "-v"),
         "Ran 12 tests"),
        ("python-usa-modal-texture-lifetime", ("tools/test_usa_modal_texture_lifetime.py", "-v"),
         "Ran 9 tests"),
        ("python-usa-save-buffer-ownership", ("tools/test_usa_save_buffer_ownership.py", "-v"),
         "Ran 10 tests"),
        ("python-usa-selected-save-admission", ("tools/test_usa_selected_save_admission.py", "-v"),
         "Ran 12 tests"),
        ("python-usa-card-read-completion", ("tools/test_usa_card_read_completion.py", "-v"),
         "Ran 19 tests"),
        ("python-usa-pete-lifetime", ("tools/test_usa_pete_lifetime.py", "-v"),
         "Ran 11 tests"),
        ("python-japanese-egg-observer", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_japanese_egg_observer.py", "-v"),
         "Ran 10 tests"),
        ("python-japanese-egg-authored-observer", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_japanese_egg_authored_observer.py", "-v"),
         "Ran 16 tests"),
        ("python-collectible-startup-observer", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_collectible_startup_observer.py", "-v"),
         "Ran 8 tests"),
        ("python-collectible-startup-physical", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_collectible_startup_physical.py", "-v"),
         "Ran 14 tests"),
        ("python-collectible-card-fixture", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_collectible_card_fixture.py", "-v"),
         "Ran 8 tests"),
        ("python-collectible-card-load-physical", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_collectible_card_load_physical.py", "-v"),
         "Ran 20 tests"),
        ("python-collectible-card-load-observer", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_collectible_card_load_observer.py", "-v"),
         "Ran 9 tests"),
        ("python-collectible-card-admission-physical", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_collectible_card_admission_physical.py", "-v"),
         "Ran 17 tests"),
        ("python-collectible-card-admission-observer", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_collectible_card_admission_observer.py", "-v"),
         "Ran 9 tests"),
        ("python-admitted-egg-lifecycle-physical", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_japanese_egg_admitted_lifecycle_physical.py", "-v"),
         "Ran 14 tests"),
        ("python-admitted-egg-lifecycle-observer", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_japanese_egg_admitted_lifecycle_observer.py", "-v"),
         "Ran 12 tests"),
        ("python-japanese-egg-physical-verifier", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_japanese_egg_research.py", "-v"),
         "Ran 29 tests"),
    )

    def test_exact_mandatory_python_only_observer_registry(self):
        self.assertEqual(self.EXPECTED, RUNNER.OBSERVER_PYTHON_CASES)
        self.assertFalse(any(name in dict(RUNNER.CORE_CASES) for name, _, _ in self.EXPECTED))

    def test_actual_new_content_prebuild_markers_match_registered_counts(self):
        # An independently copied registry assertion cannot detect two stale
        # count strings. Execute these small source-free suites as well.
        for name, arguments, marker in self.EXPECTED[2:]:
            with self.subTest(name=name):
                result = subprocess.run([sys.executable, "-B", *arguments], cwd=TOOLS.parent,
                                        capture_output=True, text=True, timeout=30, check=False)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertEqual(1, (result.stdout + result.stderr).count(marker))

    def test_observer_order_markers_failure_gates_and_default_source_isolation(self):
        scenarios = {"success": (0, len(self.EXPECTED) + 1), "runner-failed": (1, 1)}
        for index in range(len(self.EXPECTED)):
            scenarios[f"missing-marker-{index}"] = (1, index + 2)
            scenarios[f"nonzero-{index}"] = (1, index + 2)
        for opted_in in (False, True):
            for scenario, (status, python_count) in scenarios.items():
                calls = []
                def fake_run(command, **kwargs):
                    calls.append((command, kwargs["timeout"]))
                    if len(calls) == 1 and scenario == "runner-failed":
                        return subprocess.CompletedProcess(command, 1)
                    for index, (_, arguments, marker) in enumerate(self.EXPECTED):
                        if command == [sys.executable, "-B", *arguments]:
                            kwargs["stdout"].write("Ran 0 tests\nOK\n" if scenario == f"missing-marker-{index}" else marker + "\n")
                            if scenario == f"nonzero-{index}":
                                return subprocess.CompletedProcess(command, 7)
                    if command[0] == "env": kwargs["stdout"].write("Offline QA passed.\n")
                    return subprocess.CompletedProcess(command, 0)
                with self.subTest(opted_in=opted_in, scenario=scenario), \
                        tempfile.TemporaryDirectory(prefix="spyro-observer-prebuild-") as output:
                    with mock.patch.object(RUNNER, "CORE_CASES", ()), \
                            mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                            mock.patch.object(RUNNER, "SOURCE_QA_CASES", ()), \
                            mock.patch.object(RUNNER, "SOURCE_PYTHON_CASES", ()), \
                            mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                            mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("observer source read")), \
                            mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("observer CUE read")), \
                            mock.patch("builtins.input", side_effect=AssertionError("observer requested input")), \
                            mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                            mock.patch.object(sys, "argv", [str(SPEC.origin), "--timeout", "123",
                                *(["--with-source-qa", "--source-qa-timeout", "456"] if opted_in else [])]), \
                            contextlib.redirect_stdout(io.StringIO()):
                        self.assertEqual(status, RUNNER.main())
                    summary = json.loads((Path(output) / "summary.json").read_text())
                self.assertEqual("python-runner-tests", summary[0]["name"])
                self.assertEqual(["python-runner-tests", *[name for name, _, _ in self.EXPECTED]][:python_count],
                                 [row["name"] for row in summary[:python_count]])
                for index, (_, arguments, _) in enumerate(self.EXPECTED[:python_count - 1], 1):
                    self.assertEqual(([sys.executable, "-B", *arguments], 123), calls[index])
                self.assertEqual(python_count, sum(command[0] == sys.executable for command, _ in calls))
                if scenario == "success":
                    self.assertEqual("build-Spyro.Editor.App", summary[python_count]["name"])
                    self.assertEqual(["dotnet", "build"], calls[python_count][0][:2])
                    self.assertEqual(123, calls[python_count][1])
                    self.assertTrue(all(row["passed"] for row in summary))
                    if opted_in:
                        self.assertEqual("source-offline-qa", summary[python_count + 1]["name"])
                        self.assertEqual(456, calls[python_count + 1][1])
                else:
                    self.assertEqual(python_count, len(calls))
                    self.assertFalse(summary[-1]["passed"])
                    self.assertFalse(any(command[0] in ("dotnet", "env") for command, _ in calls))
                    if scenario.startswith("missing-marker-"):
                        self.assertEqual("missing success marker", summary[-1]["detail"])

    def test_list_never_executes_observers_or_resolves_source_or_creates_logs(self):
        for arguments in (["--list"], ["--list", "--with-source-qa"]):
            with self.subTest(arguments=arguments), \
                    mock.patch.object(sys, "argv", [str(SPEC.origin), *arguments]), \
                    mock.patch.object(RUNNER, "run_owned_command", side_effect=AssertionError("list ran command")), \
                    mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("list source read")), \
                    mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("list CUE read")), \
                    mock.patch.object(RUNNER.tempfile, "mkdtemp", side_effect=AssertionError("list created logs")), \
                    mock.patch("builtins.input", side_effect=AssertionError("list requested input")), \
                    contextlib.redirect_stdout(io.StringIO()) as output:
                self.assertEqual(0, RUNNER.main())
            self.assertNotIn("FRESH_CHEST_OBSERVER_OFFLINE_OK", output.getvalue())
            self.assertNotIn("Ran 9 tests", output.getvalue())


class SourcePythonOracleTests(unittest.TestCase):
    def setUp(self):
        # Source-only fake commands deliberately omit offline observer output;
        # ObserverPrebuildTests separately covers the mandatory real registry.
        patcher = mock.patch.object(RUNNER, "OBSERVER_PYTHON_CASES", ())
        patcher.start()
        self.addCleanup(patcher.stop)

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
        ("fresh-textured-disc", "src/Spyro.Editor.NativeId65TexturedDiscSmoke/source_textured_disc_oracle.py",
         "NATIVE_ID65_TEXTURED_DISC_RAW_ORACLE_OK"),
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
        observer_patcher = mock.patch.object(RUNNER, "OBSERVER_PYTHON_CASES", ())
        observer_patcher.start()
        self.addCleanup(observer_patcher.stop)

    def test_aggregate_session_is_a_separate_sourcefree_ui_mode(self):
        name = "UiSmoke"
        marker, flag = "FRESH_LEVEL_DOCUMENT_SESSION_OK", "--fresh-native-level-session-only"
        self.assertEqual([(name, marker, [flag])],
                         [case for case in RUNNER.ADDITIONAL_CORE_CASES if case[0] == name and case[2] == [flag]])
        self.assertEqual(["--fresh-native-document-only"], RUNNER.CASE_ARGUMENTS[name])
        self.assertFalse(any(case[0] == name and case[2] == [flag] for case in RUNNER.SOURCE_QA_CASES))
        with mock.patch.object(sys, "argv", [str(SPEC.origin), "--list"]), \
                mock.patch.object(RUNNER, "run_owned_command", side_effect=AssertionError("list ran command")), \
                mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("source read")), \
                mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("CUE read")), \
                contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(0, RUNNER.main())
        lines = [line for line in output.getvalue().splitlines() if "Spyro.Editor.UiSmoke/" in line]
        self.assertEqual(8, len(lines))
        self.assertTrue(any(flag + " | " + marker in line for line in lines))

    def test_aggregate_ui_is_an_additional_isolated_mode_not_a_source_or_default_alias(self):
        name = "UiSmoke"
        expected = [(name, "FREE_FLIGHT_UI_OK", ["--free-flight-ui-only"]),
                    (name, "FRESH_LEVEL_DOCUMENT_SESSION_OK", ["--fresh-native-level-session-only"]),
                    (name, "FRESH_NATIVE_LEVEL_UI_OK", ["--fresh-native-level-ui-only"]),
                    (name, "FRESH_MATERIAL_PICKER_UI_OK", ["--fresh-material-picker-only"]),
                    (name, "FRESH_MATERIAL_INSPECTION_WORKER_FREE_OK", ["--fresh-material-inspection-worker-only"]),
                    (name, "FRESH_MATERIAL_INSPECTION_UI_OK", ["--fresh-material-inspection-only"]),
                    (name, "FRESH_SAVED_RESEARCH_UI_OK", ["--fresh-saved-research-ui"])]
        self.assertEqual(expected, [case for case in RUNNER.ADDITIONAL_CORE_CASES if case[0] == name])
        self.assertEqual(["--fresh-native-document-only"], RUNNER.CASE_ARGUMENTS[name])
        self.assertFalse(any(case[0] == name and case[2] in [args for _, _, args in expected]
                             for case in RUNNER.SOURCE_QA_CASES))
        with mock.patch.object(sys, "argv", [str(SPEC.origin), "--list"]), \
                mock.patch.object(RUNNER, "run_owned_command", side_effect=AssertionError("list ran command")), \
                mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("source read")), \
                mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("CUE read")), \
                contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(0, RUNNER.main())
        lines = [line for line in output.getvalue().splitlines() if "Spyro.Editor.UiSmoke/" in line]
        self.assertEqual(8, len(lines))
        for _, marker, arguments in expected:
            self.assertTrue(any(arguments[0] + " | " + marker in line for line in lines))

    def test_material_picker_has_its_own_sourcefree_marker_and_result(self):
        name, old_marker = "UiSmoke", "FRESH_NATIVE_DOCUMENT_UI_OK"
        marker, flag = "FRESH_MATERIAL_PICKER_UI_OK", "--fresh-material-picker-only"
        self.assertEqual([(name, marker, [flag])],
                         [case for case in RUNNER.ADDITIONAL_CORE_CASES if case[2] == [flag]])
        self.assertFalse(any(case[0] == name and case[2] == [flag] for case in RUNNER.SOURCE_QA_CASES))
        for emit_marker in (False, True):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                if command[:2] == ["dotnet", "run"]:
                    kwargs["stdout"].write(marker if flag in command and emit_marker else old_marker)
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(emit_marker=emit_marker), tempfile.TemporaryDirectory(prefix="spyro-material-picker-runner-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ((name, old_marker),)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ((name, marker, [flag]),)), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", ()), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("source read")), \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("CUE read")), \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin)]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if emit_marker else 1, RUNNER.main())
                summary = json.loads((Path(output) / "summary.json").read_text())
            self.assertEqual(2, sum(command[:2] == ["dotnet", "build"] for command in calls))
            runs = [command for command in calls if command[:2] == ["dotnet", "run"]]
            self.assertEqual([["--fresh-native-document-only"], [flag]],
                             [command[command.index("--") + 1:] for command in runs])
            self.assertTrue(all("--no-build" in command and "--no-restore" in command for command in runs))
            self.assertEqual(["run-Spyro.Editor.UiSmoke", "run-Spyro.Editor.UiSmoke-fresh-material-picker"],
                             [row["name"] for row in summary[-2:]])
            self.assertEqual(len(summary), len({row["name"] for row in summary}))
            self.assertEqual(len(summary), len({row["log"] for row in summary}))
            self.assertTrue(all(row["passed"] for row in summary[:-1]))
            self.assertEqual(emit_marker, summary[-1]["passed"])
            if not emit_marker:
                self.assertEqual("missing success marker", summary[-1]["detail"])

    def test_saved_research_ui_requires_own_marker_and_never_registers_physical_output(self):
        name, old = "UiSmoke", "FRESH_NATIVE_DOCUMENT_UI_OK"
        marker, flag = "FRESH_SAVED_RESEARCH_UI_OK", "--fresh-saved-research-ui"
        self.assertEqual([(name, marker, [flag])], [c for c in RUNNER.ADDITIONAL_CORE_CASES if c[2] == [flag]])
        for _, _, args in (*RUNNER.ADDITIONAL_CORE_CASES, *RUNNER.SOURCE_QA_CASES):
            self.assertNotIn("--fresh-saved-research-publish-reviewed", args)
        self.assertFalse(any(c[2] == [flag] for c in RUNNER.SOURCE_QA_CASES))
        for present in (False, True):
            calls = []
            def fake_run(command, **kwargs):
                calls.append(command)
                if command[:2] == ["dotnet", "run"]:
                    kwargs["stdout"].write(marker if flag in command and present else old)
                return subprocess.CompletedProcess(command, 0)
            with self.subTest(present=present), tempfile.TemporaryDirectory(prefix="spyro-saved-ui-runner-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ((name, old),)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ((name, marker, [flag]),)), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", ()), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("source read")), \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("CUE read")), \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin)]), contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if present else 1, RUNNER.main())
                summary = json.loads((Path(output) / "summary.json").read_text())
            self.assertEqual(2, sum(c[:2] == ["dotnet", "build"] for c in calls))
            runs = [c for c in calls if c[:2] == ["dotnet", "run"]]
            self.assertEqual([["--fresh-native-document-only"], [flag]], [c[c.index("--") + 1:] for c in runs])
            self.assertTrue(all("--no-build" in c and "--no-restore" in c for c in runs))
            self.assertEqual(["run-Spyro.Editor.UiSmoke", "run-Spyro.Editor.UiSmoke-fresh-saved-research"],
                             [row["name"] for row in summary[-2:]])
            self.assertEqual(len(summary), len({row["name"] for row in summary}))
            self.assertEqual(len(summary), len({row["log"] for row in summary}))
            self.assertEqual(present, summary[-1]["passed"])
            if not present: self.assertEqual("missing success marker", summary[-1]["detail"])

    def test_saved_family_source_modes_keep_independent_markers_and_never_publish(self):
        name = "NativeFreshSavedLevelResearchSmoke"
        expected = (
            (name, "NATIVE_FRESH_SAVED_LEVEL_RESEARCH_SOURCE_OK", ["--source"]),
            (name, "NATIVE_FRESH_SAVED_CHEST_SOURCE_FINAL_OK", ["--locked-chest-source"]),
            (name, "NATIVE_FRESH_SAVED_TILED_FAMILY_CANDIDATE_FINAL_OK", ["--tiled-family-candidate-source"]),
        )
        self.assertEqual(list(expected), [c for c in RUNNER.SOURCE_QA_CASES if c[0] == name])
        self.assertIn(name, RUNNER.SOURCE_QA_CUE_CASES)
        self.assertNotIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)
        for _, _, arguments in (*RUNNER.ADDITIONAL_CORE_CASES, *RUNNER.SOURCE_QA_CASES):
            self.assertFalse(any("physical" in arg or "publish-reviewed" in arg for arg in arguments))
        for opted_in, missing in ((False, None), (True, None), *((True, c[1]) for c in expected)):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                kwargs["stdout"].write("Offline QA passed.\n")
                if command[:2] == ["dotnet", "run"]:
                    selected = next(marker for _, marker, flags in expected if flags[0] in command)
                    kwargs["stdout"].write("\n".join(marker for _, marker, _ in expected
                                                     if marker != missing or marker != selected))
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(opted_in=opted_in, missing=missing), tempfile.TemporaryDirectory(prefix="spyro-family-source-runner-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", expected), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", return_value="/selected original.bin") as source, \
                        mock.patch.object(RUNNER, "configured_source_cue", return_value="/selected original.cue") as cue, \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin), *(["--with-source-qa"] if opted_in else [])]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if missing is None else 1, RUNNER.main())
                summary = json.loads((Path(output) / "summary.json").read_text())
            count = 0 if not opted_in else next((i + 1 for i, c in enumerate(expected) if c[1] == missing), len(expected))
            self.assertEqual(count, source.call_count)
            self.assertEqual(count, cue.call_count)
            runs = [c for c in calls if c[:2] == ["dotnet", "run"]]
            self.assertEqual([[*flags, "/selected original.bin", "/selected original.cue"]
                              for _, _, flags in expected[:count]], [c[c.index("--") + 1:] for c in runs])
            self.assertEqual(["source-" + name + suffix for suffix in
                              ("", "-locked-chest", "-tiled-family-candidate")[:count]],
                             [row["name"] for row in summary if row["name"].startswith("source-" + name)])
            self.assertEqual(len(summary), len({row["log"] for row in summary}))
            if missing is not None:
                self.assertEqual("missing success marker", summary[-1]["detail"])

    def test_material_inspection_worker_has_isolated_free_and_opt_in_source_modes(self):
        name, legacy = "UiSmoke", "FRESH_NATIVE_DOCUMENT_UI_OK"
        free, free_flag = "FRESH_MATERIAL_INSPECTION_WORKER_FREE_OK", "--fresh-material-inspection-worker-only"
        source_marker, source_flag = "FRESH_MATERIAL_INSPECTION_WORKER_SOURCE_OK", "--fresh-material-inspection-worker-source"
        self.assertEqual([(name, free, [free_flag])],
                         [c for c in RUNNER.ADDITIONAL_CORE_CASES if c[2] == [free_flag]])
        self.assertEqual([(name, source_marker, [source_flag])],
                         [c for c in RUNNER.SOURCE_QA_CASES if c[0] == name and c[2] == [source_flag]])
        self.assertIn(name, RUNNER.SOURCE_QA_CUE_CASES)
        self.assertNotIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)
        self.assertEqual(["--fresh-native-document-only"], RUNNER.CASE_ARGUMENTS[name])
        for opted_in, free_ok, source_ok in ((False, True, True), (True, True, True),
                                           (True, False, True), (True, True, False)):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                kwargs["stdout"].write("Offline QA passed.\n")
                if command[:2] == ["dotnet", "run"]:
                    marker = (source_marker if source_ok else free) if source_flag in command else \
                             (free if free_ok else legacy) if free_flag in command else legacy
                    kwargs["stdout"].write(marker + "\n")
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(opted_in=opted_in, free_ok=free_ok, source_ok=source_ok), \
                    tempfile.TemporaryDirectory(prefix="spyro-worker-runner-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ((name, legacy),)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ((name, free, [free_flag]),)), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", ((name, source_marker, [source_flag]),)), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", return_value="/selected original.bin") as source, \
                        mock.patch.object(RUNNER, "configured_source_cue", return_value="/selected original.cue") as cue, \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin), *(["--with-source-qa"] if opted_in else [])]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if free_ok and (not opted_in or source_ok) else 1, RUNNER.main())
                    self.assertEqual(int(opted_in and free_ok), source.call_count)
                    self.assertEqual(int(opted_in and free_ok), cue.call_count)
                summary = json.loads((Path(output) / "summary.json").read_text())
            self.assertEqual(2, sum(c[:2] == ["dotnet", "build"] for c in calls))
            runs = [c for c in calls if c[:2] == ["dotnet", "run"]]
            self.assertEqual([["--fresh-native-document-only"], [free_flag]] +
                             ([[source_flag, "/selected original.bin", "/selected original.cue"]]
                              if opted_in and free_ok else []),
                             [c[c.index("--") + 1:] for c in runs])
            for command in runs:
                self.assertIn("--no-build", command)
                self.assertIn("--no-restore", command)
                self.assertFalse(any("publish" in arg or "reviewed" in arg or "verify" in arg for arg in command))
            self.assertTrue(any(row["name"] == "run-Spyro.Editor.UiSmoke-fresh-material-inspection-worker" for row in summary))
            self.assertEqual(len(summary), len({row["name"] for row in summary}))
            self.assertEqual(len(summary), len({row["log"] for row in summary}))
            if not free_ok:
                self.assertFalse(any(c[0] == "env" for c in calls))
            if not free_ok or opted_in and not source_ok:
                self.assertEqual("missing success marker", summary[-1]["detail"])

    def test_material_inspection_ui_never_aliases_worker_source_marker_or_log(self):
        name, legacy = "UiSmoke", "FRESH_NATIVE_DOCUMENT_UI_OK"
        free, flag = "FRESH_MATERIAL_INSPECTION_UI_OK", "--fresh-material-inspection-only"
        worker = (name, "FRESH_MATERIAL_INSPECTION_WORKER_SOURCE_OK", ["--fresh-material-inspection-worker-source"])
        ui = (name, "FRESH_MATERIAL_INSPECTION_SOURCE_UI_OK", ["--fresh-material-inspection-source"])
        self.assertEqual([(name, free, [flag])], [c for c in RUNNER.ADDITIONAL_CORE_CASES if c[2] == [flag]])
        self.assertEqual([worker, ui], [c for c in RUNNER.SOURCE_QA_CASES if c[0] == name])
        self.assertIn(name, RUNNER.SOURCE_QA_CUE_CASES)
        self.assertNotIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)
        for opted_in, missing in ((False, None), (True, None), (True, free), (True, worker[1]), (True, ui[1])):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                kwargs["stdout"].write("Offline QA passed.\n")
                if command[:2] == ["dotnet", "run"]:
                    marker = next((m for _, m, a in (worker, ui) if a[0] in command),
                                  free if flag in command else legacy)
                    # Every other marker is present: only the exact mode can pass.
                    kwargs["stdout"].write("\n".join(m for m in (legacy, free, worker[1], ui[1])
                                                     if m != marker or m != missing))
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(opted_in=opted_in, missing=missing), tempfile.TemporaryDirectory(prefix="spyro-inspection-ui-runner-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ((name, legacy),)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ((name, free, [flag]),)), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", (worker, ui)), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", return_value="/selected original.bin") as source, \
                        mock.patch.object(RUNNER, "configured_source_cue", return_value="/selected original.cue") as cue, \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin), *(["--with-source-qa"] if opted_in else [])]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if missing is None else 1, RUNNER.main())
                summary = json.loads((Path(output) / "summary.json").read_text())
            self.assertEqual(2, sum(c[:2] == ["dotnet", "build"] for c in calls))
            expected_source = [] if not opted_in or missing == free else [worker] if missing == worker[1] else [worker, ui]
            runs = [c for c in calls if c[:2] == ["dotnet", "run"]]
            self.assertEqual([["--fresh-native-document-only"], [flag]] +
                             [[*a, "/selected original.bin", "/selected original.cue"] for _, _, a in expected_source],
                             [c[c.index("--") + 1:] for c in runs])
            self.assertEqual(len(expected_source), source.call_count)
            self.assertEqual(len(expected_source), cue.call_count)
            self.assertEqual(len(summary), len({row["name"] for row in summary}))
            self.assertEqual(len(summary), len({row["log"] for row in summary}))
            self.assertTrue(any(row["name"] == "run-Spyro.Editor.UiSmoke-fresh-material-inspection" for row in summary))
            if expected_source:
                self.assertTrue(any(row["name"] == "source-UiSmoke" for row in summary))
            if len(expected_source) == 2:
                self.assertEqual("source-UiSmoke-fresh-material-inspection", summary[-1]["name"])
            if missing is not None:
                self.assertEqual("missing success marker", summary[-1]["detail"])
            for command in runs:
                self.assertIn("--no-build", command)
                self.assertIn("--no-restore", command)
                self.assertFalse(any("publish" in arg or "reviewed" in arg or "verify" in arg for arg in command))

    def test_aggregate_ui_marker_log_and_result_are_distinct_from_both_prior_ui_modes(self):
        name, old_marker = "UiSmoke", "FRESH_NATIVE_DOCUMENT_UI_OK"
        flight_marker, flight_flag = "FREE_FLIGHT_UI_OK", "--free-flight-ui-only"
        session_marker, session_flag = "FRESH_LEVEL_DOCUMENT_SESSION_OK", "--fresh-native-level-session-only"
        marker, flag = "FRESH_NATIVE_LEVEL_UI_OK", "--fresh-native-level-ui-only"
        for emit_marker in (False, True):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                if command[:2] == ["dotnet", "run"]:
                    emitted = flight_marker if flight_flag in command else marker if flag in command and emit_marker else session_marker if flag in command or session_flag in command else old_marker
                    kwargs["stdout"].write(emitted)
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(emit_marker=emit_marker), tempfile.TemporaryDirectory(prefix="spyro-aggregate-ui-runner-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ((name, old_marker),)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ((name, flight_marker, [flight_flag]), (name, session_marker, [session_flag]), (name, marker, [flag]))), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", ()), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("source read")), \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("CUE read")), \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin)]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if emit_marker else 1, RUNNER.main())
                summary = json.loads((Path(output) / "summary.json").read_text())
            self.assertEqual(2, sum(command[:2] == ["dotnet", "build"] for command in calls), "App and UiSmoke build once each")
            runs = [command for command in calls if command[:2] == ["dotnet", "run"]]
            self.assertEqual([["--fresh-native-document-only"], [flight_flag], [session_flag], [flag]],
                             [command[command.index("--") + 1:] for command in runs])
            self.assertTrue(all("--no-build" in command and "--no-restore" in command for command in runs))
            self.assertEqual(["run-Spyro.Editor.UiSmoke", "run-Spyro.Editor.UiSmoke-free-flight-ui", "run-Spyro.Editor.UiSmoke-fresh-level-session", "run-Spyro.Editor.UiSmoke-fresh-level-ui"],
                             [row["name"] for row in summary[-4:]])
            self.assertEqual(len(summary), len({row["name"] for row in summary}))
            self.assertEqual(len(summary), len({row["log"] for row in summary}))
            self.assertTrue(all(row["passed"] for row in summary[:-1]))
            self.assertEqual(emit_marker, summary[-1]["passed"])
            if not emit_marker:
                self.assertEqual("missing success marker", summary[-1]["detail"])

    def test_aggregate_session_marker_and_result_name_do_not_alias_legacy_ui(self):
        name, old_marker = "UiSmoke", "FRESH_NATIVE_DOCUMENT_UI_OK"
        marker, flag = "FRESH_LEVEL_DOCUMENT_SESSION_OK", "--fresh-native-level-session-only"
        for emit_marker in (False, True):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                if command[:2] == ["dotnet", "run"]:
                    kwargs["stdout"].write(marker if flag in command and emit_marker else old_marker)
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(emit_marker=emit_marker), tempfile.TemporaryDirectory(prefix="spyro-session-runner-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ((name, old_marker),)), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ((name, marker, [flag]),)), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", ()), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("source read")), \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("CUE read")), \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin)]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if emit_marker else 1, RUNNER.main())
                summary = json.loads((Path(output) / "summary.json").read_text())
            self.assertEqual(2, sum(command[:2] == ["dotnet", "build"] for command in calls), "App and shared UiSmoke each build once")
            runs = [command for command in calls if command[:2] == ["dotnet", "run"]]
            self.assertEqual([["--fresh-native-document-only"], [flag]],
                             [command[command.index("--") + 1:] for command in runs])
            self.assertTrue(all("--no-build" in command and "--no-restore" in command for command in runs))
            self.assertEqual(["run-Spyro.Editor.UiSmoke", "run-Spyro.Editor.UiSmoke-fresh-level-session"],
                             [row["name"] for row in summary[-2:]])
            self.assertEqual(len(summary), len({row["name"] for row in summary}))
            self.assertEqual(len(summary), len({row["log"] for row in summary}))
            self.assertTrue(summary[-2]["passed"])
            self.assertEqual(emit_marker, summary[-1]["passed"])

    def test_variable_data_is_additional_default_sourcefree_mode_without_source_authority(self):
        name = "NativeId65FreshDocumentSmoke"
        marker, flag = "NATIVE_ID65_VARIABLE_DATA_FREE_OK", "--variable-data-free"
        self.assertEqual([(name, "NATIVE_ID65_FRESH_DOCUMENT_SMOKE_OK")],
                         [row for row in RUNNER.CORE_CASES if row[0] == name])
        self.assertEqual([(name, marker, [flag]),
                          (name, "NATIVE_ID65_TILED_RESOURCE_FREE_OK", ["--tiled-resource-free"]),
                          (name, "NATIVE_ID65_TILED_COMPILATION_FREE_OK", ["--tiled-compilation-free"]),
                          (name, "NATIVE_ID65_TILED_MATERIAL_FREE_OK", ["--tiled-material-free"]),
                          (name, "NATIVE_ID65_TILED_MATERIAL_COMPILATION_FREE_OK", ["--tiled-material-compilation-free"]),
                          (name, "NATIVE_ID65_SELECTED_TILED_MATERIAL_FREE_OK", ["--selected-tiled-material-free"])],
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
        self.assertEqual(7, len(lines))
        self.assertTrue(any(flag + " | " + marker in line for line in lines))
        self.assertTrue(any("--tiled-resource-free | NATIVE_ID65_TILED_RESOURCE_FREE_OK" in line for line in lines))
        self.assertTrue(any("--tiled-compilation-free | NATIVE_ID65_TILED_COMPILATION_FREE_OK" in line for line in lines))
        self.assertTrue(any("--tiled-material-free | NATIVE_ID65_TILED_MATERIAL_FREE_OK" in line for line in lines))
        self.assertTrue(any("--tiled-material-compilation-free | NATIVE_ID65_TILED_MATERIAL_COMPILATION_FREE_OK" in line for line in lines))
        self.assertTrue(any("--selected-tiled-material-free | NATIVE_ID65_SELECTED_TILED_MATERIAL_FREE_OK" in line for line in lines))
        self.assertFalse(any("--variable-data-source" in line or "--variable-data-red" in line for line in lines))
        self.assertFalse(any("--tiled-resource-source" in line for line in lines))
        self.assertFalse(any("--tiled-compilation-source" in line for line in lines))
        self.assertFalse(any("--tiled-material-source" in line for line in lines))
        self.assertFalse(any("--tiled-material-compilation-source" in line for line in lines))
        self.assertFalse(any("--selected-tiled-material-source" in line for line in lines))

    def test_variable_data_marker_and_result_suffix_preserve_old_mode_and_single_build(self):
        self.assert_additional_fresh_case("NATIVE_ID65_VARIABLE_DATA_FREE_OK", "--variable-data-free", "-variable-data")

    def test_tiled_resource_marker_and_result_suffix_preserve_old_mode_and_single_build(self):
        self.assert_additional_fresh_case("NATIVE_ID65_TILED_RESOURCE_FREE_OK", "--tiled-resource-free", "-tiled-resource")

    def test_tiled_compilation_marker_and_result_suffix_preserve_old_mode_and_single_build(self):
        self.assert_additional_fresh_case("NATIVE_ID65_TILED_COMPILATION_FREE_OK", "--tiled-compilation-free", "-tiled-compilation")

    def test_tiled_material_marker_and_result_suffix_preserve_old_mode_and_single_build(self):
        self.assert_additional_fresh_case("NATIVE_ID65_TILED_MATERIAL_FREE_OK", "--tiled-material-free", "-tiled-material")

    def test_tiled_material_compilation_marker_and_result_suffix_preserve_old_mode_and_single_build(self):
        self.assert_additional_fresh_case("NATIVE_ID65_TILED_MATERIAL_COMPILATION_FREE_OK", "--tiled-material-compilation-free", "-tiled-material-compilation")

    def test_selected_material_marker_and_result_suffix_preserve_old_mode_and_single_build(self):
        self.assert_additional_fresh_case("NATIVE_ID65_SELECTED_TILED_MATERIAL_FREE_OK", "--selected-tiled-material-free", "-selected-tiled-material")

    def test_selected_saved_marker_and_result_suffix_preserve_old_mode_and_single_build(self):
        self.assert_additional_fresh_case("SELECTED_TILED_MATERIAL_SAVED_FREE_OK", "--selected-tiled-material-saved-free",
                                         "-selected-tiled-material-saved", name="NativeFreshSavedLevelResearchSmoke",
                                         old_marker="NATIVE_FRESH_SAVED_LEVEL_RESEARCH_FREE_OK")

    def test_chest_behavior_is_only_explicit_free_mode_with_distinct_marker_and_log(self):
        name, marker, flag = "TownSquareLockedChestSmoke", "LOCKED_CHEST_BEHAVIOR_FREE_OK", "--behavior-free"
        self.assertEqual([(name, marker, [flag])], [row for row in RUNNER.ADDITIONAL_CORE_CASES if row[0] == name])
        self.assertFalse(any(row[0] == name for row in RUNNER.CORE_CASES), "Chest no-argument mode is source-aware and must not run")
        self.assertFalse(any(row[0] == name for row in RUNNER.SOURCE_QA_CASES))
        self.assertNotIn(name, RUNNER.CASE_ARGUMENTS)
        self.assertNotIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)
        with mock.patch.object(sys, "argv", [str(SPEC.origin), "--list"]), \
                mock.patch.object(RUNNER, "run_owned_command", side_effect=AssertionError("list executed a command")), \
                mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("default source read")), \
                mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("default CUE read")), \
                contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(0, RUNNER.main())
        lines = [line for line in output.getvalue().splitlines() if "Spyro.Editor." + name + "/" in line]
        self.assertEqual(1, len(lines))
        self.assertTrue(lines[0].endswith(flag + " | " + marker))
        self.assertNotIn("--behavior-source", output.getvalue())
        self.assert_additional_fresh_case(marker, flag, "-behavior", name=name, old_marker=None)

    def assert_additional_fresh_case(self, marker, flag, suffix, *,
                                    name="NativeId65FreshDocumentSmoke", old_marker="NATIVE_ID65_FRESH_DOCUMENT_SMOKE_OK"):
        self.assertEqual([(name, marker, [flag])], [row for row in RUNNER.ADDITIONAL_CORE_CASES if row[2] == [flag]])
        for emit_marker in (False, True):
            calls = []

            def fake_run(command, **kwargs):
                calls.append(command)
                if command[:2] == ["dotnet", "run"]:
                    kwargs["stdout"].write(marker if flag in command and emit_marker else old_marker or "wrong mode marker")
                return subprocess.CompletedProcess(command, 0)

            with self.subTest(emit_marker=emit_marker), tempfile.TemporaryDirectory(prefix="spyro-variable-data-runner-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", ((name, old_marker),) if old_marker else ()), \
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
                run_logs = [Path(row["log"]).read_text() for row in summary if row["name"].startswith("run-")]
            builds = [command for command in calls if command[:2] == ["dotnet", "build"]]
            self.assertEqual(2, len(builds), "The selected project builds once, plus App")
            runs = [command for command in calls if command[:2] == ["dotnet", "run"]]
            self.assertEqual(1 + int(old_marker is not None), len(runs))
            if old_marker:
                self.assertNotIn("--", runs[0])
                self.assertIn(old_marker, run_logs[0])
                self.assertNotIn(marker, run_logs[0], "The additional mode cannot overwrite the original log")
            self.assertEqual([flag], runs[-1][runs[-1].index("--") + 1:])
            self.assertTrue(all("--no-build" in command and "--no-restore" in command for command in runs))
            self.assertEqual((["run-Spyro.Editor." + name] if old_marker else []) + ["run-Spyro.Editor." + name + suffix],
                             [row["name"] for row in summary if row["name"].startswith("run-")])
            self.assertEqual(len(summary), len({row["name"] for row in summary}))
            self.assertEqual(len(summary), len({row["log"] for row in summary}))
            if old_marker: self.assertTrue(summary[-2]["passed"])
            self.assertEqual(emit_marker, summary[-1]["passed"], "Old marker cannot stand in for the additional regression")
            self.assertEqual(emit_marker, marker in run_logs[-1])
            if not emit_marker: self.assertEqual("missing success marker", summary[-1]["detail"])
            self.assertFalse(any(command[0] == "env" for command in calls))

    def test_fresh_ui_uses_only_the_isolated_headless_mode_and_its_own_marker(self):
        name, marker, flag = "UiSmoke", "FRESH_NATIVE_DOCUMENT_UI_OK", "--fresh-native-document-only"
        self.assertEqual([(name, marker)], [c for c in RUNNER.CORE_CASES if c[0] == name])
        self.assertEqual([flag], RUNNER.CASE_ARGUMENTS.get(name))
        self.assertFalse(any(c[0] == name and c[2] == [flag] for c in RUNNER.SOURCE_QA_CASES))
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

    def test_fresh_level_aggregate_is_one_mandatory_no_argument_neutral_case(self):
        name, marker = "NativeFreshLevelDocumentSmoke", "NATIVE_FRESH_LEVEL_DOCUMENT_SMOKE_OK"
        self.assertEqual([(name, marker)], [c for c in RUNNER.CORE_CASES if c[0] == name])
        self.assertNotIn(name, RUNNER.CASE_ARGUMENTS)
        self.assertFalse(any(c[0] == name for c in RUNNER.ADDITIONAL_CORE_CASES))
        self.assertFalse(any(c[0] == name for c in RUNNER.SOURCE_QA_CASES))
        self.assertNotIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)
        with mock.patch.object(sys, "argv", [str(SPEC.origin), "--list"]), \
                mock.patch.object(RUNNER, "run_owned_command", side_effect=AssertionError("list command")), \
                mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("source read")), \
                mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("CUE read")), \
                contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(0, RUNNER.main())
        lines = [line for line in output.getvalue().splitlines() if "Spyro.Editor." + name + "/" in line]
        self.assertEqual(1, len(lines))
        self.assertTrue(lines[0].endswith(" | " + marker))
        self.assertNotIn("--", lines[0])

    def test_fresh_level_aggregate_requires_own_marker_and_successful_exit_without_source(self):
        name, marker = "NativeFreshLevelDocumentSmoke", "NATIVE_FRESH_LEVEL_DOCUMENT_SMOKE_OK"
        registered = tuple(c for c in RUNNER.CORE_CASES if c[0] == name)
        self.assertEqual(((name, marker),), registered)
        for emitted, status, expected in ((marker, 0, True), ("NATIVE_ID65_FRESH_DOCUMENT_SMOKE_OK", 0, False), (marker, 7, False)):
            calls = []
            def fake_run(command, **kwargs):
                calls.append(command)
                is_run = command[:2] == ["dotnet", "run"]
                if is_run: kwargs["stdout"].write(emitted)
                return subprocess.CompletedProcess(command, status if is_run else 0)
            with self.subTest(emitted=emitted, status=status), tempfile.TemporaryDirectory(prefix="spyro-level-aggregate-runner-") as output:
                with mock.patch.object(RUNNER, "CORE_CASES", registered), \
                        mock.patch.object(RUNNER, "ADDITIONAL_CORE_CASES", ()), \
                        mock.patch.object(RUNNER, "SOURCE_QA_CASES", ()), \
                        mock.patch.object(RUNNER, "run_owned_command", side_effect=fake_run), \
                        mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("aggregate source read")), \
                        mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("aggregate CUE read")), \
                        mock.patch.object(RUNNER.tempfile, "mkdtemp", return_value=output), \
                        mock.patch.object(sys, "argv", [str(SPEC.origin)]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if expected else 1, RUNNER.main())
                summary = json.loads((Path(output) / "summary.json").read_text())
            self.assertEqual(2, len([c for c in calls if c[:2] == ["dotnet", "build"]]), "mandatory project plus App")
            runs = [c for c in calls if c[:2] == ["dotnet", "run"]]
            self.assertEqual(1, len(runs))
            self.assertNotIn("--", runs[0], "no source/output/compiler/publisher arguments")
            self.assertIn("--no-build", runs[0]); self.assertIn("--no-restore", runs[0])
            self.assertEqual("run-Spyro.Editor." + name, summary[-1]["name"])
            self.assertEqual(expected, summary[-1]["passed"])
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

    def test_textured_disc_modes_are_read_only_with_separate_source_and_default_markers(self):
        self.assert_texture_modes("NativeId65TexturedDiscSmoke", "NATIVE_ID65_TEXTURED_DISC")

    def test_saved_material_bridge_is_source_opt_in_without_cue_or_publication(self):
        self.assert_texture_modes("NativeFreshSavedMaterialSmoke", "NATIVE_FRESH_SAVED_MATERIAL")
        self.assertFalse(any(c[0] == "NativeFreshSavedMaterialSmoke" for c in RUNNER.ADDITIONAL_CORE_CASES))

    def test_saved_level_candidate_is_explicit_source_only_without_native_publication(self):
        name = "NativeFreshSavedLevelResearchSmoke"
        # Keep this legacy-mode test isolated. The complete family registry,
        # exact independent markers and commands have their own test above.
        with mock.patch.object(RUNNER, "SOURCE_QA_CASES", tuple(c for c in RUNNER.SOURCE_QA_CASES
                               if c[0] != name or c[2] == ["--source"])):
            self.assert_texture_modes(name, "NATIVE_FRESH_SAVED_LEVEL_RESEARCH", needs_cue=True)
        self.assertEqual([(name, "SELECTED_TILED_MATERIAL_SAVED_FREE_OK", ["--selected-tiled-material-saved-free"])],
                         [row for row in RUNNER.ADDITIONAL_CORE_CASES if row[0] == name])
        with mock.patch.object(sys, "argv", [str(SPEC.origin), "--list"]), \
                mock.patch.object(RUNNER, "run_owned_command", side_effect=AssertionError("list executed a command")), \
                mock.patch.object(RUNNER, "configured_source_image", side_effect=AssertionError("default source read")), \
                mock.patch.object(RUNNER, "configured_source_cue", side_effect=AssertionError("default CUE read")), \
                contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(0, RUNNER.main())
        lines = [line for line in output.getvalue().splitlines() if "Spyro.Editor." + name + "/" in line]
        self.assertEqual(2, len(lines))
        self.assertTrue(any("--selected-tiled-material-saved-free | SELECTED_TILED_MATERIAL_SAVED_FREE_OK" in line for line in lines))
        self.assertFalse(any("--selected-tiled-material-saved-source" in line for line in lines))
        project = RUNNER.ROOT / ("src/Spyro.Editor." + name) / ("Spyro.Editor." + name + ".csproj")
        self.assertIn('<SavedLevelProbe Condition="\'$(SavedLevelProbe)\' == \'\'">Full</SavedLevelProbe>',
                      project.read_text())

    def test_saved_level_public_boundary_is_nonpublishing_and_source_opt_in(self):
        name = "NativeFreshSavedLevelPublicSmoke"
        self.assert_texture_modes(name, "SAVED_LEVEL_PUBLIC", needs_cue=True)
        self.assertFalse(any(c[0] == name for c in RUNNER.ADDITIONAL_CORE_CASES))

    def test_textured_research_baseline_requires_bin_cue_and_never_selects_candidate_probe(self):
        name = "NativeId65TexturedResearchSmoke"
        self.assert_texture_modes(name, "NATIVE_ID65_TEXTURED_RESEARCH_BASELINE", needs_cue=True)
        self.assertFalse(any(c[0] == name for c in RUNNER.ADDITIONAL_CORE_CASES))
        project = RUNNER.ROOT / ("src/Spyro.Editor." + name) / ("Spyro.Editor." + name + ".csproj")
        self.assertIn('<TexturedResearchProbe Condition="\'$(TexturedResearchProbe)\' == \'\'">Baseline</TexturedResearchProbe>',
                      project.read_text())

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

    def test_initial_spawn_clearance_remains_explicit_read_only_source(self):
        self.assert_texture_modes("NativeFreshInitialSpawnClearanceSmoke", "INITIAL_SPAWN_CLEARANCE")

    def test_authored_wizard_default_and_source_never_materialize_candidate(self):
        self.assert_texture_modes("NativeFreshWizardAuthoringSmoke", "FRESH_WIZARD_AUTHORING", needs_cue=True)

    def test_exhaustive_issued_matrix_is_separate_from_shared_source_deadline(self):
        self.assertEqual([("NativeFreshWizardAuthoringSmoke", "FRESH_WIZARD_AUTHORING_SOURCE_OK", ["--source"])],
                         [case for case in RUNNER.SOURCE_QA_CASES if case[0] == "NativeFreshWizardAuthoringSmoke"])
        self.assertNotIn("--source-issued-matrix", repr((RUNNER.CASE_ARGUMENTS, RUNNER.ADDITIONAL_CORE_CASES,
                                                       RUNNER.SOURCE_QA_CASES, RUNNER.SOURCE_PYTHON_CASES)))
        source = (RUNNER.ROOT / "src/Spyro.Editor.NativeFreshWizardAuthoringSmoke/Program.cs").read_text()
        self.assertIn('"--source-issued-matrix"', source)
        self.assertEqual(1, source.count("await IssuedMatrixCases.Source(bin,Check);"))
        self.assertIn("if (request.IssuedMatrix)", source)
        matrix, ordinary = source.split("if (request.IssuedMatrix)", 1)[1].split("\ntry\n{", 1)
        self.assertIn("await IssuedMatrixCases.Source(bin,Check);", matrix)
        self.assertIn("SeparatedPublicationCases.FinishAsync", matrix)
        self.assertIn("return;", matrix)
        self.assertNotIn("IssuedMatrixCases.Source", ordinary)
        self.assertIn("ACTUAL_WIZARD_MATRIX_FINAL_OK", source)
        self.assertIn("FRESH_WIZARD_AUTHORING_FINAL_OK", source)
        # This is a source/registration boundary check, not a 72-row execution.

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
            for command in calls:
                self.assertFalse(any("CandidateApi" in token or "TexturedResearchProbe" in token for token in command),
                                 "Default builds/runs and explicit source QA must not select a future compile probe.")
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
        self.assertEqual(frozenset({"NativeCollectibleCardAdmissionResearchSmoke", "NativeCollectibleCardLoadResearchSmoke", "NativeCollectibleStartupResearchSmoke", "NativeFreeFlightResearchSmoke", "NativeWizardResearchSmoke", "NativeId65FreshDiscResearchSmoke", "NativeId65FreshEditedDocumentResearchSmoke", "NativeId65FreshDocumentResearchSmoke", "NativeId65FreshDocumentPublicationSmoke", "NativeTwoWizardResearchSmoke", "NativeId65FreshWizardResearchSmoke", "NativeFreshWizardAuthoringSmoke", "NativeId65TexturedResearchSmoke", "NativeFreshSavedLevelResearchSmoke", "NativeFreshSavedLevelPublicSmoke", "UiSmoke"}), RUNNER.SOURCE_QA_CUE_CASES)
        self.assertEqual({"NativeFreeFlightResearchSmoke", "NativeWizardResearchSmoke", "NativeTwoWizardResearchSmoke"},
                         RUNNER.SOURCE_QA_CUE_CASES & RUNNER.SOURCE_QA_WORKSPACE_CASES,
                         "Only these CUE consumers also inspect the configured workspace; new detached candidates must not inherit it.")

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
            "NativeSimpleMobyGeometrySmoke": ["--source", "/selected original.bin"],
            "NativeCollectibleSelectedSlotResidenceSmoke": ["--source", "/selected original.bin"],
            "NativeCollectibleReservationMechanicsSmoke": ["--source", "/selected original.bin"],
            "NativeCollectibleColdBootBootstrapSmoke": ["--source", "/selected original.bin"],
            "NativeCollectibleStartupResearchSmoke": ["--source", "/selected original.bin", "/selected original.cue"],
            "NativeCollectibleCardLoadResearchSmoke": ["--source", "/selected original.bin", "/selected original.cue"],
            "NativeCollectibleCardAdmissionResearchSmoke": ["--source", "/selected original.bin", "/selected original.cue"],
            "NativeCollectibleSaveTailSmoke": ["--source", "/selected original.bin"],
            "NativeCollectibleSaveControllerSmoke": ["--source", "/selected original.bin"],
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
            "NativeFreshInitialSpawnClearanceSmoke": ["--source", "/selected original.bin"],
            "NativeFreshWizardAuthoringSmoke": ["--source", "/selected original.bin", "/selected original.cue"],
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
            "NativeId65TexturedDiscSmoke": ["--source", "/selected original.bin"],
            "NativeId65TexturedResearchSmoke": ["--source", "/selected original.bin", "/selected original.cue"],
            "NativeFreshSavedMaterialSmoke": ["--source", "/selected original.bin"],
            "NativeFreshSavedLevelResearchSmoke": ["--source", "/selected original.bin", "/selected original.cue"],
            "NativeFreshSavedLevelPublicSmoke": ["--source", "/selected original.bin", "/selected original.cue"],
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
            arguments = command[command.index("--") + 1:]
            if name == "NativeFreshSavedLevelResearchSmoke" and arguments[0] in (
                    "--locked-chest-source", "--tiled-family-candidate-source"):
                self.assertEqual([arguments[0], "/selected original.bin", "/selected original.cue"], arguments)
            else:
                self.assertEqual(expected[name], arguments)
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


class NeutralTiledFamilyOracleTests(unittest.TestCase):
    def test_independent_reference_runs_in_default_prebuild_gate(self):
        # This separate source-free command has no ROM/path/output mode. Keep
        # the C# consumer on the ordinary source-free authoring path as well.
        script = TOOLS.parent / "src/Spyro.Editor.NativeFreshWizardAuthoringSmoke/source_tiled_family_surface_oracle.py"
        result = subprocess.run([sys.executable, str(script), "--self-test"],
                                capture_output=True, text=True, timeout=10, check=False)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        marker = "TILED_FAMILY_SURFACE_ORACLE_OK"
        self.assertEqual(1, result.stdout.count(marker))
        report = json.loads(result.stdout.split(marker)[0])
        self.assertEqual(102, report["negativeControls"])
        self.assertEqual(2615, report["canonicalBytes"])
        self.assertEqual(912, report["surfaceBytes"])
        self.assertEqual("2bc9a53da03fdc5d5c8bfe6bc11b1c46fbbdd1db7ae089c1df8a664c20e5b375",
                         report["canonicalSha256"])
        self.assertEqual("6af708ea930471156cf3e68406eef231d2ff8b195d6ca1a832a9406c731af23f",
                         report["surfaceSha256"])
        self.assertEqual([0, 0, 0, 1, 0, 1, 1, 1], report["orderedFaceBindings"])
        for flag in ("sourceOpened", "sourceCertified", "bodyIssued", "runtimeVerified", "publicationApproved"):
            self.assertIs(False, report[flag])
        program = (script.parent / "Program.cs").read_text(encoding="utf-8")
        self.assertIn("TiledFamilyTerrainIntentCases.Free(Check);", program)
        self.assertLess(program.index("TiledFamilyTerrainIntentCases.Free(Check);"),
                        program.index("if (request == null) return;"))


class RegionalAndMatrixGateTests(unittest.TestCase):
    def test_card_load_research_publication_and_faults_never_implicit_in_runner(self):
        name = "NativeCollectibleCardLoadResearchSmoke"
        self.assertEqual("COLLECTIBLE_CARD_LOAD_RESEARCH_SOURCE_FREE_OK", dict(RUNNER.CORE_CASES)[name])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
        self.assertEqual([(name, "COLLECTIBLE_CARD_LOAD_RESEARCH_SOURCE_OK", ["--source"])],
                         [case for case in RUNNER.SOURCE_QA_CASES if case[0] == name])
        self.assertIn(name, RUNNER.SOURCE_QA_CUE_CASES)

    def test_startup_research_publication_and_faults_never_implicit_in_runner(self):
        name = "NativeCollectibleStartupResearchSmoke"
        self.assertEqual("COLLECTIBLE_STARTUP_RESEARCH_SOURCE_FREE_OK", dict(RUNNER.CORE_CASES)[name])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
        self.assertEqual([(name, "COLLECTIBLE_STARTUP_RESEARCH_SOURCE_OK", ["--source"])],
                         [case for case in RUNNER.SOURCE_QA_CASES if case[0] == name])
        self.assertIn(name, RUNNER.SOURCE_QA_CUE_CASES)

    def test_admission_research_publication_and_faults_never_implicit_in_runner(self):
        name = "NativeCollectibleCardAdmissionResearchSmoke"
        self.assertEqual("COLLECTIBLE_CARD_ADMISSION_RESEARCH_SOURCE_FREE_OK", dict(RUNNER.CORE_CASES)[name])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
        self.assertEqual([(name, "COLLECTIBLE_CARD_ADMISSION_RESEARCH_SOURCE_OK", ["--source"])],
                         [case for case in RUNNER.SOURCE_QA_CASES if case[0] == name])
        self.assertIn(name, RUNNER.SOURCE_QA_CUE_CASES)

    def test_cold_boot_prototype_cannot_implicitly_open_source_or_publish(self):
        name = "NativeCollectibleColdBootBootstrapSmoke"
        self.assertEqual("COLLECTIBLE_COLD_BOOT_BOOTSTRAP_SOURCE_FREE_OK", dict(RUNNER.CORE_CASES)[name])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
        self.assertEqual([(name, "COLLECTIBLE_COLD_BOOT_BOOTSTRAP_SOURCE_OK", ["--source"])],
                         [case for case in RUNNER.SOURCE_QA_CASES if case[0] == name])
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)

    def test_reservation_mechanics_default_is_neutral_and_source_mode_explicit(self):
        name = "NativeCollectibleReservationMechanicsSmoke"
        self.assertEqual("COLLECTIBLE_RESERVATION_MECHANICS_SOURCE_FREE_OK", dict(RUNNER.CORE_CASES)[name])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
        self.assertEqual([(name, "COLLECTIBLE_RESERVATION_MECHANICS_SOURCE_OK", ["--source"])],
                         [case for case in RUNNER.SOURCE_QA_CASES if case[0] == name])
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)

    def test_selected_slot_residence_has_only_explicit_read_only_source_mode(self):
        name = "NativeCollectibleSelectedSlotResidenceSmoke"
        self.assertEqual("COLLECTIBLE_SELECTED_SLOT_RESIDENCE_SOURCE_FREE_OK", dict(RUNNER.CORE_CASES)[name])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
        self.assertEqual([(name, "COLLECTIBLE_SELECTED_SLOT_RESIDENCE_SOURCE_OK", ["--source"])],
                         [case for case in RUNNER.SOURCE_QA_CASES if case[0] == name])
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)

    def test_collection_kernel_has_no_source_card_or_publication_route(self):
        name = "NativeCollectibleCollectionKernelSmoke"
        self.assertEqual("NATIVE_COLLECTIBLE_STATE_QUERY_OK", dict(RUNNER.CORE_CASES)[name])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
        self.assertFalse(any(case[0] == name for case in RUNNER.SOURCE_QA_CASES))

    def test_ledger_hydration_and_scheduling_defaults_have_no_source_or_publication(self):
        for name, marker in (
            ("DetachedCollectibleLedgerImageSmoke", "DETACHED_COLLECTIBLE_LEDGER_IMAGE_OK"),
            ("NativeSha256Smoke", "NATIVE_SHA256_OK"),
            ("NativeCollectibleEnvelopeLoadSmoke", "NATIVE_COLLECTIBLE_ENVELOPE_LOAD_OK"),
            ("NativeCollectibleCardLoadSmoke", "NATIVE_COLLECTIBLE_CARD_LOAD_OK"),
            ("NativeCollectibleLiveAdmissionSmoke", "NATIVE_COLLECTIBLE_LIVE_ADMISSION_OK"),
            ("NativeJapaneseEggSchedulingSmoke", "JP_EGG_SCHEDULING_SOURCE_FREE_OK"),
        ):
            self.assertEqual(marker, dict(RUNNER.CORE_CASES)[name])
            self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
            self.assertFalse(any(case[0] == name for case in RUNNER.SOURCE_QA_CASES))
            self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)

    def test_required_completion_and_two_egg_gates_are_source_free(self):
        for name, marker in (
            ("DetachedCollectibleCompletionSmoke", "DETACHED_COLLECTIBLE_COMPLETION_SOURCE_FREE_OK"),
            ("NativeJapaneseEggArtisansIntegrationSmoke", "JP_EGG_ARTISANS_INTEGRATION_SOURCE_FREE_OK"),
            ("NativeJapaneseEggMemorySmoke", "JP_EGG_MEMORY_SOURCE_FREE_OK"),
            ("NativeJapaneseEggRenderAdmissionSmoke", "JP_EGG_RENDER_ADMISSION_SOURCE_FREE_OK"),
            ("NativeJapaneseEggRgbEditSmoke", "JP_EGG_RGB_EDIT_SOURCE_FREE_OK"),
            ("NativeJapaneseEggInstanceRootSmoke", "JP_EGG_INSTANCE_ROOT_SOURCE_FREE_OK"),
            ("NativeJapaneseEggRgbVariantTailSmoke", "JP_EGG_RGB_VARIANT_TAIL_SOURCE_FREE_OK"),
            ("NativeJapaneseEggPickupPredicateSmoke", "JP_EGG_PICKUP_PREDICATE_SOURCE_FREE_OK"),
            ("NativeJapaneseEggCollectionAttemptSmoke", "JP_EGG_COLLECTION_ATTEMPT_SOURCE_FREE_OK"),
            ("NativeJapaneseEggLifecycleSmoke", "JP_EGG_LIFECYCLE_SOURCE_FREE_OK"),
            ("NativeJapaneseEggLifecycleOverlaySmoke", "JP_EGG_LIFECYCLE_OVERLAY_SOURCE_FREE_OK"),
            ("NativeJapaneseEggAuthoredClassesSmoke", "JP_EGG_AUTHORED_CLASSES_SOURCE_FREE_OK"),
            ("NativeJapaneseEggAuthoredMemorySmoke", "JP_EGG_AUTHORED_MEMORY_SOURCE_FREE_OK"),
            ("NativeJapaneseEggPlacementSmoke", "JP_EGG_PLACEMENT_SOURCE_FREE_OK"),
            ("NativeJapaneseSignFontResidentSmoke", "JP_SIGN_FONT_RESIDENT_SOURCE_FREE_OK"),
            ("NativeJapaneseSignModalHookSmoke", "JAPANESE_SIGN_MODAL_HOOK_SOURCE_FREE_OK"),
            ("NativeJapaneseEggResearchSmoke", "NATIVE_JP_EGG_RESEARCH_SOURCE_FREE_OK"),
            ("NativeJapaneseEggAuthoredResearchSmoke", "NATIVE_JP_EGG_AUTHORED_RESEARCH_SOURCE_FREE_OK"),
        ):
            self.assertEqual(marker, dict(RUNNER.CORE_CASES)[name])
            self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
            self.assertFalse(any(case[0] == name for case in RUNNER.SOURCE_QA_CASES))

    def test_detached_native_save_tail_is_mandatory_with_explicit_usa_source_mode(self):
        name = "NativeCollectibleSaveTailSmoke"
        self.assertEqual("NATIVE_COLLECTIBLE_SAVE_TAIL_OK", dict(RUNNER.CORE_CASES)[name])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
        self.assertEqual([(name, "NATIVE_COLLECTIBLE_SAVE_TAIL_SOURCE_OK", ["--source"])],
                         [case for case in RUNNER.SOURCE_QA_CASES if case[0] == name])
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)
        self.assertNotIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)

    def test_detached_native_save_controller_is_mandatory_with_explicit_usa_source_mode(self):
        name = "NativeCollectibleSaveControllerSmoke"
        self.assertEqual("NATIVE_COLLECTIBLE_SAVE_CONTROLLER_OK", dict(RUNNER.CORE_CASES)[name])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
        self.assertEqual([(name, "NATIVE_COLLECTIBLE_SAVE_CONTROLLER_SOURCE_OK", ["--source"])],
                         [case for case in RUNNER.SOURCE_QA_CASES if case[0] == name])
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)
        self.assertNotIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)

    def test_detached_save_workspace_is_mandatory_but_never_receives_one_region_source(self):
        name = "NativeCollectibleSaveWorkspaceSmoke"
        self.assertEqual("COLLECTIBLE_SAVE_WORKSPACE_SOURCE_FREE_OK", dict(RUNNER.CORE_CASES)[name])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
        self.assertFalse(any(case[0] == name for case in RUNNER.SOURCE_QA_CASES))
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)
        self.assertNotIn(name, RUNNER.SOURCE_QA_WORKSPACE_CASES)

    def test_admitted_egg_lifecycle_is_mandatory_but_never_receives_one_region_source(self):
        name = "NativeJapaneseEggAdmittedLifecycleSmoke"
        self.assertEqual("JP_EGG_ADMITTED_LIFECYCLE_SOURCE_FREE_OK", dict(RUNNER.CORE_CASES)[name])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
        self.assertFalse(any(case[0] == name for case in RUNNER.SOURCE_QA_CASES))
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)

    def test_admitted_egg_publication_is_explicit_and_never_receives_one_region_source(self):
        name = "NativeJapaneseEggAdmittedLifecycleResearchSmoke"
        self.assertEqual("JP_EGG_ADMITTED_RESEARCH_SOURCE_FREE_OK", dict(RUNNER.CORE_CASES)[name])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
        self.assertFalse(any(case[0] == name for case in RUNNER.SOURCE_QA_CASES))
        self.assertFalse(any(case[0] == name for case in RUNNER.ADDITIONAL_CORE_CASES))
        self.assertNotIn(name, RUNNER.SOURCE_QA_CUE_CASES)

    def test_collectible_save_contract_has_no_native_card_or_source_route(self):
        self.assertEqual("DETACHED_COLLECTIBLE_SAVE_SOURCE_FREE_OK",
                         dict(RUNNER.CORE_CASES)["DetachedCollectibleSaveSmoke"])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get("DetachedCollectibleSaveSmoke", []))
        self.assertFalse(any(name == "DetachedCollectibleSaveSmoke" for name, _, _ in RUNNER.SOURCE_QA_CASES))

    def test_regional_model_gates_keep_two_source_cases_explicit(self):
        self.assertEqual("JP_EGG_VISUAL_BEHAVIOR_SOURCE_FREE_OK",
                         dict(RUNNER.CORE_CASES)["NativeJapaneseEggVisualBehaviorSmoke"])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get("NativeJapaneseEggVisualBehaviorSmoke", []))
        self.assertFalse(any(name == "NativeJapaneseEggVisualBehaviorSmoke" for name, _, _ in RUNNER.SOURCE_QA_CASES))
        self.assertEqual("NATIVE_SIMPLE_MOBY_GEOMETRY_SOURCE_FREE_OK",
                         dict(RUNNER.CORE_CASES)["NativeSimpleMobyGeometrySmoke"])
        self.assertEqual("JP_EGG_MODEL_ASSEMBLY_SOURCE_FREE_OK",
                         dict(RUNNER.CORE_CASES)["NativeJapaneseEggModelAssemblySmoke"])
        for name in ("NativeSimpleMobyGeometrySmoke", "NativeJapaneseEggModelAssemblySmoke"):
            self.assertEqual([], RUNNER.CASE_ARGUMENTS.get(name, []))
        self.assertEqual([("NativeSimpleMobyGeometrySmoke", "NATIVE_SIMPLE_MOBY_GEOMETRY_DISC_OK", ["--source"])],
                         [case for case in RUNNER.SOURCE_QA_CASES if case[0] == "NativeSimpleMobyGeometrySmoke"])
        self.assertFalse(any(name == "NativeJapaneseEggModelAssemblySmoke" for name, _, _ in RUNNER.SOURCE_QA_CASES))

    def test_japanese_sign_gate_is_source_free_and_does_not_reuse_usa_source_arguments(self):
        self.assertEqual("JAPANESE_SIGN_MESSAGE_SOURCE_FREE_OK", dict(RUNNER.CORE_CASES)["JapaneseSignMessageSmoke"])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get("JapaneseSignMessageSmoke", []))
        self.assertFalse(any(name == "JapaneseSignMessageSmoke" for name, _, _ in RUNNER.SOURCE_QA_CASES))

    def test_regional_source_free_gate_has_no_disc_or_publication_arguments(self):
        self.assertEqual("NATIVE_REGIONAL_SOURCE_FREE_OK", dict(RUNNER.CORE_CASES)["NativeRegionalSourceSmoke"])
        self.assertEqual([], RUNNER.CASE_ARGUMENTS.get("NativeRegionalSourceSmoke", []))
        self.assertFalse(any(name == "NativeRegionalSourceSmoke" for name, _, _ in RUNNER.SOURCE_QA_CASES))

    def test_inventory_gates_require_the_new_matrix_checks(self):
        self.assertEqual("NATIVE_MOBY_PORTABILITY_MATRIX_SMOKE_OK", dict(RUNNER.CORE_CASES)["NativeMobyInventorySmoke"])
        self.assertEqual([("NativeMobyInventorySmoke", "NATIVE_MOBY_PORTABILITY_MATRIX_SOURCE_OK", ["--source"])],
                         [case for case in RUNNER.SOURCE_QA_CASES if case[0] == "NativeMobyInventorySmoke"])


if __name__ == "__main__":
    unittest.main()
