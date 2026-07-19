# Native terrain HP coordinate provenance

Date: 2026-07-16

Status: implemented and smoke-locked as source-scene overlay format 7. This is
source data preservation for the native base projector; it is not wired into
the Avalonia viewport yet.

## Contract

The root marker is:

```text
hpCoordinatePayload = raw-sector-header-words0-3-integer-world-xz-quarter-residue-v1
```

Each `sectorRenderMetadata` record retains all four native sector header words:

- `nativeCenterXyWord` (+0x00);
- `nativeCenterZRadiusFlagsWord` (+0x04);
- `nativeXyPositionWord` / retail `xyPos` (+0x08);
- `nativeZPositionWord` / retail `zPos` (+0x0C).

It also serializes the decoded integer origin, X/Z quarter residues, and the
header-bit-12 special-Z flag. The loader and cache-health path recompute and
compare the existing center, radius, render flags, origin, residues, and special
flag from the raw words. Source-provenance overlays fail closed when any field
is missing, malformed, inconsistent, or does not cover every HP face in its
declared sector. Provenance-free legacy overlays remain on their compatibility
path.

Header bit 12 is explicitly unsupported. The source exporter refuses to stamp
an overlay containing it, and both the loader and reconstruction helper reject
it instead of applying an inferred coordinate transform.

## Certified integer-world reconstruction

`NativeTerrainHighPolyCoordinates.ReconstructCertifiedWorldPointX4` accepts a
typed raw sector payload and an integer decoded world point from that sector's
original HP vertex lattice. It bounds the local point to the native packed
fields, requires retail-packed-scene-word certification, and returns the shared
`SpyroRetailHighPolyWorldPoint4` type with independently-verified-coordinate
certification:

```text
localX = worldX - decodedOriginX     (0..0x1FFC/4)
localY = worldY - decodedOriginY     (0..0x1FFC/4)
localZ = worldZ - decodedOriginZ     (0..0x0FFC/4)

X4 = (xyPos >> 14) + 4*localX
Y4 = ((xyPos & 0xFFFF) << 2) + 4*localY
Z4 = (zPos >> 14) + 4*localZ
```

This keeps the two X/Z sector-origin residue bits that the compatibility
integer world point cannot express by itself.

## All-35 source audit lock

The focused source-scene smoke exports and reloads every current USA level and
locks:

- 35 levels;
- 6,226 raw sector headers;
- 6,047 HP-bearing sectors;
- 190,640 HP faces with certified sector coverage;
- all-sector X residue inventory `[6226,0,0,0]`;
- all-sector Z residue inventory `[6047,0,0,179]`;
- HP-bearing X and Z residue inventories `[6047,0,0,0]`;
- raw four-word sector stream SHA-256
  `254839222C9494316643667286565D854B8E4ED8404A5C8DAD03CB72E9BED271`;
- certified HP face-point stream SHA-256
  `DA713CBFF1D0E291491E4B1FA409DCB1F681F9F244440D5E3E5CA570FE138237`.

The same smoke covers all 16 synthetic X/Z residue pairs, native local-field
maxima, below/above-lattice rejection, missing and malformed raw words,
raw/derived center/origin/residue mismatches, incomplete face-sector coverage,
uncertified-input rejection, and unsupported header-bit-12 rejection.

After the locked smoke passed, the live workspace cache was regenerated from
the selected source disc. All 35 overlays under `editor-cache` now advertise
format 7 and the coordinate contract above.
