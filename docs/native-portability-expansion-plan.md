# Native portability and independent-level development

Continuation from clean `e4ff8ef`. The user has authorized lead-engineering
implementation across the game, with parallel investigation and regression-first
changes. The previous exact Town Square key/chest native behavior, sound and
portal re-entry evidence is preserved; it does not certify arbitrary imports.

## Current implementation priorities

1. **Discover the real source universe.** Inventory all native placed rows and
   local registrations directly from the immutable source, including controllers,
   unknown identities and resources that have no placed donor row. Preserve every
   instance variant and distinguish discovery from a relocatable asset schema.
2. **Instantiate dependency families safely.** Feed the proved chest factory
   through reusable private allocation and context-bound immutable sharing. Test
   multiple independent fixture instances and refuse overlapping native singleton
   state. Do not create two apparently separate chests that secretly share state.
3. **Model native movement before authoring replacement routes.** Capture the
   exact Wizard mover, turning, floor settling, collider ownership and animation
   selector behavior. Keep movement-query radius, animated collision and full
   geometry clearance distinct. Preserve all v1 candidate byte goldens.
4. **Own native level identities explicitly.** Bind retail resources, save slots
   and observed portal destinations. Reject decimal-ID aliases and overlapping
   reservations. The ID65 laboratory is not an independently allocated level;
   save/music/entry/portal consumer expansion needs its own source-bound producer.
5. **Integrate and exercise broader closure.** After these prerequisites pass,
   author a safe native-enemy candidate and exercise its whole lifecycle. Expand
   missing-resource families through the shared linker, then expose supported
   templates and independent-level authoring through the regular editor.

Every step requires bounded negative tests, preserved existing behavior and
fresh builds. Offline proof, actual gameplay, installed-editor integration and
full-game acceptance are separate gates. No universal or zero-bug claim is made
from model-root residency, catalog counts, a byte-correct BIN or one interaction.

## Game-wide native discovery

`NativeMobySourceInventory` issues read-only snapshots only for the original USA
BIN SHA-256 `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
It reads ISO/WAD ownership with the shared checked disc parser and independently
walks the seven length-prefixed owners after scene header `0x88` to the eighth,
Moby-row component. All 35 native row starts and counts must agree with the
catalog; labels, caches and count-like byte searches do not determine geometry.
The following properties component and model-subfile registration bounds are
kept distinct. This exact-retail grammar is not a claim about arbitrary ROMs.

The independent original-EXE trace supports this walk: `80013684` loads scene
base from `800785E4`, and `800136B8` retains that base in `s2`. Header advances
at `80013A54`, `80013BA8`, `80013BBC`, `80013C24`, `80013C3C`, and `80013C60`
total `0x88`. The seven declared component advances are at `80013CC4`,
`80013D20`, `80013D7C`, `80013DD8`, `80013E34`, `80013E90`, and `80013EE4`.
The next owner is read at `80013F24`: count follows the size word, the row
base is stored to `80075828`, and the static-row end uses `count * 0x58`.
`80013F48` advances past the complete Moby owner, and `80013FC4..80013FCC`
skips the following properties component. Original EXE file offsets are these
runtime addresses minus `8000F800`. The `80013B88` / `0x484` byte span hashes
to `662f61989fac112de221b0d8b0bd5ae4ac3bc88deb9f15eab7528aee88773c82`.
This is a read-only native instruction trace, not a runtime test.

The real source has 4,877 placed rows and 1,403 local model registrations:
205 placed classes, 298 locally registered classes and a union of 321. Of these,
116 have registrations but no placed row anywhere in the inspected levels.
Those are discoverable resources, **not automatically placeable templates**.
Global-only and dynamically emitted actors outside these two inventories remain
an explicit discovery gap. The 1,990 rows without a local model root include
special/rootless rendering; absence of a root is not absence of native behavior.

All 4,877 rows have nonzero `+0x00` properties words and zero `+0x08` associated
words. Seventeen native properties pointers equal the following component's
start: the inventory records `ComponentEnd`, not a made-up positive payload
extent. Even an interior pointer only establishes its location, not the owned
private extent, schema, links or fixup closure. Full class IDs and every row's
source/hash identity are retained, including variants sharing names or bytes.

Read-only CLI (from the repository):

```sh
dotnet run --project src/Spyro.Editor.NativeMobyInventoryTool -- \
  'Spyro the Dragon (USA).bin' .
```

Add `--json` for detached row/registration metadata, or `--target townsquare`
for all 321 discovered classes' local-root/placed-row observations in that
destination. Every destination result explicitly says it grants no native
import approval. The tool does not modify the ROM, editor cache or installed app.

Focused tests:

```sh
dotnet run --project src/Spyro.Editor.NativeMobyInventorySmoke
dotnet run --project src/Spyro.Editor.NativeMobyInventorySmoke -- \
  --source 'Spyro the Dragon (USA).bin' .
```

Initial regression-first build failed with the new inventory types absent;
implementation then passed 50 source-free checks and 17 source-backed checks.
Integrated/final phase verification must be recorded separately after all
parallel changes have been reviewed and frozen.
