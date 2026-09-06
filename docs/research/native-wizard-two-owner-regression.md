# Two Wizard reward/save owners: source regression before integration

The next multi-Wizard native integration now has an independent source-byte
regression. It executes bounded original MIPS integer windows in synthetic RAM;
it is not a new candidate, a gameplay pass or a completed dependency closure.
The existing one-Wizard output and native behavior are unchanged.

The tracked fixture is
`src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/source_two_wizard_owner_lifecycle_oracle.py`.
It imports the existing scalar helper only after checking its exact SHA256 and
does not invoke that helper's default main. Run explicitly from the repository:

```sh
python3 src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/source_two_wizard_owner_lifecycle_oracle.py 'Spyro the Dragon (USA).bin'
```

This is an opt-in source oracle, not another default regression-runner step.
The complete original BIN identity is checked before and after source reads;
whole EXE, Dark overlay, Wizard Peak donor data/T6 row and 14 instruction windows
are pinned. It uses actual class 11B/kind 7F/reward 56, native ordinals 128/129 and
MIPS load/branch-delay semantics. Independent microcases check those delays.

The result is 1,148 assertions: 929 source-word consistency checks and 219 other
source/fixture/scalar assertions. It covers 32 durable/checkpoint/owner reload
cases and 8 initial-mask/collection-order cases, private one-shot death bits,
native child-owner derivation, 10-value selection, durable ORs, checkpoint/live
separation, 36x32 save-copy/restore windows and neighboring bytes. Native 255/256
behavior demonstrates why the producer must retain its persistence-domain bound.

Important qualification: collected kind 7F without checkpoint deletion receives
reward selector FF on reload. A later death can select the native RNG branch;
this disables deterministic treasure, not necessarily every future drop. The
old oracle's broad "disables drop" label is unchanged; this new fixture uses
the actual Wizard kind and stops explicitly at RNG entry and native unlink.
The RNG body, subsequent 0E/0F/10/78 callbacks, allocation/emission, actual total
updates, audio, checksum, renderer and memory-card IO are not executed.

The agent's local fixture passed twice. Root read it fully and independently
replayed it, then retained it in the existing smoke project for version control.
Only the repository-root locator changed for the new path; both complete result
JSONs compare equal after excluding the fixture's own changed file hash. The
tracked fixture passed again with `TWO_NATIVE_WIZARD_OWNER_LIFECYCLE_OK`.
No failed expectations or production changes were needed for these checks.

Evidence under `_local/research/dark-wizard-native-closure-20260906/`:
`two-owner-lifecycle-tracked-root-green.json`, the earlier local/root results,
`two-owner-lifecycle-handoff.md`, and `two-instance-binding-design-20260906.md`.

The next code milestone remains open: bind actual append/placement ancestry
through shared resource composition, derive overlay count/capacity from that
installed revision, and derive each reward identity from the same owners.
For the two-row profile, expected scene counts are 130 placed/151 fixups/89
dynamic slots, and treasure target 120. Preserve the old 129/90/110 profile and
all one-Wizard byte/receipt goldens. No native code duplication or caller-
supplied count/hash should substitute for actual scene ownership. Publication
and gameplay remain subsequent, separate steps.
