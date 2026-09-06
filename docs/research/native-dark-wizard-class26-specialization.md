# Private native class-0x26 spawn specialization

`NativeDarkHollowWizardSpawn26Specialization.Create` accepts only the existing
exact-source behavior issuer. It produces a private callable package and an
explicit before/after instruction certificate. It does not install code, alter
global spawn callback `g800758CC`, activate classes, or publish a candidate.

## Source-owned call contract

The native main package has exactly four lightning-spawn call sites. Each sets
`a0=0x26` and `a1=s3` (the current actor). Whole-source and complete instruction
window hashes bind the following code-owned profiles:

| Call PC | Parent class | Certified setup/delay window |
| --- | --- | --- |
| `8007EBDC` | Lightning `0x26` | `8007EBC8 + 1C` |
| `80082DB4` | Wizard `0x11B` | `80082DA4 + 18` |
| `80082DF0` | Wizard `0x11B` | `80082DDC + 1C` |
| `80082E38` | Wizard `0x11B` | `80082E24 + 1C` |

Unknown caller PCs, wrong parent classes, wrong class constants, changed parent
arguments, and changed complete source windows are refused. These certificates
describe private call sites, not permission to accept arbitrary runtime inputs.
The later linker must bind only these four calls to this callable; it must not
replace the game's general spawn callback with the specialization.

## One native instruction changes

The source preamble `80086DD8 + 94` retains its native `0x58` stack frame,
allocator `800524C4`, model class assignment and parent-to-byte-owner mapping.
At `80086E64`, the original `BEQZ v0,80086F34` word `10400033` becomes
`J 800870F0` word `08021C3C`. Its delay instruction remains exactly
`A2630056` (`SB v1,0x56(s3)`), preserving durable parent identity.

For the certified input `s0=0x26`, the source preamble's `SLTI v0,s0,0x10E`
sets `v0=1`. The original compare chain therefore selects exactly `800870F0`.
The full `80086E64 + 60` source chain hashes to
`53350a2947162a226dc7cdf3477dcb8d72af307891686b09cc738ea513c5c924`.
An independent bounded instruction interpreter executes branch delay slots and
checks that both paths reach this PC and write the same byte owner.

Only dead `v0` differs at initializer entry: the original compare chain leaves
`0x26`, while the specialization retains `1`. Native initializer `800870F0..F8`
does not read it and immediately calls `8003A720`, whose first instruction is
`ADDIU v0,zero,0x10`. Both the initializer prefix and that source EXE entry are
pinned independently. No live input register or owner-byte write differs.

The original three-fragment package remains unchanged and source-bound. The
specialized payload retains its original extraction reads/hashes and carries
the explicit rewrite; rewritten bytes are never labeled as Original source.
The initializer, its native model states/particles, and finalizer remain exact.
The specialized package has only its private preamble entry, not an exposed
initializer entry or a broad class dispatcher.

| Payload | SHA-256 |
| --- | --- |
| Original `0x2FC` bytes | `22626c1a40847f8fccc3fa4dc8d76a09e5a13a99edc5e48bd948767c6f0185ff` |
| Single-instruction specialization | `c40360678d8d883b8b7276f6713a3cabd844c5c5be4c7b2554634b7e0343f00c` |

## Gate and immediate integration boundary

Tests were added first; RED built cleanly and failed at the unimplemented
specialization. Focused Debug GREEN passed with zero warnings/errors,
**311 source-free / 1,882 source-backed aggregate checks**. All prior source,
Town V2/V3, scene and texture goldens remain unchanged. An independent raw-sector
Node oracle reproduced both payload hashes and the exact one-word difference.
Evidence is retained under
`_local/research/dark-wizard-native-closure-20260906/` as
`spawn26-red-green.log` and `spawn26-specialization-oracle.mjs`.

Next installation must combine owned main-entry/exit adapters, this private
callable, native particle `0x07/0x41` implementations, and the source-issued
resource layout. A candidate main dispatch seam is Dark `8007AF24..AF2C`:
the unmatched path must replay its original comparison and delay semantics,
while only classes `0x26/0x11B` enter the owned adapter. This is a proposal,
not an installed or tested hook.

The actual source copy buffer is `80085594`, from EXE case `8005A848`;
its first 16 bytes hash to
`8949add56d6960cd6ab04432154419c31b596758b14ba41f99a63cb962e922aa`.
The complete retail overlay extends to `80085A38`, and the intervening tail is
not all zero. It is not a free-code reservation. A conservative next layout
preserves the complete original overlay, appends code after it, and explicitly
relocates the copy buffer beyond the resulting owned code extent. Source-bound
final resident-memory accounting must be recomputed; stack/transient/GTE and
actual gameplay acceptance remain separate gates.
