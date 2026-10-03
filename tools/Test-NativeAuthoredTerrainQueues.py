#!/usr/bin/env python3
"""Execute exact native integer queue producers with strict owner bounds.
No emulator, BIOS, card, or image writes. Usage: USA_OR_CANDIDATE_BIN REPORT_JSON.
"""
import hashlib,json,struct,sys
from pathlib import Path
from SpyroEggScalarMips import Scalar,require
source,report=map(Path,sys.argv[1:3])
def u32(b,o):return struct.unpack_from('<I',b,o)[0]
with source.open('rb') as f:
    def sector(lba):f.seek(lba*2352+24);return f.read(2048)
    root=sector(u32(sector(16),158));at=0;owner=None
    while root[at]:
        row=root[at:at+root[at]]
        if row[33:33+row[32]]==b'SCUS_942.28;1':owner=(u32(row,2),u32(row,10))
        at+=len(row)
    require(owner is not None and owner[1]==0x66000,'Exact executable directory owner')
    exe=b''.join(sector(owner[0]+i) for i in range(owner[1]//2048))
spans={0x80025BA4:'0000E5AD0400EF21',0x800261A4:'0000E0AD',
       0x800272D4:'02002B330400601D02003937825819000000ABAF0400BD23F0FF2B22024909002058690102006B350000ABAF0400BD2304000B20DD9A0008FFFFCBA2',
       0x8002768C:'0000A0AF',0x800267F0:'0300313326C831030800218F'}
code={}
for pc,hexdata in spans.items():
    blob=bytes.fromhex(hexdata)
    require(exe[pc-0x8000F800:pc-0x8000F800+len(blob)]==blob,f'Exact native producer/consumer {pc:08X}')
    for i in range(0,len(blob),4):code[pc+i]=u32(blob,i)
checks=0;cases=[]
for count,allowed in [(50,True),(63,True),(64,False),(127,False),(242,False)]:
    cpu=Scalar(code);cpu.map(0x80071AF4,0x200);cpu.map(0x8006FCF4,0x100);cpu.r[29]=0x80071AF4
    refused=False
    try:
        for i in range(count):
            cpu.r[25]=0x8008C000+i*116;cpu.r[17]=cpu.r[25]+0x70;cpu.r[9]=0;cpu.r[22]=0x8006FCF5+i%255
            cpu.run(0x800272D4,{0x80026B74},maximum=30)
        cpu.run(0x8002768C,{0x80027690},maximum=2)
    except AssertionError as e:
        require('Unmapped fixture write' in str(e),'Only strict owner overflow is expected');refused=True
    require(refused!=allowed,f'Close queue admitted boundary {count}');checks+=1
    require(all(a>=0x8006FCF4 and a+n<=0x8006FDF4 or a>=0x80071AF4 and a+n<=0x80071CF4 for a,n,_ in cpu.writes),'No writes outside exact native queue/cache owners');checks+=1
    cases.append(dict(queue='closeHQ',faces=count,strictOwnerOverflow=refused,instructions=cpu.instructions))
for count,allowed in [(50,True),(127,True),(128,False),(242,False)]:
    cpu=Scalar(code);cpu.map(0x800718F4,0x200);cpu.r[15]=0x800718F4;refused=False
    try:
        for i in range(count):
            cpu.r[5]=0x8008C000+i*116|3;cpu.run(0x80025BA4,{0x80025BAC},maximum=3)
        cpu.run(0x800261A4,{0x800261A8},maximum=2)
    except AssertionError as e:
        require('Unmapped fixture write' in str(e),'Only strict owner overflow is expected');refused=True
    require(refused!=allowed,f'HP queue admitted boundary {count}');checks+=1
    cases.append(dict(queue='HPsectors',sectors=count,strictOwnerOverflow=refused,instructions=cpu.instructions))
# Reproduce the first observed bad read after a compact HQ header replaces an
# HP sector pointer. Pipeline semantics complete ANDI then XOR before the LW.
cpu=Scalar(code);cpu.r[25]=0x20023862
try:cpu.run(0x800267F0,{0x80026800},maximum=4);raise AssertionError('Invalid header pointer accepted')
except AssertionError as e:require('Unmapped native read 20023868+4' in str(e),'Exact first observed invalid read');checks+=1
report.parent.mkdir(parents=True,exist_ok=True)
report.write_text(json.dumps(dict(sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),executableSha256=hashlib.sha256(exe).hexdigest(),checks=checks,cases=cases,nativeInstructionSpans=5,firstObservedReadReproduced='0x20023868',scalarOnly=True,runtimeVerified=False),indent=2)+'\n')
print(f'NATIVE_AUTHORED_TERRAIN_QUEUES_SCALAR_OK checks={checks} cases={len(cases)} firstBadRead=20023868')
