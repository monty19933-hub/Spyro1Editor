# Japanese sign font: USA VRAM ownership and lifetime boundary

2026-09-25. Source-backed geometry, typed GPU operand requirements and explicit
refusals. No GPU hook, upload, overlay patch, disc write or emulator action is
performed by this contract. Source identities are the exact Japanese and USA
originals listed in `japanese-sign-runtime-portability.md`.

## Native atlas is not an existing USA font allocation

JP WAD2 `6F800/A000` is the complete font upload. Its SHA256 is
`917a2c272859d99b573ee3f79b1a62ca847dff218802b2c65f80bb55cb888ff1`.
Native code `800120F0/54` reads it, then queues a VRAM rectangle
`x=0,y=472,width=512 words,height=40`. That is 40,960 bytes in the **left half**,
not part of the normal right-half level texture upload.

USA WAD2 `5B800/3800` is instead an overlay resource, SHA256
`8ee66fe644407bf15f93af1615045fbaca17fe3daa9f3b67f38bee2277a94c7c`.
Its header includes `BASCUS-94228SPYRO` and native pointers. USA startup
`80012978..80012998` reads its directory offset/size from `8007A6E0/E4` into
`u32[800113A0]=8007AA38`, the overlay base. Similar loads occur at
`8002E2DC..8002E32C` and `80033604..80033620`. Matching WAD index2 is therefore
not a cross-region font binding. Existing USA shared UI/model glyph consumers
already participate in the level right-half texture ownership census; they
are not evidence that the Japanese left-half atlas is resident.

## Exact font footprint

The new internal `NativeJapaneseSignFontVramContract` captures the native
atlas, descriptor table, renderer, uploader and independently pinned USA
consumer windows. It decodes all 175 admitted sign glyphs, preserving format1
axis transposition and its U=F0 endpoint correction. It protects inclusive UV
endpoint rectangles conservatively; this is not a GPU rasterization claim.
The source union contains 33,357 referenced packed bytes across 3,604 pixel
rows plus the palette references. The full 40,960-byte atlas remains owned,
including zero and otherwise unreferenced bytes.

- Ordinary glyph CLUT `7FD6` = word x352,y511: atlas offset`9EC0/20`.
- Controller glyphs F7..FA use CLUT `7F96` = word x352,y510:
  atlas offset`9AC0/20`.
- Texture pages16..23 address word x0..511, base row256, with native UV V216..255.

The source addresses cannot be passed straight into
`NativeMobyTextureMaterialization`: it requires pixels **and** CLUTs in the
right-half `80000` upload. Tests verify that the real native left-half descriptor
is refused, rather than silently shifting its address domain.

## USA competing owners: geometry is not a lifetime certificate

The observed USA initialization gives these half-open physical rectangles:

| Source owner | Word rectangle | Overlap with unchanged JP atlas |
|---|---|---:|
| ordinary draw0 | x0..512,y8..232 | 0 |
| ordinary draw1 | x0..512,y248..472 | 0 |
| display0 envelope | x0..512,y0..240 | 0 |
| display1 envelope | x0..512,y240..480 | 4,096 words |
| initial ClearImage | x0..512,y0..512 | 20,480 words |
| post-startup ClearImage | x0..512,y0..480 | 4,096 words |
| two level uploads | x512..1024,y0..512 | 0 |
| native modal backdrop | x512..768,y0..225 | 0 |
| modal backdrop palette | x512..544,y224..225 | 0 |

The draw rectangles end exactly at row472, but that does **not** make the
remaining border free. The second display envelope overlaps eight atlas rows;
this is an unresolved visibility issue, not a claim that display itself writes
VRAM. Both clears are actual destructive writes. Correct temporal placement
after initialization, later reset/reentry handling, all other upload/copy/clear
paths, and visible scanout behavior remain unproved. The contract therefore
refuses unchanged native placement even though the ordinary draw clips do not
intersect it. These known rectangles are not advertised as a complete owner
inventory.

`UsaEvidence` pins full source windows for draw/display setup and their SDK
rectangle stores, both clears, the two right-half upload passes, modal backdrop
and palette transfers, and directory/overlay ownership. Full hashes live in
the code-owned evidence list, rather than caller-supplied proof flags.

## Most direct relocation geometry, still unapproved

A fixed horizontal mirror of the complete native atlas by512 VRAM words gives
`x512,y472,width512,height40`. This is not an arbitrary chosen hole: it preserves
all glyph UVs and all atlas-relative pixels/CLUTs. In the existing right-half
upload domain it is exactly `76000/A000`, ending at`80000`.

Only three renderer constant operands would change:

| Native source PC | Before word | Mirrored requirement | Meaning |
|---|---|---|---|
| `80022978` | `340C7FD6` | `340C7FF6` | ordinary CLUT x352→864, y511 unchanged |
| `80022990` | `340C7F96` | `340C7FB6` | controller CLUT x352→864, y510 unchanged |
| `800229C0` | `20420010` | `20420018` | page base16→24; glyph page index remains0..7 |

These are exposed as exact-preimage **requirements**, not applied patches. In
particular, the ordinary CLUT load is a native branch delay word and must be
rewritten through the owned instruction ledger, not a blind literal search.

Two implementation paths could eventually establish ownership:

1. **Permanent per-level reservation.** A complete final right-half upload
   ownership partition must reserve `76000/A000` for the font, preserving or
   relocating every existing terrain, actor, player, particle, HUD and global
   consumer. The existing `NativeId65VramAssembly` can protect this complete
   range and reject even a one-byte zero-valued source owner. Synthetic tests
   exercise exactly that rule. No actual target currently grants this range;
   zeros or lack of a terrain reference are not sufficient.
2. **Modal-exclusive temporary ownership.** The mirrored rectangle is below
   the native backdrop's225 rows and does not intersect that backdrop/palette,
   but it overlaps ordinary level texture storage. A modal controller could
   suspend every ordinary consumer, drain GPU work, back up the full A000
   bytes to separately owned RAM, upload the atlas, complete all font draws,
   restore the exact texture bytes, drain again, then resume ordinary rendering.
   Every close, reset, death, level transition and reentry path must restore or
   invalidate/reload the resource. The extra40,960-byte RAM owner and GPU queue
   peak must be proved. Mere modal game state or a rectangle below the backdrop
   does not certify any of these transitions.

Native USA services already identified, with full wrapper preimages captured:

- Upload (`LoadImage`): `8005FA28/64`, SHA256
  `18db14f842d5a903afb21394f277e4f3cbe92358f111c16e4767ba9c21119713`.
- Readback (`StoreImage`): `8005FA8C/64`, SHA256
  `8952ec7779ea4c783a6576c4df3d08acd05470acdcca263d322005c84cea3053`.
- Draw synchronization: `8005F764/6C`, SHA256
  `0e573f70016f10b87ac196ff7fa304e28d16549d9b1c5ef11f756f248e12689d`.

These wrappers dispatch through the native GPU service table at`80074A5C`;
capturing their bytes does not itself install a new callback or prove all
asynchronous queue/lifetime behavior. No speculative writer is provided.

`RequireUsaInstallation`, `RequireExistingRightHalfMaterializerDomain`, and
`RequireMirroredRightHalfInstallation` fail with the particular missing owner.
Source capture, footprint completeness and synthetic composition never turn
`CompleteUsaVramLifetimeProven`, `UsaInstallationApproved`, or
`PublicationApproved` true. This is an actionable dependency boundary, not
completion of Japanese signs in USA.

## Focused validation

After the parent released the full-regression build lock, a coordinated Debug
build completed with zero warnings/errors. The Japanese message smoke passed
38,220 source-free checks; the dual-source `--font-sources` route passed623
combined CPU relocation and VRAM contract checks against the exact original
Japanese and USA discs. Both routes report zero candidate writes; the latter
retains `runtimeApproved=false`. `git diff --check` passed. No broad QA run was
repeated, and no original or earlier candidate/runtime artifact was modified.
