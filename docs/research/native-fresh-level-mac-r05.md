# R05 materials editor: packaged, native GUI acceptance pending

September 8, 2026. Separate private arm64 Mac test app from clean commit
`71fd4993b0cee6461e94237501d582bc38defa23`. This is not a V6 release and does
not replace the installed editor or the preserved R03/R04 copies.

## Exact app and project

App:
`/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/research/fresh-document-mac-test-r05/Spyro Fresh Level Materials Test r05.app`

Private project:
`/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/research/fresh-document-mac-test-r05/state/materials-ui-project`

Keep this fixed-location test bundle and its private state together. Do not
move it, rebuild it in place, reset the project or use an old app to test new
features. The bundle contains no game BIN/CUE. Its source configuration refers
to the protected original game; exports must use a new output directory.

The seed contains the exact saved two-material aggregate: 1024-square floor,
heading128, music20, ordered WAD14/T1 then WAD70/T1, no placements. The separate
legacy document remains its unchanged Track02 seed. Neither silently replaces
the other; normal bootstrap must create this project's own manifest.

## What is in this build

- Explicit legacy-floor and whole-level document modes, placement editing,
  Apply, Undo/Redo, Save and Reopen.
- Two ordered retail material selections and explicit source-thumbnail inspection.
  The floor preview remains schematic, not a textured gameplay renderer.
- Connected research export for a saved schema-2 document with two retail
  materials and **no placements**. Other shapes remain refused; it does not
  silently export only a floor or fall back to the inactive legacy document.
- A safeguard for the native read-only dropdown crash found in R04, and a
  scrollable form/caption layout for smaller windows.

These behaviors pass source/headless tests. The exact R05 native window still
needs separate acceptance. In particular, mixing a placed Wizard with these
materials is not an enabled export path. Inventory browsing is not universal
Moby compatibility, and music selection has no audio-preview guarantee.

## Completed package checks

The first preflight stopped before creating output because the old R04 test
driver deliberately retains symlink refusal fixtures. A narrowly scoped,
independently reviewed inventory adapter now records those literal links
without following them; all other bundle/project/input guards remain strict.
Tests-first correction:20 tests green, prior52 tests green; earlier18 native
guard checks remain separate. Historical failure and manifests are retained.

Actual corrected build55570 closed0. The self-contained publish and native
launcher compilation logs show no warnings/errors. All19 native dependency
records pass arm64/system-library/minimum-OS checks; this is not testing on
every supported Mac. Ad-hoc strict signature validation passes.

Independent pre-GUI checks49357 and98849 each closed0:288 bundle entries,
4388 seed entries,13 preserved trees and27 pinned source/retained files match.
Between them, the actual native launch contract and actual packaged PNG
roundtrip each closed0. Neither starts the editor GUI. Full Release/default
regression at this source checkpoint is335/0, not335 gameplay tests.

Bundle inventory SHA256:
`f86766f9497b5b74f272064ea4c01ce69e8654d7bcb3f00a624ae3d23a93cf0b`.
Packaged App DLL SHA256:
`cf2162a7367a3a84404c1cadb939e59b8c878ba77a98ad048c9e5398e3696510`.
Packaged Core DLL SHA256:
`371ca72b26be02d4d7a488229c3a479bec15ff33820f145a2314fd95d2dad102`.
Full logs and baselines are in the package parent's `evidence/` directory.

## Native launch and next user test

Normal launch at17:33:22 reached macOS Documents consent before managed
startup. The OS recorded the exact R05 Documents prompt at17:33:23; Root asked
John to click Allow. No permission bypass or synthetic bootstrap was used.
At that observation the project manifest was absent and diagnostics empty.
This is a pending permission/GUI gate, not proof that the window opened.
John subsequently asked whether Root could approve it remotely. Computer Use
explicitly refused access to macOS UserNotificationCenter for safety reasons.
Root did not try another control route or bypass; the user must handle consent.

After consent, open **Fresh Level…** and select whole-level/aggregate mode.
Check source inspection, ordinary dropdown selection and a reversible edit:
Apply, Undo/Redo, Save document, then Reopen saved. For the guarded two-material
and no-placement shape, use **Create research candidate…** and a new output
location. This is separate from the main Create BIN path. Preserve the seed
and any exported evidence; a failed output must not be repaired or reused.

The exact earlier Window-produced candidate has separately passed native
DuckStation jump/landing, flame and edge-fall/return checks. That test used the
actual headless Window publication, not an R05 GUI export. See
[the exact runtime report](native-fresh-saved-ui-runtime-r01.md) and
[connected export report](native-fresh-saved-research-window.md).
