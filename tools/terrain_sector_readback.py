"""Independent native HP/LP decoder used only by verification tools."""
from terrain_native_readback import word

def sector(env, i):
    at = 4 + word(env, 8 + 4 * i)
    lpv, lpc, lpf, _, hpv, hpc, hpf, offset = env[at + 16:at + 24]
    end = at + 28 + 4 * (lpv + lpc + 2 * lpf + hpv + 2 * hpc + 4 * hpf)
    return env[at:end]

def decoded(s, at):
    packed = word(s, at)
    xy, zpos = (word(s, 8), word(s, 12))
    x = (xy >> 16) + (packed >> 21 & 2047)
    y = (xy & 65535) + (packed >> 10 & 2047)
    z = (zpos >> 14 & 65535) // 4 + (packed & 1023)
    if word(s, 4) & 4096:
        z //= 8
    return (x, y, z)

def hpFaces(s, near=False):
    lpv, lpc, lpf, _, hpv, hpc, hpf, _ = s[16:24]
    verts = 28 + 4 * (lpv + lpc + 2 * lpf)
    colours = verts + 4 * hpv
    faces = colours + 8 * hpc
    out = []
    for f in range(hpf):
        at = faces + 16 * f
        vs = s[at:at + 4]
        cs = s[at + 4:at + 8]
        assert all((i < hpv for i in vs)) and all((i < hpc for i in cs))
        out.append(tuple(((decoded(s, verts + 4 * i), word(s, colours + 4 * c + (4 * hpc if near else 0))) for i, c in zip(vs, cs))))
    return out

def lpFaces(s):
    lpv, lpc, lpf, _, hpv, hpc, hpf, offset = s[16:24]
    assert lpv <= 63 and lpc <= 63
    verts = 28
    colours = 28 + 4 * lpv
    faces = colours + 4 * lpc
    out = []
    for f in range(lpf):
        at = faces + 8 * f
        v, c = (word(s, at), word(s, at + 4))
        ids = [v >> shift & 63 for shift in [26, 20, 14, 8]]
        shades = [c >> shift & 63 for shift in [26, 20, 14, 8]]
        assert all((i < lpv for i in ids)) and all((i < lpc for i in shades))
        out.append(tuple(((decoded(s, verts + 4 * i), word(s, colours + 4 * j)) for i, j in zip(ids, shades))))
    return out

def canonical(face):
    return min((tuple(face[i:] + face[:i]) for i in range(4)))
