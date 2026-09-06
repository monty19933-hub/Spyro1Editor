"""Independent raw source splice/coordinate oracle, no Core or native execution.

Selected source support bytes and original/pair center policy are checked here.
The complete continuous dry corridor and native tree membership remain separate
C# source checks; this script does not relabel sampled support as that proof.
"""
from fractions import Fraction
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys

# Imported read-only fixture helpers must not leave generated files in src/.
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("raw_append", ROOT / "src/Spyro.Editor.NativeWizardAppendSmoke/source_wizard_append_oracle.py")
raw = importlib.util.module_from_spec(spec)
spec.loader.exec_module(raw)
sha, word, put = raw.sha, raw.word, raw.put
INTENTS = [("z", 92832, 25248, 10445, 91808, 25248),
           ("a", 86688, 25248, 10445, 87712, 25248)]


def point_distance(a, b, p):
    dx, dy = b[0]-a[0], b[1]-a[1]
    ex, ey = p[0]-a[0], p[1]-a[1]
    length, dot = dx*dx+dy*dy, dx*ex+dy*ey
    if dot <= 0 or length == 0:
        return Fraction(ex*ex+ey*ey)
    if dot >= length:
        return Fraction((ex-dx)**2+(ey-dy)**2)
    return Fraction((ex*dy-ey*dx)**2, length)


def main(path):
    with open(path, "rb") as image:
        def original():
            assert image.seek(0, 2) == 661547040
            image.seek(0)
            assert hashlib.file_digest(image, "sha256").hexdigest() == raw.IMAGE_SHA
            assert image.tell() == 661547040 and image.seek(0, 2) == 661547040

        def read(offset, length):
            out = bytearray()
            while length:
                n = min(length, 2048-offset % 2048)
                image.seek((37+offset//2048)*2352+24+offset % 2048)
                part = image.read(n)
                assert len(part) == n
                out.extend(part); offset += n; length -= n
            return bytes(out)

        original()
        header = read(0, 0x800)
        assert (word(header, 112), word(header, 116)) == (0xF01000, 0x28D800)
        assert (word(header, 320), word(header, 324)) == (0x2F6B000, 0x28A000)
        dark = read(0xF01000, 0x28D800)
        donor_data = read(0x2F6B000, 0x28A000)
        assert sha(dark) == "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97"
        assert sha(donor_data) == "9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8"
        source = dark[0x194000:0x1A5800]
        donor = donor_data[0x1CE800:0x1E0000]
        moby, count, props, end, fixups, _, fields = raw.scene_layout(source)
        assert (moby, count, props, end, fixups, len(fields)) == (0x8C30,128,0xE038,0xEED4,0x10EF0,147)
        assert sha(donor[0x6388:0x63E0]) == "59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82"
        assert sha(donor[0xC350:0xC3A0]) == "5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b"
        assert word(donor, 0xC388)-word(donor, 0x639C) == 287

        outputs = []
        nearest = []
        for family,x,y,z,ex,ey in INTENTS:
            best = min((point_distance((x,y),(ex,ey),struct.unpack_from("<ii",source,moby+8+i*0x58+12)),i) for i in range(count))
            assert best[0] > 3072**2
            nearest.append({"familyId":family,"residentIndex":best[1],"squaredDistance":str(best[0])})
        # The selected pair is exactly collinear and disjoint: inner endpoints
        # are4096 apart. This fixture arithmetic does not stand in for a general
        # segment intersection algorithm in the source-free tests.
        assert INTENTS[0][2] == INTENTS[0][5] == INTENTS[1][2] == INTENTS[1][5]
        assert INTENTS[0][4]-INTENTS[1][4] == 4096

        for extra in (1, 2):
            base = bytearray(source)
            delta = extra*0x50
            base[end+delta:fixups+delta] = source[end:fixups]
            put(base,moby+4,count+extra); put(base,props,word(source,props)+delta)
            new_fields = list(fields)
            owners = []
            for i in range(extra):
                row, private = moby+8+(count+i)*0x58, end+i*0x50
                base[row:row+0x58] = donor[0x6388:0x63E0]
                base[private:private+0x50] = donor[0xC350:0xC3A0]
                put(base,row,private); put(base,private,private+0x28)
                new_fields.extend((row,private)); owners.append((row,private))
            put(base,fixups+delta,len(new_fields))
            for i,f in enumerate(new_fields): put(base,fixups+delta+4+i*4,f)
            assert raw.scene_layout(base)[-1] == new_fields
            final = bytearray(base); mutations = []
            for i,(family,x,y,z,ex,ey) in enumerate(INTENTS[:extra]):
                row,private = owners[i]
                values = [("row.x",row+12,x),("row.y",row+16,y),("row.z",row+20,z),
                          ("route0.x",private+0x30,x),("route0.y",private+0x34,y),("route0.z",private+0x38,z+287),
                          ("route1.x",private+0x40,ex),("route1.y",private+0x44,ey),("route1.z",private+0x48,z+287)]
                for name,at,value in values:
                    mutations.append({"familyId":family,"nativeIndex":count+i,"field":name,"sceneOffset":at,"before":word(base,at),"after":value})
                    put(final,at,value)
            inverse = bytearray(final)
            for m in mutations: put(inverse,m["sceneOffset"],m["before"])
            assert inverse == base
            assert raw.scene_layout(final)[-1] == new_fields
            assert final[moby+8:moby+8+count*0x58] == source[moby+8:moby+8+count*0x58]
            assert len(set(m["sceneOffset"] for m in mutations)) == 9*extra
            if extra == 1: assert sha(final) == "6b34477c459224cbbf4153336d9c4f7e2aada20d9085c975c1eca2ef74c0d16f"
            else: assert sha(final) == "9dbdf8839f19cf31becda27b52b5a854f6badb8b59ad1db110c921055e5263dc"
            outputs.append({"familyCount":extra,"baseSceneSha256":sha(base),"finalSceneSha256":sha(final),"mutations":mutations})

        at = word(dark,8)
        for _ in range(4): at += word(dark,at)
        body = at+4; tri = body+word(dark,body+16); flags = body+word(dark,body+24)
        support = []
        for i in (874,875,921,927,876,877,899):
            p = tri+12*i; x,y,z = struct.unpack_from("<III",dark,p)
            signed9 = lambda w:(w&511)-512 if w&256 else w&511
            a = ((x&16383)*16,(y&16383)*16,(z&16383)*16)
            b = (a[0]+signed9(x>>14)*16,a[1]+signed9(y>>14)*16,a[2]+((z>>16)&255)*16)
            c = (a[0]+signed9(x>>23)*16,a[1]+signed9(y>>23)*16,a[2]+(z>>24)*16)
            assert a[2] == b[2] == c[2] == 10240 and z&0xC000 == 0
            # Native flag entries omitted past the declared count default FF;
            # never read unrelated following bytes as per-triangle flags.
            flag = dark[flags+i] if i < word(dark,body+4) else 255
            assert flag in (63,255)
            assert (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]) < 0
            support.append({"index":i,"wadOffset":0xF01000+p,"raw12":dark[p:p+12].hex(),"flag":flag,
                            "flagStored":i < word(dark,body+4),"xyz":[a,b,c]})
        original()
        print(json.dumps({"marker":"NATIVE_WIZARD_COUNT_PLACEMENT_RAW_ORACLE_OK","sourceImageSha256":raw.IMAGE_SHA,
                          "outputs":outputs,"nearestOriginalCenters":nearest,"pairSquaredDistance":4096**2,
                          "selectedSupports":support,"completeCorridorTreeProven":False,"placementApproved":False,
                          "nativeExecution":False,"publicationApproved":False},indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2: raise SystemExit("Usage: source_count_placement_oracle.py original.bin")
    main(sys.argv[1])
