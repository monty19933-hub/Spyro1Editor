#!/usr/bin/env python3
"""Independent original-source scene construction and bounded native slices.

No Core, candidate, body issuer, callbacks, GTE or emulator. Explicit synthetic
entry registers and RAM owner; actual loader/allocator instructions, no stubs.
"""
import argparse
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
HELPER = 'src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_variable_native_loader_oracle.py'
HELPER_SHA = '75f6762822c79520176bb3127e9b426822fb7b007b2ba6da931e4822cb43f283'
SOURCES = {
    'exe': (53875*2048, 0x66000, 'a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998'),
    'town': (37*2048+0x119E000, 0x2E2000, '7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0'),
    'donor': (37*2048+0x2F6B000, 0x28A000, '9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8'),
}
PINS = (
    ('scene-global-reload',0x80013680,0x44,'a976eeeeeef392e893a723ae5ab9d633a63afcb1698b4204448c0ed28589778f'),
    ('scene-lists-arena-route-buffer-fixups',0x80013C4C,0x438,'934e58aeaecedcabdd0202befbb9b6a23554a187ae365eb2b2acb58ade87e250'),
    ('allocator-including-return-delay',0x800524C4,0xA4,'6625282792e81215f1f35e1aa8b4240937206bcc8bd494eef9e000f65a4bbf9e'),
)
BASE, EXE = 0x80100000, 0x8000F800


def require(ok, label):
    if not ok: raise AssertionError(label)


def sha(data): return hashlib.sha256(data).hexdigest()
def word(data, offset=0): return struct.unpack_from('<I',data,offset)[0]
def put(data, offset, value): struct.pack_into('<I',data,offset,value)


def helper():
    path = ROOT/HELPER
    require(sha(path.read_bytes()) == HELPER_SHA, 'Frozen native scalar helper changed')
    spec = importlib.util.spec_from_file_location('p1_native_scalar',path)
    module = importlib.util.module_from_spec(spec)
    with contextlib.redirect_stdout(io.StringIO()): spec.loader.exec_module(module)
    return module


def build(town, donor, reserve=0x58):
    """Construct from named original source packets, not a C# afterimage."""
    row = bytearray(donor[0x1D4B88:0x1D4BE0])
    private = bytearray(donor[0x1DAB50:0x1DABA0])
    require(sha(row)=='59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82','Whole original Wizard row')
    require(sha(private)=='5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b','Whole original Wizard private owner')
    require(word(row)==0xC350 and word(private)==0xC378,'Two original scene references')
    require(row[0x36:0x38]==bytes.fromhex('1b01') and row[0x3A]==0x7F and row[0x53]==0x56,'Native row class/kind/reward')
    require(word(private,0x28)==2 and word(private,0x2C)==0xFFFF0000,'Source two-point route descriptor')
    require(word(private,0x38)-word(row,20)==287 and word(private,0x48)==word(private,0x38),'Source route Z delta')
    donor_scene = donor[0x1CE800:0x1E0000]
    require(word(donor_scene,0xFC3C)==191,'Original donor fixup count')
    fixups = [word(donor_scene,0xFC40+4*i) for i in range(191)]
    require([f for f in fixups if 0x6388 <= f < 0x63E0]==[0x6388] and
            [f for f in fixups if 0xC350 <= f < 0xC3A0]==[0xC350],
            'Two exact fields, and no additional donor row/private fixup obligation')
    arena = 32*0x70 + reserve
    properties_at = 0x12C + arena
    private_at = properties_at + 4
    put(row,0,private_at); put(private,0,private_at+0x28)
    for at,value in ((12,8704),(16,8960),(20,8704)): put(row,at,value)
    for at,value in ((0x30,8704),(0x34,8960),(0x38,8991),(0x40,9216),(0x44,8960),(0x48,8991)):
        put(private,at,value)
    lists = b''.join(struct.pack('<II',n,0)+bytes(n-8) for n in (12,12,8,8,12,8,8))
    output = bytearray(struct.pack('<iiiBBBB',8960,8960,9216,0,0,64,0))
    output += town[0x1D0010:0x1D0088] + lists
    output += struct.pack('<II',8+len(row)+arena,1) + row + bytes(arena)
    require(len(output)==properties_at,'Computed full arena owner')
    output += struct.pack('<I',4+len(private)) + private
    output += struct.pack('<III',8,0,0x2004) + bytes(0x2000)
    output += struct.pack('<III',2,0xD4,private_at)
    output += bytes((-len(output))%0x800)
    require(len(output)==0x3000,'Bounded complete sector-aligned scene')
    return bytes(output), properties_at


def main(path, naive):
    scalar = helper()
    checks, refusals, cases = [], [], []
    executed = set()
    def check(ok,label): require(ok,label); checks.append(label)
    def refuse(action, phrase, label):
        try: action()
        except AssertionError as error:
            require(phrase in str(error),'Wrong refusal: '+str(error))
            refusals.append(dict(label=label,error=str(error))); return
        raise AssertionError('Expected refusal: '+label)
    def identity(image):
        scalar.native.identity(image)
        require(image.read(1)==b'' and image.tell()==scalar.native.IMAGE_LENGTH,'Actual full original EOF')

    with open(path,'rb') as image:
        identity(image)
        try:
            sources = {key:scalar.native.logical(image,at,n) for key,(at,n,_) in SOURCES.items()}
            for key,(_,_,digest) in SOURCES.items(): check(sha(sources[key])==digest,'Complete original '+key)
            code = {}
            for name,pc,n,digest in PINS:
                raw = sources['exe'][pc-EXE:pc-EXE+n]
                check(sha(raw)==digest,'Frozen native source '+name)
                for at in range(0,n,4):
                    require(pc+at not in code,'Overlapping code owner')
                    code[pc+at] = word(raw,at)
                bad = bytearray(raw); bad[-1] ^= 1
                refuse(lambda:require(sha(bad)==digest,'Instruction pin changed'),'pin changed','Mutation '+name)
            scene, properties_at = build(sources['town'],sources['donor'])
            check(sha(scene)=='b7bd222702ff9b5d6ea40c623159f4aa22b3425ef9368059167d1ccab930266b',
                  'Independently derived complete P1 scene golden, frozen before Core implementation')
            check(properties_at==0xF84 and word(scene,0xCC)==0xEB8 and word(scene,0xD0)==1,'Independent P1/N32 length/count')
            check(scene[0x2FF0:]==bytes(16) and word(scene,0x2FE4)==2 and word(scene,0x2FE8)==0xD4 and word(scene,0x2FEC)==0xF88,'Complete fixups and separate sector padding')

            def initialize(data):
                m = scalar.globals_machine(code)
                m.blob(BASE,data)
                m.poke(0x800785E4,BASE); m.poke(0x8007596C,65)
                m.run(0x80013680,{0x800136C4})
                require(m.r[16]==BASE and m.r[18]==BASE,'Actual scene pointer reload')
                m.r[16],m.r[17],m.r[21],m.r[2] = BASE+0x80,0,0,word(data,0x80)
                m.run(0x80013C4C,{0x80014084})
                executed.update(m.executed)
                return m

            m = initialize(scene)
            check(m.read(0x80075828)==BASE+0xD4 and m.read(0x80075890)==BASE+0x12C,'Native placed/dynamic bases differ by owned row58')
            check(m.read(0x800756E8)==BASE+0xF84 and m.read(0x80075930)==BASE+0xF84 and m.read(0x800756A8)==32 and m.read(0x800756A4)==0,'Native complete arena capacity and private cursor')
            check(m.read(0x80075778)==BASE+0xFE4 and m.read(0x800757B4)==0,'Native two bucket banks and empty pods')
            check(m.read(BASE+0xD4)==BASE+0xF88 and m.read(BASE+0xF88)==BASE+0xFB0,'Native both row/private fixups reach actual owners')
            check(m.read(BASE+0x12C+0x48,1)==255 and m.read(BASE+0xF83,1)==255,'Native row/private sentinels remain inside dynamic arena')
            loaded = m.bytes(BASE,len(scene))
            inverse = bytearray(loaded)
            for at in (0xD4,0xF88): put(inverse,at,word(scene,at))
            inverse[0x174]=scene[0x174]; inverse[0xF83]=scene[0xF83]
            check(bytes(inverse)==scene,'Full loader inverse changes only two fixups and two sentinels')

            def allocate_all(data, end, count):
                machine=initialize(data)
                before=machine.bytes(BASE,len(data))
                allocated=[]
                for index in range(count):
                    machine.r[31]=scalar.RETURN
                    start=len(machine.writes)
                    machine.run(0x800524C4,{scalar.RETURN})
                    require(machine.r[2]==BASE+0x12C+index*0x58,'Native row stride')
                    require(machine.read(machine.r[2])==BASE+end-(index+1)*0x18,
                            'Native private pointer is derived from the backward cursor')
                    current_owners = ((machine.r[2],0x58),(machine.read(machine.r[2]),0x18))
                    require(current_owners[0][0]+0x58 <= current_owners[1][0],
                            'Full current native row/private owners overlap')
                    for owner,length in current_owners:
                        for prior in allocated:
                            for earlier,extent in ((int(prior['row'],16),0x58),(int(prior['private'],16),0x18)):
                                require(not(owner < earlier+extent and earlier < owner+length),
                                        'Full native allocation owners overlap an earlier live owner')
                    new_writes=machine.writes[start:]
                    for address,width,_ in new_writes:
                        if BASE <= address < BASE+len(data):
                            require(BASE+0x12C <= address and address+width <= BASE+end,
                                    'Native write escaped declared dynamic arena')
                            for prior in allocated:
                                for owner, length in ((int(prior['row'],16),0x58),(int(prior['private'],16),0x18)):
                                    require(not(address < owner+length and owner < address+width),
                                            f'Native allocation overlaps prior live owner {owner:08X}+{length:X}: request {index+1}, prior {prior["index"]+1}, write {address:08X}+{width}')
                    allocated.append(dict(index=index,row=hex(machine.r[2]),private=hex(machine.read(machine.r[2])),writes=len(new_writes)))
                after=machine.bytes(BASE,len(data))
                require(after[:0x12C]==before[:0x12C] and after[end:]==before[end:],'Allocation changed another complete scene owner')
                executed.update(machine.executed)
                return machine,allocated

            short, short_end=build(sources['town'],sources['donor'],reserve=0)
            check(initialize(short).read(0x800756A8)==32,'Missing reserve still reports native capacity32')
            if naive:
                allocate_all(short,short_end,32)
                raise AssertionError('Missing reserve unexpectedly preserved complete dynamic ownership')
            refuse(lambda:allocate_all(short,short_end,32),'overlaps prior live owner','Actual no-reserve allocator write overlaps an earlier live allocation')
            m,allocations=allocate_all(scene,properties_at,32)
            check(m.read(0x800756A4)==32 and len(allocations)==32,'All32 actual allocator requests completed')
            check(m.read(0x8007573C)==BASE+0xC2C and m.read(0x80075930)==BASE+0xC84,'Native forward rows/backward properties leave58-byte next-row reserve')
            check(m.read(BASE+0xC2C+0x48,1)==255,'Native next-row sentinel is owned after allocation32')
            cases.extend(allocations)
            # A nominal count is not a native refusal guard. Execute one more
            # request separately and record its exact overlap with earlier data.
            before33=m.bytes(BASE,len(scene)); start=len(m.writes)
            m.r[31]=scalar.RETURN; m.run(0x800524C4,{scalar.RETURN})
            check(m.r[2]==BASE+0xC2C and m.read(0x800756A4)==33,'Native allocation33 is not refused at nominal32')
            writes33=m.writes[start:]
            check(any(BASE+0xC84 <= at < BASE+0xF84 for at,_,_ in writes33),'Allocation33 sentinel overwrites prior dynamic private area')
            check(m.bytes(BASE,0x12C)==before33[:0x12C] and m.bytes(BASE+0xF84,0x3000-0xF84)==before33[0xF84:],'Allocation33 witness still does not imply safe live dynamic ownership')
            executed.update(m.executed)
            check(sha((ROOT/HELPER).read_bytes())==HELPER_SHA,'Frozen scalar unchanged after source work')
            result=dict(marker='FRESH_P1_SCENE_NATIVE_SLICES_OK',checks=len(checks),refusals=refusals,
                        originalSha256=scalar.native.IMAGE_SHA,sourceOwners=SOURCES,sourcePins=PINS,
                        sceneSha256=sha(scene),sceneLength=len(scene),helperSha256=HELPER_SHA,
                        geometricIntent=dict(origin=[8704,8960,8704],endpoint=[9216,8960]),
                        allocations=cases,allocation33Writes=writes33,completedPcs=[hex(pc) for pc in sorted(executed)],
                        syntheticSceneBase=hex(BASE),actualIssuedFixtureCompared=False,
                        syntheticEntry=dict(globalReloadPc="0x80013680", afterReloadPc="0x80013c4c",
                                            s0="scene+0x80", s1=0, s5=0, v0="source scene word at0x80",
                                            placedUpdaterSkippedByExplicitS5=True, levelIdGlobal=65),
                        completeSceneInitializerExecuted=False,nativeArenaExhaustionSafe=False,
                        fullRuntimePeakProven=False,bodyOrPublicationApproved=False,noNativeHelperStubs=True)
        finally:
            identity(image)
            require(sha((ROOT/HELPER).read_bytes())==HELPER_SHA,'Frozen scalar changed during source work')
            scalar.require_helpers()
    result['sourceUnchanged']=True
    print(json.dumps(result,indent=2,sort_keys=True))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('original_bin')
    parser.add_argument('--naive-no-next-row-reserve',action='store_true')
    args=parser.parse_args()
    main(args.original_bin,args.naive_no_next_row_reserve)
