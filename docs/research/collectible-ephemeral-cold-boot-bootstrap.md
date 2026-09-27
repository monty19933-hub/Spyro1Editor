# Ephemeral cold-boot bootstrap prototype

September 27, 2026. Detached exact-source afterimage and CPU regression;
`Installed`, native residence, BIOS load/cache/handoff/IRQ ownership, lifetime,
whole-engine fit, card and publication gates all remain false.

`NativeCollectibleColdBootBootstrap` consumes the exact `A00` executable
afterimage from `NativeCollectibleReservationMechanics`. It changes the header
entry PC and uses a bounded temporary code extent that is both declared loaded
by the original executable and immediately cleared by original startup. No
permanent code extent, file writer, ready ledger or card state is issued.

## Exact original ownership

All numbers below are hexadecimal. The complete original USA executable hash
remains `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.

| Source field or extent | Original value |
| --- | --- |
| Header entry PC / GP | 8005B8E0 / 0 |
| Header text address / size | 80010000 / 65800 |
| Declared loaded end / file end | 80075800 / 66000 |
| Header data and BSS address / size | All zero |
| Header stack base / offset | 801FFFF0 / 0 |
| Original native BSS clear | [80075640,8007AA38) |
| Loaded-and-cleared intersection | RAM [80075640,80075800), file [65E40,66000) |
| Entire original intersection | 1C0 bytes, all zero |

The entire original header `0/80`, tail `65E40/1C0`, and native clear-loop
`4C0E0/24` are separately pinned, in addition to complete executable identity.
Their SHA256 values are respectively
`347f9647eac84734506473cda2d4c3d5e175e517b97a78fa1343e26079333d46`,
`5c55c8f4db4010ba9203d83536d0609856af8c847ac039e37e7dde8fbd574b61`, and
`7896dd616818db767dc8a988879948e6011c1b4c1600aff3762ec7dc645c1f48`.
The ownership argument is the intersection of declared loading and immediate
source-proven clearing, not an inference that zero bytes are free space.

## Prototype behavior

Header entry becomes `80075640`. An eleven-instruction (`2C`-byte) stub clears
exactly `[801FF600,801FF800)`, including the guard, to zero. It changes only
`v0`, `v1`, and `at`; original startup overwrites these before use. There are
no loads, calls, stack accesses or HI/LO writes. A direct J with a NOP delay
slot transfers to original entry `8005B8E0` without changing incoming RA.

The original clear loop runs unchanged, erasing every stub byte. At `8005B904`
the complete original and proposed register/BSS states agree, while only the
proposed ledger has been initialized. Later original startup writes incoming
RA at `80075640`; that expected native global is preserved, not confused with
surviving code. At the next real call entry `8005DB14`, the only register
differences from original startup are the paired reserve and heap-size values
in `v1/a1`. Native SP is `801FFFF8`, GP `80075264`, and the call's delay-slot
argument `a0=8007AA3C` has executed. The callee itself is not simulated.

All-zero metadata is explicitly NOT-LOADED: the existing collection kernel
returns InvalidLedger without writing. The stub does not invent magic, ready
phase, manifest, slot, active mask, native-file identity or validated card data.

Pairing the `A00` reserve is mandatory. With original reserve `800`, the later
startup CD request covers `[801BF800,801FF800)` and overlaps the ledger. The
paired request ends at `801FF600`. This prototype's exact application/inverse
refuses the unpaired executable and any changed header, reserve, tail or
complement. Its two-step inverse reconstructs the entire original executable.

## Regression and remaining gates

The dedicated smoke was written first; its initial Release run failed on the
missing bootstrap type. Final focused results:

```text
COLLECTIBLE_COLD_BOOT_BOOTSTRAP_SOURCE_FREE_OK checks=16 sourceReads=0 installed=false
COLLECTIBLE_COLD_BOOT_BOOTSTRAP_SOURCE_OK checks=121 stubBytes=2C clearedBytes=200 entryStateCases=3 biosExecuted=false installed=false imageWrites=0 cardWrites=0
```

The unchanged `RenderProbeCpu` uses strict arithmetic and load/branch delays.
Three independent entry-register patterns check the exact zero-write set,
untouched outer guards, preserved entry state/HI/LO, no reads/calls, full stub
erasure, native RA restoration, and both startup comparison boundaries.
Source/afterimage mutation, truncation, extension, unpaired reserve and
unaligned/oversized/out-of-owner extent refusals are also covered.

```sh
dotnet run --project src/Spyro.Editor.NativeCollectibleColdBootBootstrapSmoke --configuration Release
dotnet run --no-build --project src/Spyro.Editor.NativeCollectibleColdBootBootstrapSmoke --configuration Release -- --source '/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).bin'
```

The CPU fixture copies the header-declared text into RAM; it does not execute
or certify a BIOS loader. Complete-tail loading, instruction-cache handling,
BIOS entry/return-buffer ownership, CD preload completion, pending callbacks
and IRQ stack ownership remain unproved. Normal transition lifetime, DMA,
whole-engine fit and selected-slot/card initialization remain separate work.
No original image, candidate, app, card, settings or emulator was modified.
