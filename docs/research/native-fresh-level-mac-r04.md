# Private Mac whole-level test R04

Built once from clean `60f6d6f5e4990a302edde5b74e6b5786f8df2ca7` on
September 8, 2026. This is a separate local test app, not V6 or an in-place
replacement of R03. Subsequent source changes are not automatically in it.

App:
`/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/research/fresh-document-mac-test-r04/Spyro Fresh Level Aggregate Test r04.app`

Private project: the same parent plus `state/aggregate-ui-project`.
Driver/tests: `_local/research/fresh-document-real-ui-r01.DsKiPF/mac-test-driver-r04/`.
Complete build and validation evidence: the app parent plus `evidence/`.

## Verified package; native opening observed, editing gate remains open

Root reviewed the complete driver, templates and failure tests; independent
peer review was clear. Root replayed 40 Python cases and all 18 actual native
canary fixtures. Native fixtures execute an inert test host, not the editor.

One self-contained Release publish, arm64/macOS12 launcher compilation and
strict deep signing verification completed with exit0. The complete outer
transcript is `mac-test-driver-r04/root-build-reviewed-r04.log`; unlike R03's
old transcript, this was captured directly without tool-output truncation.

The separately executed read-only validator passed all288 bundle entries,
4387 seed entries, ten complete protected trees and nineteen pinned inputs,
including full original BIN/CUE hashes and actual EOF. It rechecked strict
signatures of R04, R03 and R02. The current R03 project (Track18), default
project, old apps, original cache and default settings/logs were unchanged.

Root separately executed the actual native print-contract route and packaged
PNG compression round trip; both exited0. Neither initializes a project or
opens the GUI. The seed's real legacy document retains Track02; it is not a
copy or reset of R03's current edited document. Both the new manifest and
aggregate file were absent before the GUI gate.

Normal LaunchServices launch at11:10:30 reached a Documents-folder permission
prompt for `local.spyro.editor.freshlevel.aggregate.test.r04`. The narrow macOS
TCC log records `AUTHREQ_PROMPTING`; the helper PID36028 was waiting and private
diagnostics remained empty at that observation. CUA attachment timed out.
John was asked to handle the system prompt himself. No permission workaround,
privacy-setting change, alternate-parent launch or fabricated manifest was used.
Pending consent is not native editor acceptance. Do not reuse this PID without
fresh verification. The permission history is retained in
`evidence/root-documents-consent-r04.log`.

A later broad process-name check incorrectly reported the helper absent. A
fresh September8 15:33 full command inventory finds the same waiting wrapper
PID36028, and CUA lists R04 as running. It was left untouched; no actual editor
usability or permission acceptance follows from a live wrapper. No package was
rebuilt or permission bypassed.

The actual session log subsequently records startup at15:35:50. Root observed
the real Stone Hill window at15:59 and opened Fresh Level at16:00:02. Its seeded
legacy floor, heading96 and Track02 were present. A direct macOS accessibility
value write to the mode dropdown then triggered an actual16:00:45 SIGABRT in
PID36028: `ComboBoxAutomationPeer.IValueProvider.SetValue` throws through the
native bridge. The tool subsequently attached to newly launched R04 PID67619;
this is not a clean shutdown or successful aggregate edit. OS and managed logs
are retained in `_local/research/readonly-selection-native-regression-r01.YlGG5M/`.

After restart, ordinary dropdown keyboard Down/Return selected Level aggregate
normally and showed its correct absent-document/Fork state. Root closed only
the Fresh Level window without Save/Apply/Fork, leaving the existing main editor
open. Legacy SHA remains the exact seed `b3f10d40...16e4cf1`; the aggregate file
remains absent/nonlink and both packaged App/Core DLL hashes remain unchanged.
No route creation/save/reopen or aggregate native export is accepted by this
read-only check. A narrow accessibility regression/repair is being developed
in source; it will not silently modify this preserved bundle.

## What to test once the window opens

1. Open **Fresh Level…** from the main editor. The default **Legacy floor (v1)**
   mode shows the separately seeded floor and existing floor controls.
2. Select **Level aggregate**, then **Fork saved legacy floor**. This creates
   `native-fresh-level.json` with zero placements and leaves the legacy file
   unchanged; it is an explicit fork, not automatic synchronization.
3. Click **Add Wizard route**, enter origin X/Y/Z and endpoint X/Y, then Apply.
   On the seeded floor, a neutral example is origin9984/9984/8704 to9472/9984.
   Add another route, select it, edit it, move it up/down or remove it.
4. Test Apply, Undo/Redo, Save document and Reopen saved. Invalid raw fields
   must remain visible without replacing valid state. Cancel new route should
   discard only that pending addition. Mode/close changes prompt for dirty work.
5. Check that the saved route IDs/order and full floor settings survive reopen.

These controls passed219 headless UI checks, session138, old UI65 and old
research UI333. They have not yet passed this new package's native edit/save/
close/reopen gate. The XY preview is schematic, not a gameplay/collision view.
The128-placement document bound is not a game capacity guarantee.

Aggregate research export intentionally remains disabled. It cannot substitute
the inactive legacy floor. Texture painting, every-Moby placement, mixed
texture/Wizard export and normal new-level portal integration are not enabled.
The separately played corrected Wizard ROM is not an R04 export feature.

## Measured identities

- Bundle inventory: `eea313130c0b04fdef93ecbb006a797f269bff28cf0bf625124c27e53b96a2f1`.
- Complete protected-before and protected-after, byte-identical:
  `b6dd144e67ae10980a2b753355c9a347ef1373e9a0651c6c5db9dd7e3fd2eb36`.
- Project seed inventory: `963927d6d02d066a9987e3d2a432185f76a0720a9ad17d4bc191cb6154338ef8`.
- Native main: `51990de921a2737560beeee84d47f65bb06660373ab84ae0ddc22819d3843623`.
- App DLL: `1f6e33edf8f3c434eeb7f86778c16beaab2a998b04d6428b78ec59bf7bca980d`.
- Core DLL: `5a5a2f70218f78d00885d9e1291d2b899ad63cbc109427836e26630c6ce72130`.

The ad-hoc native identity is not notarization or filesystem permission.
Build/signature/readback evidence is not gameplay evidence. The pre-GUI
validator deliberately expects manifest absence; do not rerun it after real
bootstrap or weaken that check to accept arbitrary new project state.
