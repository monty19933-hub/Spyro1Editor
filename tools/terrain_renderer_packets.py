"""Native GPU packet triangle decoders; source preimages checked by driver."""
from collections import Counter
from terrain_native_readback import word
from terrain_sector_readback import decoded

def triangle(corners):
    xyz = [p for p, c in corners]
    u = [xyz[1][i] - xyz[0][i] for i in range(3)]
    v = [xyz[2][i] - xyz[0][i] for i in range(3)]
    if all((u[(i + 1) % 3] * v[(i + 2) % 3] - u[(i + 2) % 3] * v[(i + 1) % 3] == 0 for i in range(3))):
        return None
    return min((tuple(corners[i:] + corners[:i]) for i in range(3)))

def hp(s):
    lv, lc, lf, _, hv, hc, hf, _ = s[16:24]
    verts = 28 + 4 * (lv + lc + 2 * lf)
    near = verts + 4 * hv
    far = near + 4 * hc
    faces = far + 4 * hc
    out = Counter()
    for i in range(hf):
        at = faces + 16 * i
        ids = s[at:at + 4]
        colors = s[at + 4:at + 8]
        flags = word(s, at + 12)
        assert all((q < hv for q in ids)) and all((q < hc for q in colors))
        raw = [(decoded(s, verts + 4 * v), word(s, far + 4 * c) & 16777215) for v, c in zip(ids, colors)]
        packet = [raw[3], raw[2], raw[0], raw[1]]
        tris = [packet[:3]] + ([] if ids[0] == ids[1] else [[packet[2], packet[1], packet[3]]])
        for tri in tris:
            if flags & 2:
                tri = [tri[1], tri[0], tri[2]]
            t = triangle(tri)
            if t:
                out[t, bool(flags & 4), flags & 248] += 1
    return out

def lp(s):
    lv, lc, lf, _, hv, hc, hf, offset = s[16:24]
    assert lv <= 63 and lc <= 63 and (offset == lv + lc + 2 * lf)
    colors = 28 + 4 * lv
    faces = colors + 4 * lc
    out = Counter()
    for i in range(lf):
        at = faces + 8 * i
        v, c = (word(s, at), word(s, at + 4))
        ids = [v >> shift & 63 for shift in (26, 20, 14, 8)]
        shades = [c >> shift & 63 for shift in (26, 20, 14, 8)]
        assert all((q < lv for q in ids)) and all((q < lc for q in shades))
        assert v & 31 == 0 and c & 7 == 0, ('generated transitionbias0/opaque blend', i, hex(v), hex(c))
        packet = [(decoded(s, 28 + 4 * j), word(s, colors + 4 * k) & 16777215) for j, k in zip(ids, shades)]
        for tri in [packet[:3]] + ([] if ids[2] == ids[3] else [[packet[2], packet[1], packet[3]]]):
            t = triangle(tri)
            if t:
                out[t, bool(v & 128), c & 248] += 1
    return out
