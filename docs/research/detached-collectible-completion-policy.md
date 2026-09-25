# Detached required-dragonfly completion and inventory policy

September 25, 2026. The user selected added dragonfly eggs as **required for 100%**,
not an extra percentage bonus. This document specifies the selected source-free
policy, separate from the native Japanese feature and separate from native
integration. It neither changes the detached save schema nor authorizes native
hooks, card I/O, ROM edits, UI capability changes or publication.

## Native baseline and selected weighting

[Japanese collectible runtime ownership](japanese-collectible-runtime-ownership.md)
and `tools/Inspect-SpyroJapaneseCollectibles.py` pin the USA ordinary percentage
routine at executable file `0x1C320`, PC `0x8002BB20`, length `0xC0`, SHA256
`8ccffbcd96e5afadd87bbe4a6b84f20c9ff4194d30b32aa6223cce2c80bd37e5`.
For nonnegative supported counters its mathematical result is:

```text
if gems > 12000: 100 + floor((gems - 12000) / 100)
otherwise:      floor((50*gems + 6000*dragons + 10000*ordinaryEggs) / 12000)
```

The base target is 12,000 gems, 80 dragons and 12 ordinary eggs. Those targets
sum to 1,200,000 score units; dividing by 12,000 is the same as multiplying by
100 and dividing by 1,200,000. The existing source-backed final-homeworld
progression-controller identification also names those three base targets.

The selected custom policy gives each required dragonfly egg **10,000 score
units, the same as one ordinary egg**. This is a deliberate authoring policy,
not a discovery about the native Japanese F5 percentage behavior. Native Japanese
F5 collection does not itself contribute to the ordinary percentage routine.

For a fixed manifest and ordinary save slot:

```text
N = active (not retired) dragonfly identity count
K = earned active dragonfly identities in that slot
requiredScore = 1,200,000 + 10,000*N
earnedBaseScore = 50*min(gems, 12000) + 6000*dragons
                  + 10000*ordinaryEggs + 10000*K
```

When `N > 0` and any required base category remains incomplete, percentage is
`floor(100*earnedBaseScore / requiredScore)`. Capping the base gem contribution
means bonus treasure cannot replace a missing ordinary or custom collectible.
With bounded ordinary totals and any missing required item, the result is below
100; no artificial rounding up or silent 99% clamp is needed.

Once all four requirements are complete—12,000 base gems, 80 dragons, 12 ordinary
eggs and all N active dragonflies—the native treasure-bonus result is used:
`100 + floor((gems - 12000) / 100)`. Thus 12,000 or 12,099 gems gives 100%,
12,100 gives 101%, and 14,000 gives 120%, but only after the required base is
complete. Custom eggs themselves never add bonus percentage above 100%.

When `N == 0`, the original native formula is used **exactly**, not this new
base-completion gate. That deliberately preserves the native bonus shortcut:
12,001 gems and incomplete dragon/egg counters still calculate 100 in the legacy
path. Returning to zero active entries by explicitly retiring all custom eggs
also uses that path; retained retired earned bits are not erased. A separate
`RequiredBaseComplete` result must not be confused with the legacy numeric
shortcut when inspecting inconsistent ordinary counters.

## Detached API and input domain

`DetachedCollectibleCompletionPolicy` consumes an immutable
`DetachedCollectibleSave`, ordinary-slot index 0..2, and immutable validated
`OrdinaryProgress`. It derives N and K from the exact manifest and slot bitset;
callers cannot provide unbound counts that pretend required IDs were collected.
The snapshot includes content GUID, manifest revision/hash, slot, active identity
inventory with immutable GUID/bit/earned entries, required/earned/remaining
counts, required/base-earned score, integer percentage and completion flags.

Ordinary dragon and egg counts must be within the unchanged base profile
`0..80` and `0..12`. Other ordinary-counter content profiles are not silently
clamped into this policy. Gems accept nonnegative signed 32-bit input; 14,000 is
not invented as a hardware storage limit. Accepting a mathematical count does
not authorize that value in native RAM or an authored gem package. Negative
counts and invalid slots are refused, even for an empty manifest. Inventory
capacity inherits the ledger's bound of 4,096 lifetime identities.

Score and percentage multiplication use checked 64-bit intermediates. At the
maximum manifest capacity, `requiredScore*100` is 4,216,000,000, beyond signed
32-bit range. The maximum accepted bonus result remains representable as an
integer: 21,474,816 for `gems == Int32.MaxValue`, only when permitted by the
applicable branch. No arithmetic wraps, saturates silently or uses floating
point rounding. This models the documented native mathematical formula on the
admitted input domain; it does not claim bit-for-bit MIPS overflow, truncation or
field-width behavior for arbitrary corrupt saves or impossible native counters.

Retired IDs are excluded from N, K and the active inventory. Their earned bits
remain untouched. Slots are independent. Evaluation never mutates a manifest
or save; an unrecognized/corrupt save must first be refused by the detached
codec, not replaced by an empty state to calculate a misleading percentage.

## Verification and remaining integration

The standalone `Spyro.Editor.DetachedCollectibleCompletionSmoke` accepts no
paths or write options. It checks native zero-custom goldens and combinations,
required-egg weights, completion and bonus boundaries, monotonic ordinary/custom
progress for a fixed manifest, independent slots, retired-history preservation,
unknown/invalid input refusal, maximum capacity and checked arithmetic.

Focused Debug build passed with zero warnings and errors. The source-free run
reported `DETACHED_COLLECTIBLE_COMPLETION_SOURCE_FREE_OK checks=159466
sourceReads=0 candidateWrites=0 cardWrites=0 nativeHooks=false`. This includes
all ordinary dragon/egg combinations at 15 representative gem boundaries, plus
unit-step gem progress from 0 through 14,000 under nine custom/ordinary progress
combinations. These are mathematical policy checks, not native runtime tests.

```sh
dotnet build src/Spyro.Editor.DetachedCollectibleCompletionSmoke/Spyro.Editor.DetachedCollectibleCompletionSmoke.csproj -c Debug
dotnet src/Spyro.Editor.DetachedCollectibleCompletionSmoke/bin/Debug/net10.0/Spyro.Editor.DetachedCollectibleCompletionSmoke.dll
```

This remains a pure policy. Neither the live native percentage routine nor the
three-slot card menu, inventory rendering, pickup logic, save/reset flows or
bonus-level gate uses it yet. Their source-bound counter ownership, runtime
binding and consumer agreement must be established before claiming native
required-for-100% behavior. No GUI, disc, card or emulator state is changed by
the policy or its source-free tests.
