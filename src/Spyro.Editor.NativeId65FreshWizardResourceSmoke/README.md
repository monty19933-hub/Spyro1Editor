# Fixed fresh Wizard research resources

This source-free / opt-in source smoke has no output mode. The regression
runner now registers the default `FRESH_WIZARD_RESOURCE_FREE_OK` and optional
`FRESH_WIZARD_RESOURCE_SOURCE_OK` with exactly `--source BIN` (no CUE/workspace).
It exercises the separately typed
`NativeId65FreshWizardResearchResourcePlan`, not an existing body resource
revision, scene compiler, overlay installer or publisher.

The only accepted inputs are the actual source-issued Wizard33/T6 AssetPlan and
the actual fresh A/N16 Compilation: floor(8448,8448,8704), size1024x1024,
spawn(8960,8960,9216), heading64, opaque display colors(80,128,112)/(128,96,64),
music19. Private actual scene/chain ancestors are retained through two internal
accessors; the existing public Compilation properties/JSON do not change.
No caller bytes, expected hashes, flags, addresses, metadata catalogs or generic
policy can issue the resource result.

## Actual tests-first evidence

Evidence folder:
`_local/research/dark-wizard-native-closure-20260906/fresh-wizard-typed-resources-r01.YzST0M/`.

- `01-missing-api-red.log`: real tests-only RED,1.38s,0warnings,4 missing new
  type errors, `BuildProjectReferences=false`; no production stub.
- `02-initial-build.log` / `03-initial-free.log`: first Release5.04s clean,
 33 source-free checks/28 refusals +22 neutral stream checks.
- `04-initial-source.log`: first complete source GREEN5627 checks/60 refusals;
  all independently derived resource hashes match without changing expectations.
- `06-overreport-red.log`: additional finite hostile stream reported65537 for
  an actual65536-byte buffer while the declared remaining owner was65544.
  Actual RED was `ArgumentOutOfRangeException`, before a normalized refusal.
  The production shared identity checker now rejects `n > requested` before
  indexing/hash ingestion. No source or output bytes/goldens changed.
- `07-final-build.log` / `08-final-free.log`: final Release5.20s,0warnings/errors;
  33 free checks/28 refusals +23 hostile/neutral stream checks.
- `09-final-source.log`: final GREEN5627 checks/60 refusals. Original full
  SHA/length/actual EOF are checked before/finally. No payload files are written.
- `10-frozen-raw-oracle.json`: unchanged raw oracle repeated542/44; byte-identical
  to its pre-Core final source JSON, including all output/native loader controls.
- `11-*-build.log` / `11-*-source.log`: old smokes rebuilt against final Core;
  Asset553/54, NormalBody791, TexturedBody18+139 and FreshDisc566016 all GREEN.
  The old complete body/staging plans, both textured bodies and three old disc
  afterimages remain exact. All builds have0warnings/errors.
- `12-runner-red.log`: actual Python RED, new marker absent from registry.
  `13-runner-green.log`: all45 existing/new runner tests GREEN; fake-command
  controls reject missing free/source markers and never activate output modes.

The exact production identity checker is used by the bounded32-byte hostile
fixtures (and one65544-byte overreport fixture). It validates only identity;
this internal helper cannot issue a plan. Production passes only the original
image constants. Cases cover wrong length/initial position before reading,
short input, extra actual EOF, changed final length/position, wrong bytes and
early/mid/late cancellation. Capture finally completes original identity
readback without cancellation, then propagates cancellation; no commit occurs.

Source cases compile actual valid minimum/maximum neutral documents and refuse
both at this fixed resource boundary. They independently check all17369 real
source-owned zero-byte reservations (one aggregate count), complete unallocated
VRAM complement, all2216 inserted source bytes, three exhaustive owner
partitions,71 nonoverlapping source field preimages/afterimages and complete
inverse source packages. Mutating returned arrays never mutates issued owners.

## Independent expected bytes and separate native evidence

The unchanged source-only oracle predates the target Core implementation:
`../Spyro.Editor.NativeWizardFamilyAssetSmoke/source_fresh_wizard_resource_oracle.py`,
SHA `3f34505e2058a16bf2eb26751c0d5cb27dc48965e6d5d6b648654f34a50cdc6f`.
It pins all transitive helpers and independently reconstructs these full bytes
in memory; it does not read C# outputs or candidate files.

| Resource | Bytes(hex) | SHA256 |
| --- | --- | --- |
| Complete VRAM/bank upload | E5800 | d74c2571ed857865b2b989af3001117f44e0915f9c6d59927e30a49d90a79402 |
| Complete used data | 8FC | bea93c58d6d568c7c6162816ddad1afff32a51886f9412a0809b22887e0b83d4 |
| Complete model owner | 17800 | 782c4500b3152f6051b45b8a0a56f6c99c4f703c753948f656318ccfa8618cda |

Actual root order is0@0,78@10A4C,26@1361C,11B@13B80. Old root0 records and
terminator, full class78 allocation2BD0, complete Wizard35B4 including original
padding8, and separately generated model padding6CC remain distinctly owned.
The last native registration extent is3C80, not a claim that source35B4 grew.

The new strict data grammar is particle8C/count11 then sound720/count78, ending
8FC. The old untextured grammar deliberately refuses it despite the overlapping
length range. All76 old sound descriptors/full5E000 bank remain unchanged.
Appended76/77 samples and local77/66/67 slots are typed separately from global
selectors. Global26 retains descriptor22 and its00020028 control word.

The frozen Python oracle separately executes actual pinned parser/sound/model
loader instructions with real load/control delays and no helper stubs; the
C# tests do NOT claim that execution. Its copy anchor and registration header
remain scalar fixture inputs, not an emitted/native-safe body. The actual
parser's unread-tail blind spot is retained as evidence; complete-owner
validators refuse such truncated input independently of parser return values.

## Deliberate limits

Global map1F changes0→76 and is recorded as an unresolved occupied-selector
replacement. Preserving the other255 map bytes is not proof that unchanged
Town callsites selecting1F remain compatible. No SPU hardware/listening,
particle07/41 callbacks, Town-specific role adapter, GTE/renderer/transient
closure, body/copy anchor, scene/private/fixup/capacity/peak, save/reward,
runtime or publication approval is granted. The P0/N16 scene stays unchanged;
it is not a playable Wizard placement or capacity proof.

Run manually:

```text
dotnet run --project src/Spyro.Editor.NativeId65FreshWizardResourceSmoke -c Release
dotnet run --project src/Spyro.Editor.NativeId65FreshWizardResourceSmoke -c Release -- --source ORIGINAL.bin
```

The source CLI accepts only the original BIN. There is no CUE, output directory,
body writer, candidate/publisher call, emulator or save/RAM mutation here.
