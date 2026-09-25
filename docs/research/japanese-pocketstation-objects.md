# Japanese dragonfly eggs and editable signs

Status: Japanese source supplied and authenticated, 2026-09-25. No Japanese
object import, color correction, message patch, save extension or percent change
has been implemented. The user identified the requested objects as probably the
Japanese/PocketStation objects, not the ordinary retail egg or flight arrows.

## Source availability and prior evidence

The available authenticated source is USA SCUS-94228, full BIN SHA256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
The known original-ROM folder contains USA and CLEAN-ORIGINAL CUEs; no Japanese
image was found there. This is not evidence that Japanese assets are absent from
every local folder, or that every corresponding asset is absent from USA data.
That earlier source-availability blocker is now resolved: the user supplied
`/Users/john/Desktop/Spyro the Dragon (Japan)/`. The exact SCPS_100.83 revision
and its separate archive ownership are documented in the
[active full milestone](v6-universal-japanese-milestone.md). Its new read-only
regional session passes both Japanese and USA exact-source checks. This does
not yet certify Japanese placement, text, appearance or save integration.

Primary references establish the intended feature, not a portable PS1 package:

- [CrystalFissure's original gameplay documentation](https://www.spyroforum.com/viewtopic.php?id=14666)
  describes PocketStation-enabled eggs and raising them on that device.
- [The PC mod author's announcement](https://www.reddit.com/r/Spyro/comments/1wkqjt9/spyro_the_dragon_pc_port_dragonfly_eggs_japanese/)
  describes restored Japanese egg placements and translated signs. A PC-port
  restoration is not native PS1 implementation evidence.

Earlier targeted primary-code searches did not identify the Japanese egg/sign class IDs,
native rows, message codec, render setup, pickup handlers or save consumers.
Do not invent those mappings or infer that USA class `0x22` is a dragonfly egg.
No ROM or third-party game assets were downloaded. New native-ROM evidence is
being recorded separately; the former lack of source must not be treated as a
continuing blocker now that the original Japanese disc is available.

## Implemented discovery correction

`NativeMobySourceInventory` now retains the complete shared PETE registration
table alongside its unchanged 35-level placed/local census. Native copy header
WAD `7B8800/800` has SHA256
`b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1`;
payload `7B9000/39C48` has SHA256
`1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d`.
The actual native copy extends `448` bytes past the declared WAD entry; it is
retained completely, not mistaken for a collection of proven model packages.

There are 54 shared registrations. Ordinary retail egg `0x22` is slot3/root
`3382C`; key `0xAD` is slot10/root `34A8C` (offsets include the PETE header).
The library now has 4,877 placed observations and 1,457 resource observations
(1,403 local + 54 shared). The original placed/local class census stays321.
Shared entries have **no level ID, level-data WAD entry or placed-row index**.
Both retail and fresh-level browsers expose them as inspect-only resources;
existing template matching and fresh placement admission still reject them.
Source ordering/uniqueness checks apply to this observed USA layout, not to every
possible PETE variant. Adjacent roots never establish package lengths.

Root verification is recorded under
`_local/research/v6-mixed-export-20260912-r01.jR0oBr/`: missing-type RED614,
build615 (0 warnings/errors), source-free616 (106 checks), actual USA source617
(40 checks), UI rebuild625 (0 warnings/errors), actual retail browser626 and
fresh aggregate browser627. Both browser routes retain the old no-write,
stale-selection and approved-template tests. The new fresh catalogue cases pass
112 checks. Retail test624 exposed a missing dispatcher pump in the new search
test, corrected before successful626; the refusal was not suppressed.
Shared-resource screenshots from both routes were visually inspected. This is
source and headless UI validation, not a new packaged editor or emulator test.

## Why the requested behaviors need separate work

**Signs:** editing an existing editor Name changes `Moby.Label`, not game text.
The current EXE text exporter owns global level/world names only. Per-sign
messages need identified handlers, encoding/font assets, bounded text storage,
pointer relocation and independently editable message ownership. Arrow-sign
Fairy `0x1E4`, direction scenery `0x1E5`, and Artisans portal names are not proof
of the Japanese tutorial-sign implementation.

**Egg appearance:** the editor's classifier marker color, native row render
fields, model vertex colors and texture CLUTs are different mechanisms. First
identify the actual egg/model and source render settings; do not present a
green marker recolor as an in-game fix. Correct colors require a Japanese
reference plus actual rendered readback.

**Inventory and percentage:** USA ordinary eggs already participate in completion.
The actual USA EXE routine at file `1C320/C0` (PC `8002BB20`) has SHA256
`8ccffbcd96e5afadd87bbe4a6b84f20c9ff4194d30b32aa6223cce2c80bd37e5`.
It computes integer `(gems*50 + dragons*6000 + retailEggs*10000)/12000`, switching
to `100 + (gems-12000)/100` above12,000 gems. This agrees with the
[primary matching-decomp routine](https://raw.githubusercontent.com/theMagicalKarp/open-spyro/main/src/c/ComputeInventoryProgressPercent.c).
Adding dragonfly eggs to the existing egg counter would change progression
semantics, not merely display a new collectible.

Existing native egg-save storage covers18 rows; ID65 has no established egg-save
row. Document UUIDs do not automatically become native collectible/save IDs.
Any USA restoration therefore needs explicit collectible identity, collection
retirement, inventory/save ownership and a deliberate completion policy. Preserve
existing retail counters and saves until those contracts are independently tested.

## Next evidence gates

1. Read-only authenticate an identified Japanese revision. Locate one known egg
   and one sign; trace row/class, model/render, behavior, text/font and dependencies.
2. Establish whether each component is retained in USA or must be ported; keep
   region-specific decoders and source identities separate.
3. Add typed per-sign messages with encoding/length limits. Test two distinct
   messages through edit, undo, save/reload, serialization and native interaction.
4. Add a separately owned dragonfly collectible contract. Define persistence and
   completion policy only after native consumers and compatibility are known.
5. Use an isolated DuckStation candidate for colors, interaction/collection,
   inventory, percent, reentry/reset and card save/reload. Source tests alone do
   not certify any of these gameplay outcomes.
