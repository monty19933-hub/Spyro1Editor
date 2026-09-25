# USA PETE lifetime and a separate resident-ledger reservation

September 25, 2026. Read-only exact-USA investigation. No ROM/card/settings
write, allocator, hook, export approval, or runtime-lifetime certificate.
All numbers below are hexadecimal unless otherwise stated.

## Decision

**Appending mutable collectible state to PETE does not provide persistence.**
The native loader recopies it, and alternate modes discard its reservation and
reuse its high end as particle storage. Lowering PETE's resident base alone
does not fix either failure.

A **separate increase of the native top reservation** is a smaller promising
design than relocating all overlays. It is not implemented or proved safe
across the full game: a native sentinel crosses the nominal ceiling, every
level loses capacity, and interrupt/computed-pointer lifetime remains open.
The exact retail normal-memory census already rules out an unconditional
additional `800` reservation with every original level unchanged.

## Exact source / probe

`tools/Inspect-SpyroUsaPeteLifetime.py` authenticates the complete original USA
image (`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`),
complete EXE, PETE header and 22 named EXE beforeimages. It reads only. It repeats
held-file and named-file authentication at completion. The report additionally
records complete hashes and bounded word censuses for 38 WAD owners: entries
2, 7, odd entries9..77, and81. Raw word candidates are not automatically code.
This is not a proof that indirect/computed addresses or BIOS code are closed.

The generated local report is
`_local/research/japanese-content-20260925-r01.RCmBCn/usa-pete-lifetime.json`.
Commands:

```sh
python3 tools/test_usa_pete_lifetime.py
python3 tools/Inspect-SpyroUsaPeteLifetime.py 'Spyro the Dragon (USA).bin'
```

Eleven source-free refusal/geometry tests pass, including partial-word native
load/store candidates (LWL/LWR/SWL/SWR). Exact-source capture passes all
22 EXE pins, ten guarded overlay high-address windows, the direct call/store
censuses and final original identity. It executes neither CPU nor BIOS.

## PETE's boundary changes with phase

Native `g800755A4=800`; define `T=80200000-800=801FF800`.
WAD entry8 is `7B8800/3A000`. Its header says native payload length `39C48`.
`8005B7D8..8005B8DC` reads the package, copies its payload, rebases model roots,
stores `g800785FC=T-39C48=801C5BB8`, then calls allocator8005B6F8(a0=0).

| Observed path | Exact owner / implication |
| --- | --- |
| Boot setup | `80012C80` sets FC=T, then `80012C84` calls the full PETE loader. |
| Ordinary model registration | `80015A84..80015AC8` clears only model roots whose low24 address is below FC; 512 entries. Player animation cleanup `80013230..80013288` similarly retains pointers at/above FC except the -1 sentinel. This is model-root retention, not ledger ownership. |
| Ordinary death/reentry | Mode4 branch `8002EE68..8002EE90` calls `800144C8`: reread the scene, then `8001364C(1)` at14520. No PETE reload or FC change in these observed bodies. Scene initialization resets particle state; it does not establish an all-callee no-write proof for arbitrary added bytes. |
| Card-mode entry | `8002D170..8002D224` sets mode13, clears only card-state globals78D78/5C, and derives menu workspace from E8. It does not clear/reload PETE itself. Card/title transition is not synonymous with this one entry function. |
| Mode15 entry | `8002D228..8002D334` reloads PETE atD284 iff original a0 is nonzero. CallerD52C passes0 for g7566C=2 and1 for g7566C=3. |
| Class/mode return | `8002D440..8002D550`: g7566C=1 takes full PETE reloadD4A4 before mode13/new-game setup. Numeric states are pinned; this report does not invent menu labels. |
| Card outer transition | `80033004..800330D4`: when native progress75864=7 and timer78D80>=180, `33094` sets FC=T and `33098` calls allocator(1); then continues level loading. |
| Mode14 transition | `800331AC..800332B4`: g7566C=3 path resets FC=T at3326C and calls allocator(1) at33270 before continuing load. |

Whole aligned EXE direct-call census finds exactly three PETE calls (12C84,
2D284,2D4A4), three allocator calls (33098,33270,5B8B8), and four adjacent
LUI/store FC writes (12C80,33094,3326C,5B860). No extra such candidates appear
in the 38 bounded WAD owners. This does not turn a direct census into complete
computed-caller closure.

## Concrete appended-tail collision

Allocator8005B6F8 sets F8=FC-2000, F4=FC-6000, F0=FC-6008. It then allocates
two render regions, stride1C000 normally and13000 for a0!=0. It clears F4/4000
and F0/8; it does **not** reserve PETE independently from FC.

Level initializer8001364C, also called by the loader at80015D74, reaches
8001441C..8001444C: bind F8 into particle globals80075824/80075738; place an
FF free marker at pool+1; place a **four-byte -1 sentinel at pool+2000=FC**.
Particle allocator80053570..80053604 manages all256 records of20 bytes.
At535C4 it writes kind byte+1 of the next record. The final record is
`FC-20`; at full capacity the53598 random-replacement path can likewise select
any record and writes+1 at535BC.

If PETE length grows by D while T is unchanged, its new base is
`T-(39C48+D)` and the appended tail is `[T-D,T)`. After alternate FC=T, that
tail is inside native particle storage `[T-2000,T)` for D<=2000. For D=680,
the final-record kind store at801FF7E1 is inside the proposed ledger.
This is an actual native writer/capacity counterexample, not a scan for zeros.
Separately, every full PETE reload would copy disc initial bytes over the
mutable tail unless an explicit retained-state owner and restoration hook existed.

The original PETE temporary-source overread and forward-copy overlap remain
as documented in [normal-memory](japanese-egg-normal-memory.md): directory
length and native length differ; actual source-read overhang450, overlap98.
An offline overhang snapshot cannot certify original runtime tail equivalence.

## Better bounded direction: reserve above the native allocation ceiling

Seven adjacent EXE LUI8007/LW pairs read g755A4, at12954,12C70,33084,3325C,
5B824,5B84C,5B930. No direct low-half writer, GP+340 reference or literal pointer
to the global is found in the stated source scope. Their meanings:

- 12954 makes the startup CD destination `801C0000-reserve`; read length40000,
  so its end moves down with the same T.
- 12C70,33084,3325C define FC=T at their reset boundaries.
- 5B824 uses T for temporary PETE CD staging;5B84C uses it for resident placement.
- 5B930 reduces the startup heap's available size. Boot SP is independently
  derived from g755A8=200000 at5B904..5B918, giving801FFFF8; it does not move.

Counterfactual **not an issued allocation**: change reserve800 to1000; new
T=801FF000. Keep `[801FF000,801FF010)` as explicit sentinel/alignment guard.
A680-byte three-slot detached ledger could then occupy
`[801FF010,801FF690)`, strictly below the unchanged original stack reservation
`[801FF800,80200000)`. PETE, both frame modes, startup CD staging and heap limits
move down800. A ledger beginning exactly at T would be corrupted by1444C's
sentinel. No native BSS clear or card read initializes this proposed owner.

Other high-address evidence:

- Ten overlays (entries9,11,15,21,23,25,33,45,57,69) contain a copy to801FE000.
  The pinned branch selects it only if E8>801FFFFF. The ordinary branch derives
  the destination from g80078A44-400C. Decreasing a valid 2MiB frame arena does
  not make the high arm true. Other computed address provenance is not proved.
- EXE8002BB00..BB0C passes801FFF00 to BIOS A0/51 through8005DB24. This native
  executable handoff and BIOS stack/interrupt behavior are outside this proof;
  they cannot be recast as a free-memory owner.
- The three physical-address LUI001F candidates form001FFFFF masks at13298,
  1347C,49078. Literal high-range words are reported without guessing whether
  they are instructions, masks, encoded data or pointers.

**Remaining capacity blocker:** normal floor lowers80187BB0→801873B0. The
existing exact retail normal-memory report gives Twilight Harbor a gap2BC;
an unconditional800 reduction would instead overlap by544 with that original
body unchanged. This prior static census is not a new runtime certificate.
All load/transient extents, alternate modes, known synchronous stack and IRQ/
renderer/callback peaks must be checked for the final composed body, not just
Artisans. No global capacity or stack proof is issued here.

## Minimum next ownership/hook work

Prefer a **new typed top-reserve resident owner**, if the capacity problem is
solved. It must own the changed constant, guard, ledger/header extents and
all shifted memory charges, with exact inverse and source ancestry. PETE bytes
and overlay PCs need not move in this approach. Initializing after cold-boot
BSS/heap setup but before card/collectible use requires a new bounded hook;
the existing BSS loop clears only `[80075640,8007AA38)`.

Minimum semantic hooks: (1) cold-boot owner/header initialization with an
explicit not-loaded state, (2) successful selected-slot card load/manifest
validation, (3) explicit new-game/slot erase initialization, (4) idempotent
per-instance pickup/inventory mutation, (5) save/readback transaction and
failure retention, and (6) accepted required-for100% completion consumers.
Ordinary death/level changes must retain earned bits; unknown/corrupt/version-
mismatched data must remain refused/preserved rather than silently reset.
Title/card cancellation must not accidentally choose another slot's ledger.
BIOS executable restart is a new cold boot, not an RAM-retention promise.

If this top-reserve route cannot satisfy global capacity, resident EXE/BSS
growth below a relocated level arena is a fallback, **not already implemented**.
The original BSS end/heap start is8007AA38, also the whole-overlay base via
g800113A0. Moving it requires actual overlay code/data absolute relocations,
initializer/dispatch bindings, boot clear/heap bounds and all load extents;
changing just that global is insufficient. `NativeMobyPersistentOwnerSource`
and `Ledger` issue stable row/retirement identities, not physical RAM.
`NativePackedResearchImageOwner` moves EXE on disc but explicitly requires
length66000 and does not relocate this runtime arena. Their source/inverse/
identity patterns can be reused, not their names as residency authority.
