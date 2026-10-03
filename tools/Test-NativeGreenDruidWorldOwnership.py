#!/usr/bin/env python3
"""Exact native animation-group consumers plus unchanged foreign row indexing.
Scalar fixtures establish ownership effects; no emulator or image writes.
Usage: USA_BIN ALPINE_CLOSURE_JSON REPORT_JSON
"""
import hashlib,json,struct,sys
from pathlib import Path
from SpyroEggScalarMips import Scalar,require
source,closure_path,report=map(Path,sys.argv[1:4])
def word(b,o):return struct.unpack_from('<I',b,o)[0]
with source.open('rb') as f:
 def sector(lba):f.seek(lba*2352+24);return f.read(2048)
 root=sector(word(sector(16),158));at=0;owner=None;wad_owner=None
 while root[at]:
  row=root[at:at+root[at]]
  if row[33:33+row[32]]==b'SCUS_942.28;1':owner=(word(row,2),word(row,10))
  if row[33:33+row[32]].startswith(b'WAD.WAD'):wad_owner=(word(row,2),word(row,10))
  at+=len(row)
 require(owner is not None,'Exact native executable owner')
 exe=b''.join(sector(owner[0]+i) for i in range(owner[1]//2048))
 require(wad_owner is not None,'Native WAD owner');header=sector(wad_owner[0])
 def entry(index):
  offset=word(header,index*8);length=word(header,index*8+4);require(offset%2048==0,'Aligned WAD entry')
  return b''.join(sector(wad_owner[0]+offset//2048+i) for i in range((length+2047)//2048))[:length]
 overlay=entry(35);source_data=entry(36);host_data=entry(14)
require(hashlib.sha256(exe).hexdigest()=='a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998','Original complete native EXE')
code={pc:word(exe,pc-0x8000F800) for pc in range(0x8002B390,0x8002B444,4)}
checks=1;cases=[]
def fixture():
 cpu=Scalar(code);cpu.map(0x80078560,0x4C);cpu.map(0x80090000,9*12+4);cpu.map(0x800A0000,9*3*8);cpu.r[31]=0x80070000
 for table in range(9):
  cpu.poke(0x80078560+table*8,3);cpu.poke(0x80078564+table*8,0x80090000+table*12)
  for item in range(3):
   at=0x800A0000+(table*3+item)*8;cpu.poke(0x80090000+table*12+item*4,at);cpu.poke(at,(0 if item!=1 else 5)|(12<<8)|(7<<16)|(3<<24))
 return cpu
for group in (0,5,254):
 cpu=fixture();cpu.r[4]=group;cpu.r[5]=0xFC;cpu.r[6]=3;cpu.run(0x8002B390,{0x80070000},maximum=1500)
 expected=18 if group==0 else 9 if group==5 else 0
 require(len(cpu.writes)==expected and all(size==1 and address%8==1 for address,size,_ in cpu.writes),'Native flags change every matching animation record across all nine tables');checks+=1
 require(all(value==15 for _,_,value in cpu.writes),'Exact native mask/or flags retained');checks+=1
 cases.append(dict(function='8002B390',group=group,matchingRecordsWritten=len(cpu.writes),instructions=cpu.instructions))
 cpu=fixture();cpu.r[4]=group;cpu.r[2]=0x12345678;cpu.run(0x8002B3F4,{0x80070000},maximum=1500)
 if group!=254:require(cpu.r[2]==0x03070C,'Exact frame/flags/wait read from first matching original record');checks+=1
 else:require(cpu.r[2]!=0x12345678,'Missing ID does not supply a defined inactive result; delay slot retains last nonmatching record');checks+=1
 cases.append(dict(function='8002B3F4',group=group,result=f'{cpu.r[2]:08X}',instructions=cpu.instructions))
# Authenticate this exact original Alpine packet through a closure dumped from
# UniversalSourceLibrary; the data packet is independently checked below.
rows=json.loads(closure_path.read_text());alpine={row['Pc']:row['Word'] for row in rows}
require(all(word(overlay,pc-0x8007AA38)==w for pc,w in alpine.items()),'Every original Alpine closure instruction matches actual USA overlay');checks+=1
expected=[0x8E020008,0x3C038007,0x8C6358E4,0x00023840,0x00E23821,0x00073880,0x00E23823,0x3C028007,0x8C425828,0x000738C0,0x0060F809,0x00473821]
for i,w in enumerate(expected):require(alpine[0x80083A70+4*i]==w,'Exact Alpine terrain-target/particle packet');checks+=1
cpu=Scalar(alpine);cpu.map(0x800A8000,16);cpu.map(0x800758E4,4);cpu.map(0x80075828,4);cpu.poke(0x800A8008,48);cpu.poke(0x800758E4,0x800B0000);cpu.poke(0x80075828,0x80090000);cpu.r[16]=0x800A8000
cpu.run(0x80083A70,{0x800B0000},maximum=20)
require(cpu.r[7]==0x80090000+48*0x58,'Unrelocated props+8 selects host physical row48 rather than source class148 owner');checks+=1
report.write_text(json.dumps(dict(checks=checks,sourceOverlaySha256=hashlib.sha256(overlay).hexdigest(),sourceClosureWordsVerified=len(alpine),sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),executableSha256=hashlib.sha256(exe).hexdigest(),certifiedNativeInstructions=len(code)+len(expected),cases=cases,unrelocatedTargetAddress=f'{cpu.r[7]:08X}',scalarOnly=True,runtimeVerified=False),indent=2)+'\n')
print(f'NATIVE_GREEN_DRUID_WORLD_OWNERSHIP_OK checks={checks}')
