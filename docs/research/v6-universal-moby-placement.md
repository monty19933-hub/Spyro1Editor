# V6 universal Moby placement (beta49)

September 29, 2026. Every placed Spyro 1 object class can be chosen in every
editable level from **Add Moby from any level…**. Create BIN imports what the
object needs automatically. Dragonfly eggs and signs from the Japanese release
are first-class placeable objects.

## Pipeline (Core/Universal)

| Stage | Code | What it does |
| --- | --- | --- |
| Catalogue | `UniversalMobyCatalog`, `UniversalSpecialCatalog` | One entry per placed class (205) with every source row as a variant, categorised; plus Dragonfly Egg (7 colour variants) and Sign. Estimates and exact dry runs per destination. |
| Dependency closure | `UniversalDispatchAnalysis` | Locates each overlay's update class switch (compare trees and jump tables), traces a class's handler, computes its CFG closure, and collects spawned classes / particle constants as dependencies. |
| Assets | `UniversalAssetImport`, `UniversalLevelData` | Copies relocatable model packages (packed, 8-byte aligned), relocates textures into free VRAM (ownership-protected ranges, best-fit, cluster splitting, CLUT dedupe), remaps/appends sounds, appends placed rows and property blocks with fixups. |
| Behavior | `UniversalBehaviorBundle` | Appends a pruned copy of each source update function (class switch replaced by a replayed dispatch chain), a path-specialised init, and wrappers chained onto the destination's own callbacks; patches the EXE per-level bindings. |
| Recovered objects | `UniversalSpecialObjects`, `UniversalSpecialBehaviors` | Japanese egg/sign models from the unchanged JP disc; native USA-engine egg pickup routine and sign text routine. |
| Export | `UniversalMobyExport` | Image-to-image Create BIN stage (runs last), rebuilds WAD/EXE with fresh EDC/ECC, independent readback, never writes a source image. |

Budgets are enforced exactly (compose, not estimate): RAM end ≤ 0x80187BB0 −
0x400, ≥16 dynamic slots, ≤256 rows, ≤63 model slots, texture fit.

## Dragonfly eggs

Classes 0x1E9 (native colours), 0x1F2 blue, 0x1E8 red, 0x1EC gold, 0x1F0 purple,
0x1F9 pink, 0x1D4 pearl white (hue/saturation rotations of the JP model's
colour tables). The per-frame routine spins the egg, detects Spyro within
0x280/0x380, spawns the sparkle particle, sets the durable collected bit
(0x8003B854), increments the level (0x80076FE8[level]) and total (0x80075810)
egg counters, and frees the Moby. Export patches the completion egg weight to
ceil(120000/(12+N)) and widens the inventory egg row to min(12+N, 17) slots;
per-level inventory icons exist only for worlds 1–3 (u8 table 0x8006F480).

## Signs

Class 0x1D0 with the JP sign model (JP Artisans row 173). The message (three
28-byte lines) is the row's private properties block. The engine rebuilds its
visible-object list (0x800720F4, 256 entries) *after* object updates, so the
text is drawn at render time:

- EXE: the gameplay renderer's `jal 0x80019300` (gem HUD) at 0x8001EFE4 is
  redirected to a 22-instruction routine placed in 0x8003A79C, an EXE function
  that no EXE or overlay code references (checked over the EXE and all 256 WAD
  entries). It calls the gem HUD, then calls `update−8` only when the current
  level's bound update function is preceded by the marker `NGIS`
  (0x5349474E) and lies in KSEG0 RAM. Preimages are verified before patching.
- Overlay: every universal bundle starts with `marker, j sign_draw|jr ra, nop`.
  Export proves the marker is present exactly in the levels that have signs.
- `sign_draw` skips when the visible list already holds ≥150 entries, finds the
  first active sign within 0x700 (x/y) and 0x900 (z) of Spyro, and draws its
  lines with the native HUD letter spawner 0x800181AC (layout of the native
  "DEMO MODE" caption) and flush 0x80018880.

## Tests

- `UniversalMobySmoke` (source-free, 30 checks, default regression gate).
- `UiSmoke --universal-moby-ui-only` (offline QA; headless MainWindow): browse,
  place a Stone Hill sheep and a later-world enemy in Toasty, save/reopen,
  undo/re-add, dragonfly egg colour variant, sign with edited text, duplicate
  and delete, save/reopen, Create BIN readback. Eggs/signs run when
  `SPYRO_UNIVERSAL_JAPAN_IMAGE` names the unchanged JP disc.
- Isolated DuckStation runtime (never the user's settings/memory card):
  Toasty + Stone Hill sheep (wanders, flame → butterflies); Artisans + Beast
  Makers and Gnasty's World objects; Lofty Castle + Stone Hill/Artisans
  objects; Dark Hollow + six objects from six levels + three automatic helper
  dependencies (600 stable frames); Toasty eggs (render, pickup counters,
  durable bit, HUD, inventory, completion); Toasty sign (text near, hidden
  away); editor-produced Toasty BIN (sheep + High Caves enemy + blue egg + sign:
  text, pickup, 600 stable frames).
- Survey of all 7,175 class × level pairs: 4,627 fit alone, 1,990 exceed RAM,
  558 exceed free texture memory (34 of them non-4-bit groups).

## Packaged checkpoint R12 / beta49

`_local/v6-private-preview-20260929-r12.dTj4LA` — open with
`Open V6 R12 Preview.command`. Regression 291/0; Mac/Windows package
verification passed (community ad-hoc mode); packaged app launched and quit
cleanly. Hashes and evidence: that folder's `PRIVATE-PREVIEW.md`. R06–R11 untouched.

## R13 / beta50 (usability)

`_local/v6-private-preview-20260929-r13.UYJq0Z` — `Open V6 R13 Preview.command`.
Objects tab: **Add object…**, **Add dragonfly egg**, **Add sign**; the previous
Add window moved to **More…**. Browser wording is plain language (technical
data under "Technical details"); fit status shows "Checking…" until the exact
check completes; Japanese-disc banner with **Choose Japanese disc…**. Regression
291/0, UI flow passed, packages verified, packaged app launched and quit cleanly.

## R14 / beta51

`_local/v6-private-preview-20260929-r14.ffU8ju`. Egg colour list fix (refresh
cleared the variant combo). Dragonfly eggs no longer touch the stolen-egg
counters, egg HUD or completion (the beta49 completion/egg-row/icon-table EXE
patches were removed). A second routine in the EXE cave (0x8003A7F8, called
in place of the inventory divider line at 0x8001BA04) draws them from each
egg's durable collected bit (0x80077908 + level*32, bit = row): up to 6 tinted
egg sprites at the right of the native egg row, else one sprite + "K/Y".
Capacity 32 eggs per project. Regression 291/0.

## R15 / beta52

`_local/v6-private-preview-20260929-r15.wWgOZj`. Egg eyes (UniversalEggEyes: surface ray-cast placement,
4-frame model with closed-eye frames 2/3, blink driven by the egg routine via moby +0x3E/+0x3F); inventory
icons in each egg's colour with eyes (routine at 0x800673A0, data in the 0x8003A7F8 cave, max 116 eggs);
fully removed local classes drop their model (rows → inert class 0x1FE). Regression 291/0. See HANDOFF.md.
