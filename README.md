# Spyro PS1 Level Editor Prototype

This is a Windows desktop prototype for editing extracted `Spyro the Dragon` PS1 level data.

Run it by double-clicking:

```text
Launch Spyro Level Editor.bat
```

Or from PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\SpyroLevelEditor.ps1
```

## What It Does

- Move gems, enemies, and dragon locations by dragging them on the map.
- Edit object name, type, X/Y/Z position, color, and notes in the inspector.
- Add and delete gems, enemies, and dragon markers.
- Palette swap the whole level using preset Spyro-inspired palettes.
- Change level music to another level's music from the music dropdown.
- Import `.bin`, `.iso`, or `.img` PS1 disc images and inspect the ISO9660 file table.
- Analyze imported images for disc layout, serials, file table status, and loader readiness.
- Detect Spyro 1 `WAD.WAD` archive entries and summarize level metadata/asset package pairs.
- Select a detected WAD level in the Level tab and attach that level's metadata/asset WAD IDs to the current project.
- Run a Stone Hill reverse-engineering pass that attaches the known WAD 9/10 package offsets and confirmed runtime moby stride evidence to the project.
- Import a raw PS1 RAM dump from a live level and populate the editor with runtime mobys as clickable, movable objects.
- Run a first-pass WAD linker that searches the selected level asset package for exact runtime X/Y/Z coordinate triples and marks matching mobys as disc-linked candidates.
- Show runtime moby type/state/index columns and a type distribution summary so repeated object classes can be tested and identified.
- Queue a diagnostic patch against all stored WAD coordinate candidates for a selected moby when the ranked candidate does not produce an obvious in-game change.
- Compare focused before/after RAM dumps with `tools\Compare-SpyroRamMobys.ps1` to discover likely moby meanings. One Stone Hill gem collection sample currently marks type `0x20` as a likely gem/collectible.
- Display imported RAM mobys in Spyro runtime XY coordinates, with a labeled world grid and a highlighted placement area instead of an artificial scatter-plot layout.
- Show a Stone Hill replica readiness report that separates confirmed editor-ready evidence from route-level moby hypotheses and missing validation samples.
- Show a Stone Hill identity matrix that maps public Stone Hill targets to current L# moby candidates and source-window leads.
- Load the Stone Hill workbench with compact map labels such as `L109 Treasure/gem lead`, so the canvas, picker, and reports use the same moby identifiers.
- Show the Stone Hill replica dossier from the editor for the current target-to-moby lookup, patch-test queue, and missing proof captures.
- Promote `L186` as the current collected-gem sample: it is the type `0x20` record that changed state while `_globalGemCount` increased in the isolated Stone Hill gem-clean RAM pair.
- Open the generated labelled Stone Hill moby map from the editor as a reference while selecting and moving L# mobys in the workbench.
- Save and load `.splevel.json` project files.
- Export a `.patchplan.json` file for a future extractor/injector pipeline.
- Create a patched copy of an imported image when verified binary patch offsets are present.

## Important Scope Note

The editor can import a PS1 image, detect common ISO/raw BIN layouts, scan for likely serial/file markers, detect the Spyro 1 `WAD.WAD` archive table, select detected levels by WAD ID, and create a patched copy. At the moment, object placements, palette swaps, and music swaps are saved as semantic patch-plan data until verified Spyro 1 object/table offsets are mapped. If `binaryPatches` entries are added to a project, the editor will write those bytes into the copied image.

Developer analysis helpers live in `tools/`:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Analyze-SpyroImage.ps1 -ImagePath "C:\path\to\Spyro.bin" -OutPath .\spyro-image-analysis.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Analyze-SpyroWad.ps1 -ImagePath "C:\path\to\Spyro.bin" -OutPath .\spyro-wad-analysis.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Analyze-StoneHillObjects.ps1 -ImagePath "C:\path\to\Spyro.bin" -OutPath .\stonehill-object-analysis.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Dump-DuckStationRam.ps1 -OutPath .\stonehill-before.bin
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Find-SpyroRamGeometry.ps1 -RamPath .\stonehill-before-gem-clean.bin -OutPath .\stonehill-geometry-overlay.json -MinRuntimeAddress 0x80080000
```

Stone Hill is the first focused target. The current pass confirms its metadata package at WAD index 9, asset package at WAD index 10, and the loaded moby runtime structure stride (`0x50` bytes, with type/state bytes at `0x48`/`0x49`). The remaining mapping work is to connect those live moby records back to their on-disc source records so object movement can emit verified binary patches.

## Loading Runtime Mobys

The quickest way to see real level objects in the editor is to import a raw 2 MB PS1 RAM dump while Spyro is standing in the level. Use **Import RAM Mobys...** on the Disc Image tab or **File > Import Runtime Moby RAM Dump...**. This is not the Spyro game `.bin`; the game image still belongs in **Import Image...**. The importer reads the live `_ptr_levelMobys` pointer at `0x80075828` and converts the `0x50`-byte runtime moby records into editable map markers.

If DuckStation's own **Dump RAM...** menu is unavailable, keep DuckStation running in the level and run `tools\Dump-DuckStationRam.ps1` from this folder. It scans the DuckStation process for Spyro's live 2 MB main RAM window and writes a plain `.bin` file that **Import RAM Mobys...** can read.

These imported mobys are real live level objects, but they are not yet linked back to exact WAD source bytes. Moving them in the GUI updates the project/patch plan; writing those moves back into the disc still requires the source-record mapping step. The canvas uses the runtime X/Y top-down plane from RAM, matching the generated Stone Hill moby map and route reports; the inspector `Z` field stores the remaining PS1 coordinate. This is useful for preserving relative object placement, but the actual Stone Hill walls, collision, and terrain mesh are still not fully decoded.

The runtime moby table can have gaps. The importer scans the full table and marks records found after long invalid gaps as `sparse-table-candidate`; these may be real late-table objects or false positives, so use them as visual/research hints until confirmed by before/after RAM comparisons.

After importing runtime mobys, use **Link WAD...** on the Disc Image tab or **File > Link Runtime Mobys to WAD...**. Exact coordinate-triple matches are marked green as `disc-linked-candidate`; ambiguous matches are marked teal as `disc-linked-ranked-candidate`. This is intentionally conservative and does not yet prove every object type or field layout.

If a patched copy boots but nothing obvious moved, select the same moby, move it a large distance in the editor, then use **Queue All Candidates** before creating a patched copy. That writes the moved position to every stored coordinate candidate for that moby, which is useful for testing ambiguous WAD matches on a disposable BIN copy.

The native Stone Hill editor now separates the two testing modes. **Live Apply** pushes the selected moved moby into DuckStation RAM for a temporary visual check only; it does not prove the source record, collision, or interaction data. **Create Source BIN** writes only the current best source-window lead for each saved edit into a disposable disc image, which is the path to testing permanent placement with the level loader rebuilding normal behavior. **Create Broad BIN** keeps the older broad-candidate diagnostic behavior for cases where the top-ranked source lead does not visibly affect the intended object. **Validate Source** captures current DuckStation RAM and checks the generated source-BIN patch plan against the live level table, reporting whether each patched moby loaded at the edited source coordinates, stayed original, or is only a runtime visual move. **Behavior Diff** captures current DuckStation RAM and compares chest, gem, and fodder records plus linked special-data blocks, so collision/reward/AI evidence is kept separate from visual placement. When a before RAM is provided, validation also reports a separate `interactionProofStatus` so placement and reward/counter proof cannot be confused.

Native source-BIN patch plans include `functionalTests` entries for each saved move. These label risky classes such as chests and fodder separately from simple collectible/scenery moves, and spell out the success signal to check after a fresh patched-CUE boot: chest collision plus reward/drop behavior, actor AI/home behavior, or gem counter/collectable-flag changes. If a source BIN moves a model but the functional signal fails, keep the placement source lead and search for the linked behavior/home/contents record instead of trusting the live RAM visual move.

The native editor inspector also loads `stonehill-moby-special-data.json` and shows linked special-data chains for selected mobys. This matters for containers: `L109` now shows six linked special-data pointers, while `L120`/`L131` share a regular-chest block with fifteen linked pointers. Treat those as collision/contents/behavior leads to preserve or map after a placement source lead is proven.

The same validation can be run from PowerShell after fresh-loading the generated CUE:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Validate-StoneHillSourcePatch.ps1 -CaptureLive -PatchPlanPath ".\Spyro the Dragon (USA)-nativepatchtest-topranked.bin.patchplan.json"
```

Use `source-placement-loaded` as placement evidence only. For a chest such as `L109`, the next proof is still an interaction RAM pair after breaking it: the chest must have collision and produce the expected gem/reward state change. The 2026-05-10 06:03 DuckStation capture was not a clean source-BIN proof: `L65` and `L186` were still original/not fresh-loaded, `L109` was still at an unexpected runtime position, and no gem counter or collectable-flag changes were detected.

For chest, fodder, and gem behavior checks, use the focused behavior diff after capturing a before/after pair. It compares the normal `0x50` moby record plus the linked special-data blocks, so a visual XYZ move is not mistaken for collision, reward, or AI proof:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Compare-StoneHillMobyBehavior.ps1 -BeforePath .\stonehill-before-gem-clean.bin -AfterPath .\stonehill-live-duskstation-automation-20260510-070343.bin -OutJsonPath .\stonehill-moby-behavior-diff.json
```

To capture the currently running DuckStation state directly, either click **Behavior Diff** in the native editor or run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Compare-StoneHillMobyBehavior.ps1 -CaptureLive -OutJsonPath .\stonehill-moby-behavior-diff-current.json -OutMarkdownPath .\stonehill-moby-behavior-diff-current.md
```

To regenerate the consolidated object identity and functional-move checklist, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\New-StoneHillFunctionalMovePlan.ps1
```

In the current 2026-05-10 live-test state, L109/L120/L131 chest main XYZ fields changed but their linked special-data blocks did not. That supports the working rule: RAM-only chest movement is visual placement, while functional collision/reward testing requires a fresh source-BIN boot and then a separate interaction pair.

## Experimental Geometry Overlay

Run `tools\Find-SpyroRamGeometry.ps1` against a live Stone Hill RAM dump to create `stonehill-geometry-overlay.json`. In the editor, use **Import Geometry...** on the Disc Image tab or **File > Import Geometry Overlay...**. The scanner ranks candidate RAM vertex clouds by how well their best 2D projection overlaps the runtime moby cluster. It also searches near each vertex cloud for possible `uint16` triangle/quad index tables. Use **Next Geometry** or `G` / `Shift+G` to cycle through the ranked candidates and look for one that lines up with moby placement. Use **Fit Geometry** or `H` to fit the canvas to the current geometry candidate; the overlay label shows candidate bounds and how many points/edges are being sampled. This is a discovery aid, not verified terrain yet; the next reverse-engineering pass is to identify which candidate corresponds to actual Stone Hill collision/visual mesh and map it back to WAD bytes.

The follow-up WAD-origin probe is `tools\Find-SpyroWadGeometryMatches.ps1`. It compares RAM geometry signatures against Stone Hill's asset WAD subfiles one candidate at a time:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Find-SpyroWadGeometryMatches.ps1 -ImagePath ".\Spyro the Dragon (USA).bin" -GeometryPath .\stonehill-geometry-overlay.json -CandidateIndex 0 -MaxCandidates 1 -MinSignatureVertices 6
```

As of the current Stone Hill dump, the top RAM vertex candidates do not exact-match the on-disc WAD bytes, which suggests the visible RAM geometry is transformed, decompressed/repacked, or not the original terrain source table. That is useful negative evidence for the next pass: search for the loader/decompression path or collision-sector records rather than only raw vertex copies.

Two helper tools continue that search:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Find-SpyroRamGeometryReferences.ps1 -RamPath .\stonehill-before-gem-clean.bin -GeometryPath .\stonehill-geometry-overlay.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Find-SpyroRamMeshDescriptors.ps1 -RamPath .\stonehill-before-gem-clean.bin -FocusRuntimeAddress 0x800CFBEC -OutPath .\stonehill-mesh-descriptor-800CFBEC.json
```

The current best descriptor lead is `0x800CFBEC`, which points at several loaded chunks around `0x800CFC00`, `0x800D36B4`, `0x800E4EF0`, `0x80114EE4`, and `0x80118EE4`. To visualize those descriptor-derived chunks in the editor, generate and import a second overlay:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-SpyroDescriptorGeometryOverlay.ps1 -RamPath .\stonehill-before-gem-clean.bin -FocusRuntimeAddress 0x800CFBEC -OutPath .\stonehill-descriptor-geometry-overlay.json
```

Open that JSON with **Import Geometry Overlay...** and cycle with `G` / `Shift+G`. This overlay is still heuristic, but it is anchored to a RAM pointer descriptor rather than only broad vertex-cloud scoring. The exporter intentionally filters out descriptor chunks that look like sentinel/index tables, so the resulting overlay should be less noisy than the first descriptor attempt.

If the strict `0x800CFBEC` overlay is too sparse, the neighboring pointer table at `0x800CF3C4` produces a denser descriptor-derived overlay:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-SpyroDescriptorGeometryOverlay.ps1 -RamPath .\stonehill-before-gem-clean.bin -FocusRuntimeAddress 0x800CF3C4 -OutPath .\stonehill-descriptor-geometry-overlay-800CF3C4.json -MaxPointers 32
```

This expanded overlay has more candidates and more vertices, so it is useful for visual comparison against the original broad scan.

The next topology experiment is `tools\Export-SpyroIndexedTopologyOverlay.ps1`. It decodes the byte-index records at `0x800CF3C4` as local vertex-index strips (`0..34` plus `0xFF`) and applies those real index edges to ranked 35-vertex windows from an existing geometry overlay:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-SpyroIndexedTopologyOverlay.ps1 -RamPath .\stonehill-before-gem-clean.bin -IndexTableRuntimeAddress 0x800CF3C4 -GeometryPath .\stonehill-geometry-overlay.json -OutPath .\stonehill-indexed-topology-overlay.json
```

Import `stonehill-indexed-topology-overlay.json` to test topology-derived edges. This is not yet a full Stone Hill mesh, but its edges come from the discovered byte-index records instead of nearest-neighbor guesses.

The pointer-table survey found a likely sibling table at `0x800CD480` with the same 52-entry count as the index-strip table. Generate a paired diagnostic overlay with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Find-SpyroRamPointerTables.ps1 -RamPath .\stonehill-before-gem-clean.bin -OutPath .\stonehill-pointer-tables.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-SpyroPairedStripOverlay.ps1 -RamPath .\stonehill-before-gem-clean.bin -DataTableRuntimeAddress 0x800CD480 -IndexTableRuntimeAddress 0x800CF3C4 -OutPath .\stonehill-paired-strip-overlay.json
```

`stonehill-paired-strip-overlay.json` starts with a combined diagnostic candidate, then individual paired strips. The packed data coordinates are not verified world positions yet; this overlay is for testing whether `0x800CD480[i]` and `0x800CF3C4[i]` are structurally paired.

The next, more direct topology probe is `tools\Find-SpyroRamAddressReferences.ps1` plus `tools\Export-SpyroIndexedPointerOverlay.ps1`. The address-reference pass found a compact descriptor pattern containing `0x23`, `0x80119F58`, `0x34`, and `0x800CF3C4`, which looks like a 35-entry pointer table paired with the 52-entry index-strip table:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Find-SpyroRamAddressReferences.ps1 -RamPath .\stonehill-before-gem-clean.bin -RuntimeAddress 0x800CD480,0x800CF3C4,0x800CFBE4,0x800785D4,0x80078640,0x8008A3B8 -OutPath .\stonehill-address-references.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-SpyroIndexedPointerOverlay.ps1 -RamPath .\stonehill-before-gem-clean.bin -PointTableRuntimeAddress 0x80119F58 -PointCount 35 -IndexTableRuntimeAddress 0x800CF3C4 -IndexRecordCount 52 -OutPath .\stonehill-indexed-pointer-overlay.json
```

Import `stonehill-indexed-pointer-overlay.json` to test this structure. The first candidates are labeled `diagnostic-fit-to-mobys/...` and are scaled/centered over the runtime moby bounds so local-coordinate shapes do not pile up at the canvas origin. The later candidates are the raw local-coordinate records, which currently cluster near zero because the real world placement transform has not been identified yet. This is still not confirmed world-space Stone Hill terrain, but it is anchored to a much stronger RAM descriptor than the earlier broad scans.

If the indexed-pointer overlay looks like a tangled local mesh moved over the mobys, use the world-space slice overlay instead:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-SpyroWorldspaceSliceOverlay.ps1 -GeometryPath .\stonehill-geometry-overlay.json -OutPath .\stonehill-worldspace-slice-overlay.json -BinSize 1024 -MinSlicePoints 12 -MinRuntimeAddress 0x80080000
```

Import `stonehill-worldspace-slice-overlay.json` and cycle with `G`. These candidates come from the broader world-sized RAM vertex clouds and are split into Z bands so the editor draws cleaner horizontal slices before the all-heights views. The `0x80080000` lower bound filters out executable-code false positives that can look like plausible coordinate data. This is still diagnostic geometry, but it is a better target for visually matching Stone Hill's layout than the small local `35 + 52` table.

The descriptor-chain follow-up found a stronger Stone-Hill-sized chunk at `0x800D36B4`. First scan descriptor chunks for plausible coordinate layouts:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Find-SpyroDescriptorChunkLayouts.ps1 -RamPath .\stonehill-before-gem-clean.bin -ChunkRuntimeAddress 0x800D36B4,0x800E4EF0 -OutPath .\stonehill-descriptor-chunk-layouts.json -MaxRecords 384
```

Then export the strongest current layout as an overlay:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-SpyroDescriptorChunkOverlay.ps1 -RamPath .\stonehill-before-gem-clean.bin -ChunkRuntimeAddress 0x800D36B4 -RecordStride 16 -XOffset 10 -YOffset 14 -ZOffset 8 -OutPath .\stonehill-descriptor-chunk-overlay.json
```

Import `stonehill-descriptor-chunk-overlay.json` and cycle with `G`. The first six candidates are full points-only views using different axis projections (`xy`, `xz`, `yx`, `yz`, `zx`, `zy`) because the true horizontal axes are not confirmed. Height slices follow, and the final candidates draw sequential triplets as diagnostics only. This chunk currently decodes to a near full-level coordinate range, roughly `20..14224` by `17..13819`, so it is a better reverse-engineering target than the scattered broad-scan overlays, but it is not expected to look like final Stone Hill terrain until the real face/sector connectivity is decoded.

If the descriptor chunk still does not resemble Stone Hill, treat it as rejected for map layout. The next on-disc probe is Stone Hill asset subfile 3, which appears to contain packed halfword records with flag bits in the upper bits and ordered low-bit coordinate values:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-SpyroWadPackedLineOverlay.ps1 -ImagePath ".\Spyro the Dragon (USA).bin" -OutPath .\stonehill-wad-packed-line-overlay.json -SubfileIndex 3 -StartOffset 0x10 -RecordStride 8
```

Import `stonehill-wad-packed-line-overlay.json` and cycle with `G`. This is an on-disc format probe, not verified terrain, but it is based on a concrete packed record pattern in Stone Hill's WAD asset package rather than arbitrary RAM point clouds. The overlay is split using subfile 3's header offsets: header/boundary records first, then `0x80..0x255C`, `0x255C..0xB800`, `0xB800..end`, and finally all records. If a candidate only draws a box, it is probably a bounds/control table rather than the visible Stone Hill terrain.

The packed-line exporter also adds flag-group candidates after the broad section views. Candidate indexes around 36-45 isolate middle-table records by high-bit pattern, and candidate indexes around 58-65 isolate tail-table records. These smaller groups are more useful than the all-records candidates for deciding whether any record type resembles actual level layout.

The more direct terrain target is Stone Hill asset subfile 1, which follows the documented Spyro 1 model-block pattern: a texture-list jump, then a ground-model jump, then a part-pointer table. Generate the first structured ground-model overlay with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-SpyroWadGroundModelOverlay.ps1 -ImagePath ".\Spyro the Dragon (USA).bin" -OutPath .\stonehill-wad-ground-model-overlay.json
```

Import `stonehill-wad-ground-model-overlay.json` and cycle with `G`. This is still experimental, but it is no longer a generic coordinate scan; it decodes 178 documented ground-model parts from the Stone Hill WAD and emits packed-vertex/poly-edge candidates with several projection and sector-scale assumptions.

To compare those model-part shapes directly against the runtime moby canvas, create an aligned diagnostic overlay:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Align-SpyroGeometryOverlayToRuntimeMobys.ps1 -GeometryPath .\stonehill-wad-ground-model-overlay.json -RamPath .\stonehill-before-gem-clean.bin -OutPath .\stonehill-wad-ground-model-aligned-robust-overlay.json -MaxSourceCandidates 4
```

Import `stonehill-wad-ground-model-aligned-robust-overlay.json` after importing the matching RAM mobys. This stretches or uniformly fits the decoded model candidates into the live moby X/Y bounds, using percentile bounds to avoid helper/outlier mobys, so it is only a visual alignment aid; it helps decide whether the WAD model parts have the right shape before the exact sector/world transform is known. In the editor, **Use Moby X/Y** remaps already-loaded RAM mobys to the top-down X/Y plane used by the Stone Hill catalog; it does not import JSON. Use **Import Geometry...** for arbitrary overlay JSON, or **Import C4 X/Z** for the generated candidate 4 X/Z overlay.

To compare candidates without cycling blindly, render a contact sheet:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Render-SpyroGeometryContactSheet.ps1 -GeometryPath .\stonehill-wad-ground-model-aligned-robust-overlay.json -RamPath .\stonehill-before-gem-clean.bin -OutPath .\stonehill-wad-ground-model-aligned-robust-contact-sheet.png -MaxCandidates 16
```

To make the current object-identification state easier to review, render a labelled Stone Hill moby map and JSON catalog:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Render-StoneHillMobyMap.ps1 -RamPath .\stonehill-before-gem-clean.bin -GeometryPath .\stonehill-wad-ground-model-aligned-robust-overlay.json -GeometryCandidateIndex 0 -DiffPath .\stonehill-ram-gem-clean-diff.json -OutImagePath .\stonehill-moby-map.png -OutCatalogPath .\stonehill-moby-catalog.json
```

The catalog is deliberately conservative. Type `0x20` is the only strong item label so far because the before/after gem-collection sample changes a `0x20` record. Type `0x30` is tracked as a weak dragon/NPC candidate, type `0x18` as a weak active-object/enemy candidate, and helper-looking records remain marked as unverified until more focused RAM samples are captured. The RAM diff now also checks the public-symbol-map progress globals: `_globalGemCount` at `0x80075860`, `_globalDragonCount` at `0x80075750`, `_globalEggCount` at `0x80075810`, and `_collectablesStateFlags` at `0x80077900`. In the current gem sample, `_globalGemCount` increases by `1` and two collectable-state bytes change (`0x8007793E` and `0x80077DB8`), which is stronger evidence that the changed `0x20` records are treasure-related.

To regenerate that focused sample diff:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Compare-SpyroRamMobys.ps1 -BeforePath .\stonehill-before-gem-clean.bin -AfterPath .\stonehill-after-gem-clean.bin -OutPath .\stonehill-ram-gem-clean-diff.json
```

To keep the focused RAM-pair work organized, run the focused-pair report:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Update-StoneHillFocusedRamPairReport.ps1
```

This creates `stonehill-focused-ram-pairs.json` if it is missing, compares any complete before/after pairs, and writes `stonehill-focused-ram-pair-report.md` plus `stonehill-focused-ram-pair-report.json`. The current report has one completed calibration pair (`gem-clean`) and ten missing focused pairs: Astor freed, Gavin freed, Lindar freed, Gildas freed, hidden key pickup, locked chest opened, blue thief caught, one ram defeated, one shepherd defeated, and one sheep/fodder sample. Drop new raw 2 MB RAM dumps at the filenames listed in the report, then rerun the script to promote them into diff evidence.

To refresh the whole Stone Hill workbench after adding new RAM captures or patch-test evidence, run the orchestration script:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Update-StoneHillReplicaWorkbench.ps1
```

This updates the gem-clean diff, focused-pair report, special-data report, source-window rankings, replica map plan, labelled moby map/catalog, target board, patch-test matrix, readiness report, identity matrix, validation playbook, and dossier. Add `-DeepSearch` when you want to rescan the Spyro BIN for WAD source-window links instead of reusing the current `stonehill-moby-source-links.json`.

For a quicker visual pass, use **Load Stone Hill** on the Disc Image tab or **File > Load Stone Hill Workbench**. This loads `stonehill-before-gem-clean.bin`, applies labels from `stonehill-moby-catalog.json`, attaches source-window leads from `stonehill-moby-source-links.json`, and imports the current best local ground-model overlay if it is present. The result is a single editor view where the current Stone Hill map proxy and all decoded runtime mobys can be inspected and moved together. Selecting a moby now shows its conservative catalog label, evidence note, and any scaled-int16 source-window leads in the WAD/source candidate inspector. The Objects tab also has a Stone Hill target picker above the moby list; choose a labelled target such as Astor, treasure/gem, high-field enemy/thief, key route, or locked-chest route and click **Go** to select that runtime moby and center it on the canvas. The Objects summary also prints a compact target lookup for treasure, named-dragon candidates, thief/enemy candidates, locked chest, and hidden key buckets so the current L# mapping is visible while dragging objects.

Use **Show SH Map** on the Disc Image tab or **File > Show Stone Hill Moby Map** to open the generated labelled PNG reference map inside the editor. This is a read-only reference for the same L# markers used by the workbench, target picker, identity matrix, and dossier.

The generated moby map and workbench also include yellow Stone Hill route anchors from public walkthrough layout evidence: start/dry well, beach/key cave, castle/Return Home, cave/tower route, and high fields. The PNG map and editor canvas now draw dashed yellow route-hypothesis lines between those anchors and store the same `publicRouteEdges` in `stonehill-moby-catalog.json`, which makes the current Stone Hill proxy easier to use as a route map while testing moby identities. These anchors and edges are landmarks only; they help orient moby labels such as Gavin/well, Astor/portal, Lindar/Gildas tower route, key/beach, and thief/high-field candidates, but they are not runtime mobys or verified patch targets. The replica map plan now also carries a public route graph for the start field, left cave, middle castle/Return Home route, right tower/high-field route, and dry well, so **Show Stone Hill Target Board** can display route-specific marker buckets and validation cues inside the editor.

To check whether the current workbench is close enough to call a Stone Hill replica, generate the readiness report:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\New-StoneHillReplicaReadinessReport.ps1
```

Open it from **Show SH Readiness** on the Disc Image tab, or read `stonehill-replica-readiness.md`. The current grade is `editor-workbench-ready`: the editor can show the Stone Hill map proxy, move runtime mobys, and guide source-window patch tests, but named dragons, enemies, key, and chest remain hypotheses until the focused RAM pairs are captured.

For a compact lookup of what moby currently corresponds to each Stone Hill target, generate the identity matrix:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\New-StoneHillIdentityMatrix.ps1
```

Open it from **SH Identity** on the Disc Image tab, or read `stonehill-identity-matrix.md`. The target lookup answers which L# marker to try first for treasure, Astor, Lindar/Gildas, blue thief/enemy candidates, Gavin/well, locked chest, and the hidden key route. The moby lookup shows the reverse mapping from each decoded L# marker to its current target label, route zone, source-window lead, and special-data pointer evidence.

For a single Stone Hill status dossier that combines public layout facts, target-to-moby candidates, immediate patch tests, top source-window leads, and missing focused RAM captures:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\New-StoneHillReplicaDossier.ps1
```

Read `stonehill-replica-dossier.md` first when deciding what to move or test next. Its current verdict is **Workbench ready, not a replica yet**: use L10/L21/L32/L43/L109/L120/L131/L186 for confirmed treasure-class layout work, then patch-test L186, L109, and L87 before promoting treasure source encoding or high-field enemy/thief identities.

For Stone Hill-only diagnostic patch tests, move a labelled moby in the workbench and use **Queue SH Leads** on the Disc Image tab or **File > Queue Selected Stone Hill Source-Window Patches...**. This writes the moved coordinate delta as little-endian scaled `int16` values into every current source-window lead attached to that selected moby, then **Create Patched Copy...** can emit a disposable BIN for emulator testing. In the native editor, prefer **Create Source BIN** first; use **Create Broad BIN** only when the top-ranked source lead fails. This is still lead validation, not a verified source-record encoder; use it to discover which candidate window actually controls the in-game object.

To keep source-record mapping moving without recreating large scratch overlays, run the compact placement probe:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Find-StoneHillMobySourceLinks.ps1 -ImagePath ".\Spyro the Dragon (USA).bin" -CatalogPath .\stonehill-moby-catalog.json -OutPath .\stonehill-moby-source-links.json
```

The current report confirms that exact int32 coordinate triples in the Stone Hill WAD do not explain any placement-like moby yet; the exact triples are placeholder/helper-style matches. It does find scaled-int16 two-axis windows for several gems, enemy candidates, and dragon/NPC candidates. Treat those as leads for the next source-table pass, not verified patch offsets.

Mine repeated layouts inside those source windows with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Find-StoneHillSourceWindowLayouts.ps1 -SourceLinksPath .\stonehill-moby-source-links.json -OutPath .\stonehill-source-window-layouts.json
```

The first layout report mines `106` scaled source windows across `15` mobys. This does help as a triage pass, but it is not a finished source-record decoder yet. The strongest raw pair pattern is `axes=xz ordered=xz delta=34`, spanning gem, enemy/active, and unknown records; its unknown examples are zero-position mobys, so treat it as a lead that needs byte-level validation. The cleaner high-signal leads are the gem/enemy X/Z pair spacings around `delta=16`, `delta=18`, and `delta=25`, plus stride-field groups such as `stride=16 x@3 z@5` / `stride=8 x@3 z@5`.

The source-window layout report also emits `topActionableSourceLeads`, `topActionablePairPatterns`, and `topActionableStrideFieldPatterns`. These sections intentionally rank real placement candidates above zero-position helper records. Current highest-signal patch-test leads include gem moby `#109` at WAD-relative `0x95FC80`, enemy/active moby `#87` at `0x9F1EC0`, and the repeated gem/enemy field pattern `stride=16 x@3 z@5` / `stride=8 x@3 z@5` across mobys `#109`, `#120`, and `#208`.

The replica target checklist is generated with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\New-StoneHillReplicaChecklist.ps1 -CatalogPath .\stonehill-moby-catalog.json -SourceLinksPath .\stonehill-moby-source-links.json -OutPath .\stonehill-replica-checklist.json
```

This tracks the public Stone Hill target inventory (`200` treasure value, `4` dragons, `1` egg thief, `1` key, locked chest, rams, shepherds, sheep fodder, and gem containers) against the current decoded RAM/WAD evidence. It is the best place to check what is confirmed, weakly suspected, and still missing.

For the editor-facing view, generate the fused map plan:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\New-StoneHillReplicaMapPlan.ps1 -CatalogPath .\stonehill-moby-catalog.json -SourceLinksPath .\stonehill-moby-source-links.json -SourceLayoutsPath .\stonehill-source-window-layouts.json -OutPath .\stonehill-replica-map-plan.json
```

`stonehill-replica-map-plan.json` ties each decoded runtime moby to its conservative working role, runtime map cluster, public Stone Hill target anchors, current source-window lead, and a best public target guess. **Load Stone Hill Workbench** now attaches those map-plan notes to the editor entities, so selecting a moby shows its zone/role/source status, target guess, and validation advice alongside the catalog and WAD/source lead reports.

The map plan also adds tentative public-route hints for each marker. These do not replace RAM evidence, but they make the editor more useful while the source format is still being decoded: type `0x20` markers are strong gem/collectible leads, type `0x30` markers are weak dragon/NPC leads with route guesses such as Astor or Lindar/Gildas, type `0x18` markers are weak enemy/active-object leads, and unknown placed records are grouped against public route areas such as the well, hidden beach cave, return portal, tower/cave side, and high open fields. The same JSON includes `focusedCapturePlan`, which lists the next RAM samples needed to assign named dragons, the blue thief, the key, locked chest, enemies, and fodder to concrete moby/source records.

The map plan now also includes `publicTargetCandidateLedger` plus per-marker `publicTargetCandidates` and `displayTargetLabel` fields. **Load Stone Hill Workbench** uses those short labels in the object list, and `tools\Render-StoneHillMobyMap.ps1 -ReplicaPlanPath .\stonehill-replica-map-plan.json` uses them on `stonehill-moby-map.png`. Current labels remain evidence-ranked, not final identities: `Treasure/gem` is the only strong class, while `Astor?`, `Lindar/Gildas?`, `Blue thief/enemy?`, `Well/chest?`, and similar names are public-route candidate buckets awaiting focused RAM samples. Records after the first long invalid runtime-table gap are labelled `Late-table?` and are diagnostic only until a fresh RAM dump or focused before/after pair confirms them.

For a compact "which moby should I try moving?" board, generate:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\New-StoneHillReplicaTargetBoard.ps1 -ReplicaPlanPath .\stonehill-replica-map-plan.json -OutJsonPath .\stonehill-replica-target-board.json -OutMarkdownPath .\stonehill-replica-target-board.md
```

`stonehill-replica-target-board.md` is the quickest current lookup table for editor testing. It now has a location-first ledger for the start field hub, dry well/Gavin/locked chest, castle/Return Home/Astor, hidden beach/key cave, left cave/Lindar route, tower/Gildas route, high fields/blue thief loop, late-table outliers, and placeholder helpers. It also maps public Stone Hill targets to candidate editor markers: treasure/gems use `L10`, `L21`, `L32`, `L43`, `L109`, `L120`, `L131`, and `L186`; Astor currently points to `L65`; Lindar/Gildas share `L54`; the blue thief/high-field enemy route starts with `L87` and `L142`; the well/locked-chest route now uses only non-sparse `L95` and `L183`; and the beach/key route has no current non-sparse marker. Late-table records such as `L390`, `L399`, `L412`, `L413`, `L421`, and `L422` remain visible as diagnostics, not replica targets. The identity matrix now prefers the patch-test matrix's native-editor source leads, such as `L109@0xB34800 xy`, `L120@0x928640 xy`, `L186@0x9958C0 xz`, `L87@0x9F1EC0 xz`, and `L65@0xAB5200 yz`.

Inside the editor, use **Show SH Targets** on the Disc Image tab or **File > Show Stone Hill Target Board** to load that target board into the Disc Image information panel. This keeps the current location-to-marker and target-to-marker lookup visible while dragging mobys in the Stone Hill workbench.

For the next byte-level proof pass, generate the ranked patch-test matrix:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\New-StoneHillPatchTestMatrix.ps1
```

`stonehill-patch-test-matrix.md` turns the current source-window evidence into a concrete test order. The top tests are `L186` collected-gem source proof at WAD-relative `0x9958C0`, `L109` metal-chest source proof at `0xB34800`, `L87` high-field active-object/red-gem source proof at `0x9F1EC0`, and `L120` regular-chest source proof at `0x928640`. It also records weaker or blocked targets: `L65` is now known to be a live-tested tree/scenery lead, `L54` is another tree/scenery lead, and named dragons, the hidden beach key, enemies, and the locked chest still need focused before/after RAM pairs. Use this matrix when deciding which moby to drag and patch-test first.

For the current "what do I move next, and how do I prove what it is?" workflow, generate the validation playbook:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\New-StoneHillValidationPlaybook.ps1
```

`stonehill-validation-playbook.md` combines the target board, patch-test matrix, focused RAM-pair list, and a useful external SpyroEdit validation trick: when object identity is unknown, move one coordinate sharply and watch which visible object moves; SpyroEdit's guide suggests increasing an object's `Z` by about `1000` as a clear live-emulator probe. In this editor, use **Load Stone Hill Workbench**, select an `L#` marker from the target picker, drag it for X/Y map tests, then use **Create Source BIN** on disposable BIN copies for source-window tests. The current first source-record proof is `L186` because it is the specific collected-gem sample; `L109` remains the strongest broader treasure source-window family lead, and `L87` remains the first high-field enemy/thief lead. Open the generated playbook from **SH Playbook** on the Disc Image tab or **File > Show Stone Hill Validation Playbook**.

There is also a PCSX-Redux live probe for the identity step:

```text
tools\pcsx-redux-stonehill-moby-probe.lua
```

Load it from PCSX-Redux's Lua console while Stone Hill is running. It reads `_ptr_levelMobys`, lets you choose the same editor `L#` index, applies large X/Y/Z offsets in live RAM, and can revert the edited coordinates. Use this to prove which visible item, enemy, dragon, or helper corresponds to a candidate marker before promoting that marker in the editor or patch-testing its WAD source-window leads.

The public Spyro 1 reverse-engineering symbol map also identifies `_ptr_dynamicLevelMobys` at `0x8007573C` and `_ptr_levelMobySpecialData` at `0x80075930`, so the current object pass now inventories those pointers separately instead of assuming every useful record is in `_ptr_levelMobys`:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Analyze-SpyroRuntimeMobyTables.ps1 -RamPath .\stonehill-before-gem-clean.bin -OutPath .\stonehill-runtime-moby-tables.json
```

In the current Stone Hill dump, the level moby table is still the clean editor source: `31` decoded candidates, `27` placement-like candidates. The dynamic table produces only `7` sparse same-stride candidates, including one `0x20`, so those are research leads for future focused RAM samples rather than confirmed movable Stone Hill objects. The special-data pointer probe is intentionally noisy and should not be treated as an object list.

For the cleaner per-moby special-data pass, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Analyze-StoneHillMobySpecialData.ps1
```

This writes `stonehill-moby-special-data.json` and `stonehill-moby-special-data.md`. The current report reads `128` bytes from each valid special-data pointer, finds `7` valid main-RAM special-data pointers across the catalog, and exposes linked pointer chains for all seven. Treasure-class blocks are shared by `L21`/`L32` and `L120`/`L131`; the regular-chest block has fifteen linked targets and the `L109` metal-chest block has six. The dragon/NPC candidates `L54` and `L65` each have valid special-data blocks, and `L65` links to `L54`, so they should be treated as a linked dragon/NPC chain until isolated Astor/Lindar/Gildas/Gavin RAM pairs split the names. Six of the seven valid blocks exact-match at least their first Stone Hill asset bytes, which gives extra class/behavior source leads; placement edits still need the scaled-int16 source-window patch tests. **Load Stone Hill Workbench** attaches this report to the loaded mobys so the source/diagnostic panel shows special-data pointers, pointer fields, and WAD byte hits for the selected marker.

Keyboard shortcuts in the canvas: press `G` for the next geometry candidate, `Shift+G` for the previous candidate, `H` to fit the view to the current geometry candidate, and `F` to fit the view to placed runtime mobys.

The included `sample-artisans.splevel.json` file can be opened from the app's File menu.
