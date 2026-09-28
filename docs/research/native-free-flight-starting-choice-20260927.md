# Starting-flight choice: precise UI and reopened-editor regression

2026-09-27, source continuation from `2993d90`. This closes a presentation and
editor-reload evidence gap. It does not expand the native patch or supported
destination set.

## Existing behavior retained

The earlier normal-export integration already connects a default-OFF checkbox
to durable project/level intent and the actual Create BIN dispatcher. Explicit
OFF forces a full original-source rebuild. ON admits only Toasty14 without
ordinary saved edits; ID65, unsupported enabled targets and incompatible compound
exports remain refused. The eleven native instruction windows, source identities,
animation closure, settings schema and publication ownership checks are unchanged.

The native permission is cleared by the existing player reset and reestablished
by the patched movement predicate for the supported destination. “Starting
flight” therefore means the ability is available after entering or respawning
in Toasty. It does not mean an airborne spawn. The native entry control remains
jump followed by airborne Cross. The editor's Free Camera is unrelated.

## Focused change

The unchanged **Fly Anywhere Enabled** control now has a tooltip distinguishing
Spyro's exported-game ability from Free Camera. Details distinguish a never-saved
`default: Off` from explicit `saved: Off`, name a changed draft's actual ON/OFF
value, explain the native entry control, and identify Save Flight as a project
export preference. The save status also names that project ownership.

The actual-control smoke closes one unshown MainWindow, creates a fresh instance,
and checks saved ON after an unsaved OFF draft was abandoned. It then saves OFF,
closes that instance, and checks another fresh instance's unchecked control,
exact saved bytes, explicit OFF intent and required rebuild. These are isolated
temporary project fixtures; no native image is opened or created.

## Verification

Evidence directory:
`_local/free-flight-start-semantics-20260927.2UqaiN/`.

The pre-implementation Debug build passed with zero warnings/errors. Its actual
UI smoke then failed on the intended RED assertion: “A never-saved default Off
is identified as default, not a saved player ability.” The implementation's Debug
build passed with zero warnings/errors and the UI smoke passed:

```text
FREE_FLIGHT_UI_OK checks=46 sourceOpened=false nativeOutputWritten=false
```

Existing settings/export-intent and native instruction checks were replayed
separately, with zero-warning/zero-error builds:

```text
FREE_FLIGHT_SETTINGS_SMOKE_OK checks=76
NATIVE_FREE_FLIGHT_SOURCE_FREE_OK checks=32190
```

Their logs are `settings.log` and `native-source-free.log`. These checks do not
test memory-card loads or prove runtime acceptance. `git diff --check` also
passed; Git emitted its existing CRLF normalization notices only.

## Remaining native and compatibility gates

Save Flight persists an editor-project choice; the exported executable supplies
the additional ability. No native memory-card flag or in-game toggle has been
introduced. The normal ON/OFF outputs must still be exercised with actual game
save/reload and the intended entry/reset behavior. Research R02's observed
respawn and portal reentry do not substitute for that normal-output gate.

The broader goal is deterministic starting availability as compatible levels
are proved. Additional destinations need their animation/effect/resource closure,
memory fit and native entry/reset/save-reload evidence before admission. Native64
and flight-course retail behavior must stay intact; independent ID65 retains its
separate policy. No new destination or compound ON export is admitted here.

Original ROMs/CUEs, cards, emulator settings, sealed packages and previous
candidates were not written or launched by this focused change. No new native
runtime verification is claimed.

## September28 actual packaged picker regression

Separate R09/beta46, clean source d60e620, exposed a real missing refresh after
Stone Hill→Toasty through the ordinary level picker: the correct default-Off
binding was present, but checkbox/Save/Revert stayed disabled after loading.
Settings are bound inside both load fences; their finalizers called the central
availability refresh without updating flight. Source96f6f2a adds that one
availability refresh without rebinding drafts or widening any export gate.

Test-first RED used the actual picker and loader, not direct settings binding.
Release GREEN passes55 UI checks (previous46 plus9 real-selection checks),
including direct-inner and outer-picker held-load fences, repeated selection,
no saved preference/source/output writes and unchanged supported-target rules.
R09 remains preserved with its observed bug. A separate beta47 package and
native ON/OFF/save-reload remain next; source tests do not certify either.
