#!/usr/bin/env python3
"""Authenticated SCPS-10083 font inspection, never a disc/editor exporter.

The pure decoder models the unscaled sign renderer's axis-aligned sampling.
PNG inspection requires Pillow; source-free tests do not. Palette pixels are
shown unmodulated (not a claim of emulator/GPU runtime validation).
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys

FONT_BYTES = 0xA000
FONT_SHA = "917a2c272859d99b573ee3f79b1a62ca847dff218802b2c65f80bb55cb888ff1"
TABLE_SHA = "3ebf51c29f337e461de260343b0e11b85eadeec9972b3eefd1afd04b33085913"
COLOR_SHA = "6f267eba5b81e5ae2abd5a82d393938a6be96b1db08bf1bd56bcee67b6837f75"
RENDERER_SHA = "592e7b33f9ed7f898b0ce475a8567aad7d6cd69c7c28a14c9c30cc078d799820"
CALLER_SHA = "7bcf0a02497ffc0a0c36a334362a6b5c04725cde12063e9dd3e1141902659d6b"


def is_glyph(code: int) -> bool:
    return (1 <= code <= 0x18 or 0x21 <= code <= 0x29 or 0x2B <= code <= 0x39
            or code in (0x3D, 0x3F) or 0x80 <= code <= 0xFC)


@dataclass(frozen=True)
class SignQuad:
    texture_page: int
    kind: int
    width: int
    height: int
    # Native packet vertex order: upper-left/right, lower-left/right.
    uv: tuple[tuple[int, int], ...]


def sign_quad(descriptor: int) -> SignQuad:
    """Decode PC800229B8..80022AF8, with caller a3=0 => t0=24,t1=16.

    Screen coordinates have exclusive right/bottom rasterization boundaries.
    Format1 transposes axes. Its u=F0 case subtracts one from both the screen
    height and endpoint U, avoiding an 8-bit endpoint wrap from 256 to zero.
    """
    if type(descriptor) is not int or not 0 <= descriptor <= 0x7FB or descriptor & 4:
        raise ValueError("Descriptor has bits outside the observed font schema")
    kind, u = descriptor & 3, descriptor & 0xF8
    width = 24 if kind == 0 else 23
    height = 15 if kind == 3 or (kind == 1 and u == 0xF0) else 16
    if kind == 1:
        uv = ((u, 232), (u, 255), (u + height, 232), (u + height, 255))
    else:
        v = 236 if kind == 3 else 216
        uv = ((u, v), (u + width, v), (u, v + height), (u + width, v + height))
    if any(not 0 <= c <= 255 for point in uv for c in point):
        raise ValueError("Sign-mode texture endpoint would wrap; no such source glyph is admitted")
    return SignQuad(16 + ((descriptor >> 8) & 7), kind, width, height, uv)


def nibble(data: bytes, page: int, u: int, v: int) -> int:
    if len(data) != FONT_BYTES or not 16 <= page <= 23 or not 0 <= u <= 255 or not 216 <= v <= 255:
        raise ValueError("Texel escapes the identified 512-word by 40-row VRAM upload")
    # PS1 4bpp: each page step is 64 VRAM words = 256 decoded texels.
    x, y = (page - 16) * 256 + u, 256 + v - 472
    return (data[y * 1024 + x // 2] >> (4 * (x & 1))) & 15


def glyph_indices(data: bytes, descriptor: int) -> tuple[SignQuad, bytes]:
    quad = sign_quad(descriptor)
    u, v = quad.uv[0]
    result = bytes(nibble(data, quad.texture_page,
                          u + (y if quad.kind == 1 else x),
                          v + (x if quad.kind == 1 else y))
                   for y in range(quad.height) for x in range(quad.width))
    return quad, result


def palette_words(data: bytes, code: int) -> tuple[int, ...]:
    if len(data) != FONT_BYTES or type(code) is not int or not 0 <= code <= 255:
        raise ValueError("Invalid font owner or glyph code")
    # CLUT 7FD6: (352,511); colored controller icons F7..FA use 7F96:(352,510).
    y = 38 if 0xF7 <= code <= 0xFA else 39
    return struct.unpack_from("<16H", data, y * 1024 + 352 * 2)


def rgba(word: int) -> tuple[int, int, int, int]:
    if type(word) is not int or not 0 <= word <= 0xFFFF:
        raise ValueError("A CLUT entry is one unsigned native halfword")
    channels = [(word >> shift) & 31 for shift in (0, 5, 10)]
    return tuple((c << 3) | (c >> 2) for c in channels) + (0 if word == 0 else 255,)


def capture(path: Path) -> tuple[dict, bytes, tuple[int, ...]]:
    spec = importlib.util.spec_from_file_location("jp_font_source", Path(__file__).with_name("Inspect-SpyroJapaneseCollectibles.py"))
    source = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = source
    spec.loader.exec_module(source)
    image = source.AuthenticatedImage(path, source.JP)
    try:
        entry = struct.unpack("<II", image.read(500, 2 * 8, 8))
        if entry != (0x6F800, FONT_BYTES):
            raise ValueError("Font directory owner changed")
        ranges = (("font", 500, entry[0], entry[1], FONT_SHA),
                  ("descriptors", 24, 0x69DB8, 512, TABLE_SHA),
                  ("modulationColors", 24, 0x69D98, 32, COLOR_SHA),
                  ("renderer", 24, 0x13068, 0x2D4, RENDERER_SHA),
                  ("signCaller", 24, 0xBA68, 0xA0, CALLER_SHA))
        blobs, evidence = {}, []
        for name, lba, offset, size, expected in ranges:
            data = image.read(lba, offset, size)
            if hashlib.sha256(data).hexdigest() != expected:
                raise ValueError(f"Pinned font evidence changed: {name}")
            blobs[name] = data
            evidence.append(dict(id=name, lba=lba, logicalOffset=offset, byteLength=size, sha256=expected))
        font = blobs["font"]
        table = struct.unpack("<256H", blobs["descriptors"])
        glyphs = []
        for code, descriptor in enumerate(table):
            if not is_glyph(code):
                continue  # Descriptor zero is not proof that controls render as hiragana あ.
            quad, indices = glyph_indices(font, descriptor)
            words = palette_words(font, code)
            glyphs.append(dict(code=f"{code:02X}", descriptor=f"{descriptor:04X}",
                               texturePage=quad.texture_page, format=quad.kind,
                               width=quad.width, height=quad.height, uv=quad.uv,
                               indicesSha256=hashlib.sha256(indices).hexdigest(),
                               nontransparentTexels=sum(words[index] != 0 for index in indices),
                               clut="7F96" if 0xF7 <= code <= 0xFA else "7FD6"))
        report = dict(schema=1, revision="SCPS-10083", sourceSha256=source.JP.digest,
                      nativeImportApproved=False, sourceReadOnly=True,
                      sourceHeldAndNamedRechecked=True, evidence=evidence,
                      scope="Unscaled sign-mode source texels; no emulator/GPU runtime certification",
                      upload=dict(xWords=0, y=472, widthWords=512, height=40, bitsPerPixel=4),
                      modulationColors=[f"{x:08X}" for x in struct.unpack("<8I", blobs["modulationColors"])],
                      paletteWords={f"{code:02X}": [f"{w:04X}" for w in palette_words(font, code)] for code in (0x80, 0xF7)},
                      glyphs=glyphs)
        return report, font, table
    finally:
        image.close()  # Checks held descriptor and named image even on decode failure.


def render_grid(path: Path, font: bytes, table: tuple[int, ...]) -> None:
    from PIL import Image, ImageDraw
    grid = Image.new("RGB", (16 * 104, 16 * 100), "#252934")
    draw = ImageDraw.Draw(grid)
    for code, descriptor in enumerate(table):
        x, y = (code % 16) * 104, (code // 16) * 100
        draw.text((x + 2, y + 2), f"{code:02X} {descriptor:04X}", fill="white")
        if not is_glyph(code):
            draw.text((x + 2, y + 36), "control/unused", fill="#9da7b7")
            continue
        quad, indices = glyph_indices(font, descriptor)
        colors = [rgba(word) for word in palette_words(font, code)]
        glyph = Image.new("RGBA", (quad.width, quad.height))
        glyph.putdata([colors[index] for index in indices])
        glyph = glyph.resize((quad.width * 4, quad.height * 4), Image.Resampling.NEAREST)
        grid.paste(glyph, (x, y + 26), glyph)
    with path.open("xb") as output:
        grid.save(output, format="PNG")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True, help="Existing artifact folder; outputs must not exist")
    args = parser.parse_args()
    targets = [args.output_dir / name for name in ("font-decoder-report.json", "font-decoder-glyph-grid.png")]
    if not args.output_dir.is_dir() or any(path.exists() for path in targets):
        parser.error("Use an existing output folder with no font-decoder output files")
    report, font, table = capture(args.source)
    render_grid(targets[1], font, table)
    with targets[0].open("x") as output:
        json.dump(report, output, indent=2)
        output.write("\n")
    print(json.dumps(dict(marker="JAPANESE_FONT_DECODE_OK", glyphs=len(report["glyphs"]),
                          originalFinal=True, candidateWrites=0, artifacts=[str(path) for path in targets])))


if __name__ == "__main__":
    main()
