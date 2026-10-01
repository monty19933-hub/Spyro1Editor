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
