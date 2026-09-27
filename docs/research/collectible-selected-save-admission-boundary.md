# Selected ordinary-save admission boundary (USA, detached research)

The smallest useful observation point is **8007CA98**, immediately after the
native selected-save tuple has been committed. It can bind a future pending
selection to an ordinary save. **It does not authorize a ledger initializer to
enter Ready, initialize a legacy tail, or reset existing custom progress.**

This extends [save-buffer ownership](usa-save-buffer-ownership.md), not its
unproved indirect-dispatch, asynchronous-card or persistent-RAM boundaries.
No Core, publisher, runner, image, card or emulator changes accompany it.

## Exact tuple and native branch provenance

| Observed field | Source writer / interpretation |
| --- | --- |
| u32[80078D18] | 8007CA84 stores `g78DA0 << 4`: controller encoding 0 or 16. |
| u32[80078D1C] | 8007CA8C stores `g78D8C`: selected ordinary slot 0..2. |
| u32[80078D20] | 8007CA94 stores the zero-extended u16 at `u32[g78DC4]+7E`. |
| u32[80078D14] | 1 on these bound selection paths; 0 on the unbound new-game path below. |
| u32[80078D94] | 0 on the selected new-game paths; 1 after selected-slot deserialization. Other values are not either admission kind. |

The filename is fixed `BASCUS-94228SPYRO`, at overlay 8007AA3C and resident
80010DB8. The 16-bit header marker is a native identity check, not a collision-free
file/manifest identity or proof that the card has not been replaced.

Loaded selection calls deserializer 80059594 at **8007CA34**; on return,
8007CA64 stores marker 1. Native slot `+40==0` instead branches at 8007C770 to
new-game initializer 8001277C at **8007C798**; 8007C7C8 stores marker 0 and
8007C7E0 jumps to the tuple tail8007CA80. Confirmed overwrite calls the same
initializer at **8007C87C**, stores marker 0 at 8007C8A8, then jumps from 8007C8AC
to 8007CA68. These are different native intents, not equivalent custom-reset
permissions. At the common CA98 boundary, outer phase g78D78 is 2 and the
selection path has set substate g78D7C to 5.

Controller choice is copied from the menu cursor at 8007C3F8; other source
paths explicitly select 0/1 at 8007B22C/8007B264. The error path 8007B308 can
store **-1** into g78DA0, so a future observer must validate the final controller
encoding and slot bounds, not trust the existence of the globals.

Native shutdown is called at 8007CC00, then 8007CC20 publishes outer phase 3.
Resident transition 80032B08 reads the intent marker at 80032B54 and distinguishes
0/1 at 80032B60/80032B98. It also has a separate marker 2 path calling the
initializer at 80032BD8. This is a source-bound consumer of the marker, not
proof that an extension was read, written, durable, or owns live RAM.

## Counterexamples to automatic initialization

- **New-game call does not mean fresh selected save.** New-file construction
  calls the initializer at 8007BE4C before serializing three native slots;
  no selected ordinary slot has yet been admitted.
- **An empty-looking native slot need not be pristine.** After retries, native
  checksum-repair paths call the initializer at 8007B478 and 8007BFFC, then
  reserialize failed slots (for example 8007B4AC and 8007C030/C064/C098).
  A repaired legacy/corrupt slot can subsequently reach the empty-slot new path.
- **Marker 0 can carry stale tuple values.** The new-game path 8007B8DC sets
  marker 0 at 8007B900 but clears g78D14 at 8007B908 without rewriting the tuple.
- **Successful native load is not successful custom load.** Native menu reads
  only `[0,1400)`; neither an all-zero/all-FF tail nor a native checksum proves
  an empty/valid custom ledger. Unknown, corrupt and unsupported legacy data
  must be preserved/refused unless separate explicit migration/reset authority
  is supplied.

## Smallest next gate, not an implementation

Observe CA98 as a **pending selection**, binding the validated tuple, exact
new/load branch provenance, manifest and a fresh session/operation epoch.
Do not infer freshness from current globals alone. A fresh-file provenance
candidate must include the actual 8007BDF0 create route and its accepted result,
then the normal verified-prefix transition 8007BF9C/BFA0; distinguish it from
the repair transition and carry that provenance through later slot selection.
Creation uses a distinct earlier file-marker store at 8007BE48.

Neither that candidate nor native new-game intent issues extension ownership.
Ready still requires separately authorized new custom state **or** validated
saved custom state, plus the owned live residence and transaction/lifetime
conditions established elsewhere. Explicit native overwrite intent must not
silently become custom-ledger reset policy. Stale selection/callback, card
replacement, native/extension partial success and the small marker's collisions
remain unresolved. This note does not implement a hook or initializer.

## Source pins and reproducible evidence

Original USA image SHA256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`;
EXE SHA256 `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`;
whole card overlay (base 8007AA38) SHA256
`8ee66fe644407bf15f93af1615045fbaca17fe3daa9f3b67f38bee2277a94c7c`.

| Pinned window | SHA256 |
| --- | --- |
| Existing selected call/tuple: 8007C9FC/0xA8 | `84d736a2f5f3bf9ca2cca31cdbd844acf9c93f63a92ef37dde7ad6fe865ef9e3` |
| New/overwrite branches: 8007C674/0x388 | `fb0d48f5ce71e4b279bddfcb7f88c832c5c8d8ba363e7ce1ab4ee9c65291cbec` |
| Existing resident consumer: 80032B08/0x150 | `d3ad247f70ad06102b483afadf5d3bcdf8f5939110f6bd3d7475a2216e9a46de` |

The separate oracle `tools/Inspect-SpyroUsaSelectedSaveAdmission.py` reuses all
30 pins from the ownership tool and freezes 8 additional bounded windows. It
checks 38 exact instruction contracts, 17 direct call targets, 2 convergence jumps,
the empty-slot branch, both filename copies, and the 4 literal tuple-store PCs
in the exact EXE/card owners. That literal store-immediate census does not rule
out computed-address stores, other overlays or indirect execution.

```
python3 -B tools/test_usa_selected_save_admission.py
python3 -B tools/Inspect-SpyroUsaSelectedSaveAdmission.py '/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).bin'
```

Regression first: the new test failed with the missing oracle, then all 12
source-free tests passed. Separate exact-source capture passed the checks above
and rehashed both held and named original unchanged. These are **static source
checks**, not CPU/card/emulator tests. JSON explicitly reports no Ready authority,
initializer, complete dispatch/I/O closure, source/card writes or publication.
