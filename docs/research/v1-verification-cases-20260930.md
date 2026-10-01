# Focused V1 saved-project and native terrain verification

The exact user-project snapshot is read-only. `V1UserProjectUiCases` first clones
it, including current normal manifests and older research documents. The test
never uses the running packaged editor or emulator. It drives MainWindow's
ordinary Create BIN task and actual project-scope dialog, tests cancellation,
checks both native New Game instruction choices, and independently reads each
physical actor/logical save binding, terrain assignment/private record and
current authored egg. It compares saved identities/scene semantics, exact old
research bytes and the original snapshot. Saving before cancellation may update
the clone's regular manifest timestamp; previous BIN/CUE remain protected.

```sh
SPYRO_UNIVERSAL_JAPAN_IMAGE=/absolute/readonly/JP.bin \
dotnet run --project src/Spyro.Editor.UiSmoke -c Release -- \
  --v1-user-project-only /absolute/readonly/project-snapshot \
  /absolute/new/evidence /absolute/source-workspace
```

The fixture requires the user's exact project: five native removals, eight
universal imports with persistent slots, eight texture edits/private record68,
and current two-surface/one-egg authored scene. It produces actual separate
owner/modal PNG captures at1024×720 and1440×900, not fabricated composites.
The optional Japanese source must be configured to reproduce the saved signs.

Independent native terrain readback commands (no editor/emulator writes):

```sh
python3 tools/Test-TerrainNativeIndexReadback.py --plan /absolute/cell-crossing.terrain-patch-plan.json --ram /absolute/paused.ram --report /absolute/result.json
python3 tools/Test-TerrainTopologyNativeReadback.py --visibility all --plan /absolute/floor-copy.terrain-patch-plan.json --report /absolute/copy.json
python3 tools/Test-TerrainRemovalNativeReadback.py --plan /absolute/floor-remove.terrain-patch-plan.json --report /absolute/remove.json
python3 tools/Test-TerrainSculptNativeReadback.py --plan /absolute/reachable-floor.terrain-patch-plan.json --report /absolute/sculpt.json
python3 tools/Test-TerrainRendererPacketReadback.py --kind sculpt --plan /absolute/reachable-floor.terrain-patch-plan.json --report /absolute/packet.json
python3 tools/Test-TerrainLevelGrowthReadback.py --plan /absolute/townsquare-lp-growth.terrain-patch-plan.json --report /absolute/growth.json
```

The source BIN is authenticated to the exact original USA hash before analysis.
The existing fan check independently decodes native Z/Y/X tree/group sequences,
compares unchanged query ordering and all active triangle identities, and uses
rational polygon clipping to prove final triangle/cell memberships. An optional
paused RAM snapshot must contain every actual patch exactly once. This is data
and source/native-lookup evidence, not controller or standing acceptance.

Exact Artisans90:0 copy/removal checks bind the original fixed model allocation,
all native EXE/copy-buffer bytes, WAD header and bytes outside the model. Copy
preserves all original sectors/triangles/assignments/903flag-prefix bytes and
query lists, verifies the two source-derived translated triangles and their
rational memberships, checks HP/LP counts/offset contract and native visibility.
Removal checks exactly two degenerate collision slots and their absent lookup
memberships; all survivor order; original LP-floor deactivation; exact XYZ,
FAR-colour and cyclic winding multiset for every remaining HP face's new LP
replacement; unrelated sector preservation; and source90 visibility inheritance.
The reports call suffix zero bytes measured trailing zeros, not a newly inferred
allocator budget. The topology compiler separately checks the actual native
sound-end/padding owner and final heap gate.

Native source details independently read: scene/collision table800785A8,
collision pointer at+44/800785D4. The loader80012F6C..80012FF0 rebases all five
collision pointers. At8004DA78..8004DA90 the vertical query reads header+4 as
flag-prefix count, uses flag[index] only when index<count, otherwise255. The
native group reader8004D818..8004D830 masks the first high-bit marker then reads
successive signed halfwords until the next marker. Equal-height replacement is
rejected at8004DA30, making retained query order relevant. Newly entered copied
or moved memberships append after retained rows; that ordering is explicit.

These tests bind the named Artisans fixtures only. They do not prove arbitrary
terrain growth, all levels, full collision gameplay or memory-card behavior.

The earlier v17 candidates passed raw layout readback: copy uses208bytes of original
allocation padding and preserves all four source NEAR/FAR corner colours in HP,
with the complete translated FAR-colour quad in LP; removal uses488bytes and
rebuilds all37 remaining HP polygons as LP in the original owner sector, with
no appended LP-only sectors; sculpt uses496bytes and matches all38 HP/LP
polygons while updating the14 native collision rows referencing the four moved
vertices. Their raw-slot cyclic comparison did **not** prove rendered packet
triangles or actual distant colours. Those visual claims were withdrawn in
`terrain-packet-audit-correction.json`; failure logs and original receipts remain.
The corrected v23 candidates now independently pass native GPU packet inputs:
copy2, sculpt72, removal70 drawn triangles, exact physical HP table2 FAR RGB,
culling winding after HP flip, double-sided/ordering depth flags, and generated
opaque/bias0 LP material. The new packet test rejects the old v17 copy.
All6199 instructions in the referenced native environment renderer were compared
with the authenticated original USA EXE with zero mismatches; the reproducible
packet tool directly guards ten relevant instruction windows.
Reports are `terrain-packet-v23-*-independent.json`, with corrected structural
and collision receipts at `terrain-topology-v23-*-independent.json`.
Native source lookup has boundary-only omissions/extras on some changed fan
rows. The sculpt test preserves these source memberships and their order while
checking rational membership changes; it does not replace the native convention
with a stricter geometric partition or claim every pre-existing source cell is
geometrically exact.

The exact saved-project baseline passed the actual ordinary export modal and
native readback with177physical rows, all8 imported logical save identities,
all8 HP texture changes/private68, and the authored native-colours egg remapped
to USA class1E9. `v1-exact-baseline-receipt.json` records exact module and BIN
hashes. The first readback attempt's incorrect model-subfile/JP-source-class
assumptions are preserved in failure logs and corrected in the fixture.
Both contextual start choices passed the ordinary modal, native payload readback,
saved identity/scene-fingerprint and research preservation checks. Their EXEs
differ in exactly the two guarded New Game words. The authored-start BIN hash
is31fb132654f637e78dff742ae5d30d204ae2b921f0886caa8362a2ba65f65ee1;
Artisans-start is766b171af15469711d3b4543bc819d64f574e39fc89a342cfaf83b22e60015a5.
`SPYRO_V1_RESUME_CLONE` can continue a prior isolated fixture clone after first
validating its existing Artisans-start output; it cannot select the preserved
source snapshot. `SPYRO_V1_ADD_DRUID=1` optionally follows both baseline start
choices by removing only native rows32/33 through ordinary Remove, building the
Alpine Ridge row1 source template, snapping its requested5312/3060 placement to
the actual loaded terrain, and issuing a further ordinary Artisans-start export.
These optional continuation receipts remain separate from the baseline result.
The first actual two-removal Druid ordinary export was blocked by VRAM despite
the prepared-host planner's positive result. The original authored BIN remained
intact. The worker identified the saved-nine-actor reservation preflight falling
back to an empty reservation set after a raw-source fit failure. That product
pipeline correction requires a fresh isolated source snapshot and a
`SPYRO_V1_RETRY_DRUID=1` continuation before claiming a Druid output.

The v23 Town Square complete-entry growth also passed independently: the actual
body matches guarded source/after arrays and grows2048bytes; all other255 WAD
entries including PETE/overlay remain byte exact; all37 model roots and later
subfile offsets relocate exactly; all model packages, scene/fixups, portals,
particles and sound bytes are preserved. The EXE's ISO extent moves53875→53876,
but its bytes remain exact. Independent initializer constant propagation recovers
copy-buffer80089ECC; native sound-end plus resident packages gives
original80183BD4→final80184460, with14160bytes of headroom against the full
1024byte margin. Ten changed fan rows preserve assignment/flag prefix and
query priority; their native membership changes pass. The separate packet audit
matches212 drawn triangles in owner213 and rebuilt LP. This remains source/data
evidence, not a native Town Square execution claim.
