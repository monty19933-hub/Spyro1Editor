"""Source-free process ownership tests; no .NET builds or disc images."""

import importlib.util
import contextlib
import io
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


class RunnerOptionsTests(unittest.TestCase):
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


if __name__ == "__main__":
    unittest.main()
