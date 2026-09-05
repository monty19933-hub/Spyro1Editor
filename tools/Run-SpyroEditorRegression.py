#!/usr/bin/env python3
"""Repeatable source-free regression gate; never launches the editor or emulator."""

import argparse
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time


ROOT = Path(__file__).resolve().parent.parent
# Each entry was inspected for independence from disc images and local caches.
# Source-disc and runtime tests remain explicit in Run-SpyroEditorOfflineQa.sh.
CORE_CASES = (
    ("DiscImageWorkingCopySmoke", "Disc-image working-copy smoke passed."),
    ("DiscImageWriteIntegritySmoke", "DISC_IMAGE_WRITE_INTEGRITY_SMOKE_OK"),
    ("DiscImageReadBoundsSmoke", "DISC_IMAGE_READ_BOUNDS_SMOKE_OK"),
    ("DiscCueSheetSmoke", "DISC_CUE_SHEET_SMOKE_OK"),
    ("MobyTreasureExportSmoke", "MOBY_TREASURE_EXPORT_SMOKE_OK"),
    ("MobyWadReaderSmoke", "MOBY_WAD_READER_SMOKE_OK"),
    ("MobyPlanTransactionSmoke", "MOBY_PLAN_TRANSACTION_SMOKE_OK"),
    ("NativeMobyDependencyFactsSmoke", "NATIVE_MOBY_DEPENDENCY_FACTS_SMOKE_OK"),
    ("NativeCollisionIndexSmoke", "NATIVE_COLLISION_INDEX_SMOKE_OK"),
    ("NativeRelocationMapSmoke", "NATIVE_RELOCATION_MAP_SMOKE_OK"),
    ("WadGrowthGapSmoke", "WAD_GROWTH_GAP_SMOKE_OK"),
    ("FixedTailPrivateRecordSmoke", "FIXED_TAIL_IMAGE_PROOF_CASES_OK"),
    ("Id65AuthoringMobyDependencyBundleCompilerSmoke", "NATIVE_SCENE_PROPERTIES_LINKER_SMOKE_OK"),
    ("CombinedExportPublicationSmoke", "COMBINED_EXPORT_PUBLICATION_SMOKE_OK"),
    ("DiagnosticsBundleSmoke", "DIAGNOSTICS_BUNDLE_SMOKE_OK"),
    ("UnusedLevel65MusicOwnershipClosureSmoke", "ID65_MUSIC_SELECTION_COMPILER_SMOKE_OK"),
    ("Id65AuthoringMusicBuildInputSmoke", "ID65_AUTHORING_MUSIC_BUILD_INPUT_SMOKE_OK"),
    ("StructuralCompositionSmoke", "STRUCTURAL_COMPOSITION_SMOKE_OK"),
    ("CrossLevelCandidateFingerprintSmoke", "CROSS_LEVEL_CANDIDATE_FINGERPRINT_SMOKE_OK"),
    ("TextureHistoryWorkspaceSmoke", "TEXTURE_HISTORY_WORKSPACE_SMOKE_OK"),
    ("FarLodTextureCompanionSmoke", "Far-LOD texture companion smoke passed."),
    ("TerrainRelocationStoreSmoke", "Native terrain texture relocation edit store smoke: PASSED"),
    ("PersistenceSmoke", "PASS: external project layout"),
    ("GteProjectionSmoke", "PS1 GTE RTPS/RTPT smoke passed."),
    ("NativeTerrainBaseProjectionSmoke", "Native terrain base-projection smoke passed."),
    ("UpdateSmoke", "PASS: whole-number and incremental prerelease discovery"),
)
# Some source-aware executables expose an explicitly isolated source-free mode.
# Never execute their fixture/materializer path as part of the default gate.
CASE_ARGUMENTS = {
    "FixedTailPrivateRecordSmoke": ["--source-free-image-proof"],
    "Id65AuthoringMobyDependencyBundleCompilerSmoke": ["--source-free-properties-linker"],
    "UnusedLevel65MusicOwnershipClosureSmoke": ["--source-free-music-selection"],
}


def run_owned_command(command, *, cwd, stdout, timeout, termination_grace=1.0):
    """Run one POSIX command in an owned, timeout-controlled process group.

    A timeout remains a failure even if TERM allows a clean child exit. Forced
    termination cannot promise .NET finally blocks or shell cleanup completed;
    interrupted exports/backup directories may require manual recovery. This
    helper never deletes output artifacts. Windows tree termination is not yet
    implemented or tested, and is rejected explicitly rather than orphaning work.
    """
    if os.name != "posix":
        raise OSError("The regression runner requires POSIX process-group termination; Windows is not supported yet.")
    if timeout <= 0 or termination_grace < 0:
        raise ValueError("Timeout must be positive and termination grace nonnegative.")
    process = subprocess.Popen(command, cwd=cwd, stdout=stdout, stderr=subprocess.STDOUT,
                               start_new_session=True)
    try:
        returncode = process.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        _stop_owned_process_group(process, termination_grace)
        raise
    except KeyboardInterrupt:
        if process.returncode is None:
            _stop_owned_process_group(process, termination_grace)
        raise
    return subprocess.CompletedProcess(command, returncode)


def _stop_owned_process_group(process, termination_grace):
    # Do not reap the group leader before the final signal. Its reserved PID
    # prevents accidental reuse of this owned PGID while descendants shut down.
    try:
        try:
            os.killpg(process.pid, signal.SIGTERM)
        except ProcessLookupError:
            pass
        time.sleep(termination_grace)
    finally:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        finally:
            process.wait()


def parse_options(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-all", action="store_true",
                        help="Build every project, including research tools, before regressions.")
    parser.add_argument("--with-source-qa", action="store_true",
                        help="Also run existing source-aware offline QA using this checkout's configured disc/cache; creates disposable readbacks and local reports.")
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--timeout", type=int, default=600,
                        help="Maximum seconds for each build or source-free test (default: 600).")
    parser.add_argument("--source-qa-timeout", type=int, default=1800,
                        help="Maximum seconds for the entire optional source-aware QA suite, not each nested test (default: 1800).")
    parser.add_argument("--list", action="store_true", help="List selected cases without building.")
    options = parser.parse_args(arguments)
    if options.timeout <= 0:
        parser.error("--timeout must be positive")
    if options.source_qa_timeout <= 0:
        parser.error("--source-qa-timeout must be positive")
    return options


def main():
    options = parse_options()
    cases = [(ROOT / "src" / ("Spyro.Editor." + name) /
              ("Spyro.Editor." + name + ".csproj"), marker, CASE_ARGUMENTS.get(name, []))
             for name, marker in CORE_CASES]
    if options.list:
        for project, marker, arguments in cases:
            print(f"{project.relative_to(ROOT)} {' '.join(arguments)} | {marker}")
        return 0

    # Per-run logs are preserved outside the checkout, including on a failure.
    output = Path(tempfile.mkdtemp(prefix="spyro-regression-"))
    print(f"Regression logs: {output}", flush=True)
    results = []

    def execute(name, command, marker=None, timeout=None):
        command_timeout = options.timeout if timeout is None else timeout
        started = time.monotonic()
        log = output / f"{len(results):03d}-{name}.log"
        passed = False
        detail = ""
        with log.open("w", encoding="utf-8") as stream:
            stream.write(json.dumps(command) + "\n")
            stream.flush()
            try:
                process = run_owned_command(command, cwd=ROOT, stdout=stream,
                                            timeout=command_timeout)
                passed = process.returncode == 0
                detail = f"exit={process.returncode}"
            except subprocess.TimeoutExpired:
                detail = f"timeout={command_timeout}s; owned process group stopped; interrupted output cleanup may require recovery"
            except OSError as error:
                detail = str(error)
        contents = log.read_text(encoding="utf-8", errors="replace")
        if passed and marker is not None and marker not in contents:
            passed = False
            detail = "missing success marker"
        elapsed = round(time.monotonic() - started, 2)
        results.append(dict(name=name, passed=passed, detail=detail, seconds=elapsed,
                            log=str(log), command=command))
        print(f"{'PASS' if passed else 'FAIL'} {name} ({elapsed}s; {detail})", flush=True)
        if not passed:
            print("\n".join(contents.splitlines()[-20:]), flush=True)
        (output / "summary.json").write_text(
            json.dumps(results, indent=2) + "\n", encoding="utf-8")
        return passed

    if not execute("python-runner-tests",
                   [sys.executable, "-m", "unittest", "discover", "-s",
                    str(ROOT / "tools/tests"), "-p", "test_spyro_regression_runner.py", "-v"]):
        print("Process-runner regression failed; no project builds were started.", flush=True)
        return 1

    app = ROOT / "src/Spyro.Editor.App/Spyro.Editor.App.csproj"
    required_projects = [app] + [project for project, _, _ in cases]
    # Registered regressions remain mandatory when their project was deleted or
    # renamed. A filesystem-only glob would silently skip them and report green.
    projects = (sorted(set(ROOT.glob("src/**/*.csproj")) | set(required_projects))
                if options.build_all else required_projects)
    # Never race MSBuild writers against the shared Core/App output directories.
    build_results = {}
    for project in dict.fromkeys(projects):
        build_results[project] = execute(
            "build-" + project.stem,
            ["dotnet", "build", str(project), "--configuration", options.configuration,
             "--nologo", "--verbosity", "minimal"])
    for project, marker, arguments in cases:
        if not build_results.get(project, False):
            print(f"SKIP {project.stem}: build failed", flush=True)
            continue
        execute("run-" + project.stem,
                ["dotnet", "run", "--project", str(project), "--configuration",
                 options.configuration, "--no-build", "--no-restore"] +
                (["--"] + arguments if arguments else []), marker)
    if options.with_source_qa and all(result["passed"] for result in results):
        execute("source-offline-qa",
                ["env", "CONFIGURATION=" + options.configuration, "bash",
                 str(ROOT / "tools/Run-SpyroEditorOfflineQa.sh")], "Offline QA passed.",
                timeout=options.source_qa_timeout)
    failed = sum(not result["passed"] for result in results)
    print(f"{len(results)} checks; {failed} failed. Report: {output / 'summary.json'}", flush=True)
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
