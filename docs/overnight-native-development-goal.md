# Autonomous native editor development from 1f91e4b

The active user goal is the complete overnight request in the supplied
`6b16fa9e-a27d-4209-9ddc-a79ff23b6522/pasted-text-1.txt` attachment, read through
all 289 lines. This note tracks that scope; it does not narrow success to the
first green test or a report-only milestone. The preceding turn made concrete
progress and established the clean 240-step / 499-check baseline.

## Required end-state work and evidence

- Recursive native enemy dependency closure: animations, sounds, effects,
  state/behavior, references, ownership, level assumptions and interactions.
  Evidence requires real source/destination comparisons plus actual compiled
  producer output, not a catalogue label or a caller-declared certificate.
- At least one dependency-complete enemy transplant into a destination missing
  required family resources. Validate model, placement, animation, movement,
  collision, AI, sound/effects, player interaction, repeated instances,
  reload/respawn and preservation of unrelated destination content in-game.
  If a candidate is unsuitable, retain the new rule and investigate another.
- Explicit runtime/resource fit before publication, separate from disc growth.
  Unknown capacity cannot be treated as free capacity.
- A usable cross-retail source-variant catalogue in both editor surfaces,
  preserving 4,877 source rows, safe existing edits/Undo/save and honest
  dependency/portability status. Unsupported metadata must not unlock export.
- Continued independent/editor-owned level development: identity, spawn,
  geometry, collision, textures, 46-track music, Mobys/resources, stable export,
  editor reload and native load. Document any remaining donor structures.
- Optional **Enable Free Flight**: persisted default-off, isolated reversible
  generated-output patching, genuine 3D flight, normal disabled behavior,
  intuitive controls, source preservation, integrity/rollback, UI visibility,
  regressions and native ascent/descent/movement/turn/transition/reload checks.
- Preserve all established tests, corruption/rollback safeguards, source-ROM
  hashes, installed editor and retained candidates; keep coherent commits and
  separate structural, gameplay and installed/release evidence.

## Current parallel implementation

Main agent owns integration, the shared catalogue workflow, source/runtime
acceptance, runner registration and checkpoints. Independent subagents own:
general player-animation streams and merging; append-only model registration
and native memory accounting; Free Flight native-state/input investigation and
isolated output producer. Sound/effect closure and independent-level work
continue as these dependencies become available. Shared .NET builds are
serialized; original ROMs, installed apps and earlier candidate bytes are not
development scratch space.

The goal remains active until the actual requested end state is proved or the
strict repeated-blocker condition is met. A useful checkpoint is not completion.

## Implemented checkpoints during this continuation

- `99a974e`: generalized exact-source native model registration; old chest
  profiles and output goldens preserved.
- `eb75be6`: decoded all 35 special player-animation streams and implemented
  source-bound, byte-preserving dependency insertion/reuse.
- `09021da`, `7ea8182`: usable exact-variant native library in both retail and
  blank-level surfaces; existing approved Add handoff only, stale-scene refusal,
  invalid-ROM recovery and isolated UI regressions.
- `f98856f`: source-derived normal-load resident extents and the shared dynamic
  Moby arena. Known extents are not peak/transient/runtime-fit certification.
- `61fc4a7`: reversible target-scoped native Free Flight instruction plan with
  register-lifetime/source-consumer tests. No gameplay certification yet.
- `8f09f26`: typed retained-recovery publication exception, preserving existing
  rollback behavior and evidence instead of parsing exception messages.
- `ab22df5`: strict, stale-write-safe, default-off Free Flight preferences.
  Explicit off remains saved so future export must rebuild normal movement.

Additional sound, private texture-allocation and isolated Free Flight research
candidate producers are being integrated with focused source-free/retail-backed
gates. These are not universal native-import or normal-export approvals. The
whole Release gate must be rerun after their source checkpoint; the older
240-step result cannot certify the current changes.

### Important corrected assumptions

Dark Hollow is the current absent-resource Wizard destination under evaluation:
it has substantially more normal-load headroom than Town Square or the previous
expanded Toasty profile. Its 128 retail rows must remain intact. The old proposed
`B838` properties scratch address is actually the dynamic Moby arena start, and
the old proposed `s3`/`s7` register swap disagrees with native donor code. Neither
is approved for the new native path. Texture, sound, animation and memory
producers must be composed from original-address typed changes; a retail-derived
full afterimage must never overwrite already-authored scene/model edits.

### Native test preparation

DuckStation was available and idle on 2026-09-06. Its existing per-game
configuration had Moon Jump enabled. Before any test-setting modification, the
idle emulator was closed and exact settings backups were saved under
`_local/overnight-native-20260906-r02.jPIcTb/emulator-settings/`:

- `settings.ini.original` SHA-256
  `cd3bfb693556cc08c81b69f56199b61068ac7b483f3d5dae22e2a3931ca55721`
- `SCUS-94228.ini.original` SHA-256
  `629d9bd3eb9890eedb2bfd9ae2458f7f214272f951a9081f687abe8e5f11d1f3`

No test setting has been changed at this checkpoint, and no new native gameplay
has yet been claimed. Subsequent gameplay must disable cheats and isolate memory
cards/save states; restore backed-up user settings after the session. The
installed editor and original source ROM remain outside development scratch.
