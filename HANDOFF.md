# Handoff — universal Moby placement, dragonfly eggs, signs (2026-09-29)

## Where
- Working directory: `/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release`
- Branch `research/id65-v3-environment-header-evidence-20260824`, HEAD `2aa9acd`. **Nothing committed**
  (user has not asked). All work is uncommitted:
  - Modified: `CHANGELOG.md`, `docs/{known-limitations,public-user-guide,release-user-guide}.md`,
    `docs/research/v6-universal-japanese-milestone.md`, `src/Spyro.Editor.App/Spyro.Editor.App.csproj` (beta.52),
    `src/Spyro.Editor.App/Views/{MainWindow.cs,MainWindow.UiShell.cs}`,
    `src/Spyro.Editor.Core/{Editing/MobyEditStore.cs,Exporting/MobySourcePatchExporter.cs,Scene/Moby.cs}`,
    `src/Spyro.Editor.UiSmoke/Program.cs`, `tools/Run-SpyroEditorOfflineQa.sh`, `tools/Run-SpyroEditorRegression.py`.
  - New: `src/Spyro.Editor.Core/Universal/` (all universal code), `src/Spyro.Editor.App/Views/MainWindow.UniversalMobyLibrary.cs`,
    `src/Spyro.Editor.UiSmoke/UniversalMobyUiCases.cs`, `src/Spyro.Editor.UniversalMobySmoke/`,
    `docs/research/v6-universal-moby-placement.md` (design + evidence), this file.
  - Git warns LF→CRLF on edited `.cs` files; the working tree was already mixed (LF/CRLF) and git normalizes. Harmless.
- Latest packaged preview: `_local/v6-private-preview-20260929-r15.wWgOZj` → `Open V6 R15 Preview.command`
  (beta52). R06–R14 previews untouched. Original USA/JP discs never modified.

## Completed (beta52)
- Objects tab → **Add object…** (all 205 placed classes from any level + Dragonfly Egg + Sign), shortcuts
  **Add dragonfly egg**, **Add sign**; old dialog is **More…**. Plain-language browser with exact per-level fit check.
- Create BIN imports model/textures/sounds/behavior/spawned helpers from the original USA disc (`UniversalMobyExport`, runs last).
- Dragonfly eggs (7 colours, JP model + native routine): eyes between the wings, blinking; collected = durable bit only
  (not the 12 stolen eggs / completion); inventory row of coloured still eggs with eyes (≤6 slots, else egg + "K/Y"), max 116.
- Signs: 3 lines, native HUD letters near the sign (render hook).
- Make room: removing every copy of a local class drops its model (rows → inert class 0x1FE, indices kept).
- Editor: save/reopen, copy/paste, delete/undo, egg recolour + sign text in Edit dialog, JP disc picker/locator.

## Key implementation discoveries
- EXE cave (unreferenced retail code): 0x8003A79C–0x8003A9EC (sign hook + egg data), 0x800673A0 (+157 words, egg row code).
  Hooks: 0x8001EFE4 `jal 0x80019300` → sign hook; 0x8001BA04 `jal 0x8001844C` → egg row. Preimages verified.
- Visible list 0x800720F4 is rebuilt after object updates (0x800521C0), so HUD text must be drawn at render time.
- Collected mask: 0x80077908 + (realm*6+slot)*32, bit = row index. AddPrim 0x800168DC appends (draw order = insert order).
- Egg model: up is −Z, wings along ±Y; vertex word x=w>>21, y=(w<<10)>>21, z=(w<<20)>>19 (shared boundary bits);
  moby +0x3E/+0x3F = current/next animation frame (4-frame model: 2,3 = eyes closed).
- Scene row offsets are relative to subfile 3 (use `UniversalLevelData.SetRowClass`).

## Tests actually run (last pass, beta52 code)
- `python3 tools/Run-SpyroEditorRegression.py` → 291 checks, 0 failed.
- `UniversalMobySmoke` (source-free) → 36/36.
- `UiSmoke --universal-moby-ui-only` with `SPYRO_UNIVERSAL_JAPAN_IMAGE` set → UNIVERSAL_MOBY_UI_OK.
- Isolated DuckStation (scratch copy, GDB harness): eyes/blink, inventory icons (3 and 8 eggs), removal + 73 KB import
  in Toasty, editor-built BIN stable 900 frames. Package build + verifier (community ad-hoc) passed; packaged app
  launched/quit cleanly.
- Not run: `tools/Run-SpyroEditorOfflineQa.sh` full source suite; Windows runtime; real memory-card save/reload of eggs.

## Known failures / limits
- No known failing tests. Limits: per-level RAM/VRAM (VRAM of dropped models is not reclaimed), 256 rows, 63 models;
  signs show one message at a time; model drop only when a class is fully removed and not spawned/referenced.

## Next unfinished task
- Reclaim texture (VRAM) space of dropped models so texture-limited imports can fit (subtract regions used only by
  dropped models from the protected set in `UniversalMobyComposer`/`DefaultProtectedVram`). Not started.
