#!/usr/bin/env python3
"""Repeatable source-free regression gate; never launches the editor or emulator."""

import argparse
import json
from pathlib import Path
import subprocess
import tempfile
import time


ROOT = Path(__file__).resolve().parent.parent
# Each entry was inspected for independence from disc images and local caches.
# Source-disc and runtime tests remain explicit in Run-SpyroEditorOfflineQa.sh.
CORE_CASES = (
    ("DiscImageWorkingCopySmoke", "Disc-image working-copy smoke passed."),
    ("DiscImageWriteIntegritySmoke", "DISC_IMAGE_WRITE_INTEGRITY_SMOKE_OK"),
    ("DiscImageReadBoundsSmoke", "DISC_IMAGE_READ_BOUNDS_SMOKE_OK"),
    ("MobyTreasureExportSmoke", "MOBY_TREASURE_EXPORT_SMOKE_OK"),
    ("MobyPlanTransactionSmoke", "MOBY_PLAN_TRANSACTION_SMOKE_OK"),
    ("CombinedExportPublicationSmoke", "COMBINED_EXPORT_PUBLICATION_SMOKE_OK"),
    ("CrossLevelCandidateFingerprintSmoke", "CROSS_LEVEL_CANDIDATE_FINGERPRINT_SMOKE_OK"),
    ("TextureHistoryWorkspaceSmoke", "TEXTURE_HISTORY_WORKSPACE_SMOKE_OK"),
    ("TerrainRelocationStoreSmoke", "Native terrain texture relocation edit store smoke: PASSED"),
    ("PersistenceSmoke", "PASS: external project layout"),
    ("GteProjectionSmoke", "PS1 GTE RTPS/RTPT smoke passed."),
    ("NativeTerrainBaseProjectionSmoke", "Native terrain base-projection smoke passed."),
    ("UpdateSmoke", "PASS: whole-number and incremental prerelease discovery"),
)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-all", action="store_true",
                        help="Build every project, including research tools, before regressions.")
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--timeout", type=int, default=300,
                        help="Maximum seconds per build/test (default: 300).")
    parser.add_argument("--list", action="store_true", help="List selected cases without building.")
    options = parser.parse_args()
    if options.timeout <= 0:
        parser.error("--timeout must be positive")
    cases = [(ROOT / "src" / ("Spyro.Editor." + name) /
              ("Spyro.Editor." + name + ".csproj"), marker) for name, marker in CORE_CASES]
    if options.list:
        for project, marker in cases:
            print(f"{project.relative_to(ROOT)} | {marker}")
        return 0

    # Per-run logs are preserved outside the checkout, including on a failure.
    output = Path(tempfile.mkdtemp(prefix="spyro-regression-"))
    print(f"Regression logs: {output}", flush=True)
    results = []

    def execute(name, command, marker=None):
        started = time.monotonic()
        log = output / f"{len(results):03d}-{name}.log"
        passed = False
        detail = ""
        with log.open("w", encoding="utf-8") as stream:
            stream.write(json.dumps(command) + "\n")
            stream.flush()
            try:
                process = subprocess.run(command, cwd=ROOT, stdout=stream,
                                         stderr=subprocess.STDOUT, timeout=options.timeout,
                                         check=False)
                passed = process.returncode == 0
                detail = f"exit={process.returncode}"
            except subprocess.TimeoutExpired:
                detail = f"timeout={options.timeout}s"
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

    app = ROOT / "src/Spyro.Editor.App/Spyro.Editor.App.csproj"
    projects = sorted(ROOT.glob("src/**/*.csproj")) if options.build_all else [app] + [p for p, _ in cases]
    # Never race MSBuild writers against the shared Core/App output directories.
    build_results = {}
    for project in dict.fromkeys(projects):
        build_results[project] = execute(
            "build-" + project.stem,
            ["dotnet", "build", str(project), "--configuration", options.configuration,
             "--nologo", "--verbosity", "minimal"])
    for project, marker in cases:
        if not build_results.get(project, False):
            print(f"SKIP {project.stem}: build failed", flush=True)
            continue
        execute("run-" + project.stem,
                ["dotnet", "run", "--project", str(project), "--configuration",
                 options.configuration, "--no-build", "--no-restore"], marker)
    failed = sum(not result["passed"] for result in results)
    print(f"{len(results)} checks; {failed} failed. Report: {output / 'summary.json'}", flush=True)
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
