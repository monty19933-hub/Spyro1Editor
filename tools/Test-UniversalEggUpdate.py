#!/usr/bin/env python3
"""Execute emitted egg update in strict scalar MIPS; effects are explicit test doubles.

This verifies control flow, bit/address binding and retirement admission, not actual
particles, native unlink/audio, gameplay, or card I/O. No ROM input is required.
"""
import argparse,hashlib,json,struct,subprocess
from pathlib import Path
from SpyroEggScalarMips import Scalar,require
ROOT=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet',default='dotnet');p.add_argument('--dll',type=Path,default=ROOT/'src/Spyro.Editor.UniversalIdentitySmoke/bin/Release/net10.0/Spyro.Editor.UniversalIdentitySmoke.dll')
a=p.parse_args();em=json.loads(subprocess.run([a.dotnet,str(a.dll),'--emit-egg-update'],capture_output=True,text=True,check=True,timeout=30).stdout)
b=bytes.fromhex(em['code']);entry=em['address'];code={entry+i:struct.unpack_from('<I',b,i)[0] for i in range(0,len(b),4)}
classes=set(em['classes']);PARTICLE=0x800D0000;MARK=0x8003B854;FREE=0x80052568;STOP=0xDEADBEE0;BASE=0x80100000
checks=[];results=[]
def check(ok,why):require(ok,why);checks.append(why)
def fixture(cls,row=195,level=2,found=False,near=True,count=256,state=0,pointer=None):
 m=Scalar(code)
 for addr,size in [(0x800700F4,12),(0x80075828,4),(0x80075890,4),(0x80075964,4),(0x800758E4,4),(0x80078A58,12),(0x800778B8,32),(0x80077908,0x480),(BASE,0x6000),(0x80120000,0x58),(0x801EF000,0x1000)]:m.map(addr,size)
 actor=BASE+row*0x58 if pointer is None else pointer
 m.poke(0x800700F4,actor);m.poke(0x80075828,BASE);m.poke(0x80075890,BASE+count*0x58);m.poke(0x80075964,level);m.poke(0x800758E4,PARTICLE)
 m.poke(actor+0x36,cls,2);m.poke(actor+0x48,state,1);m.poke(actor+0x46,17,1);m.poke(actor+0x3E,0xAB,1);m.poke(actor+0x3F,0xCD,1)
 for off in (0xC,0x10,0x14):m.poke(actor+off,0 if near else 0x10000)
 # Nonzero neighboring bits detect wrong word, row, and level selection.
 m.poke(0x80077908+((level if level<36 else 0)*32),0x1249)
 durable=0x80077908+level*32+(row>>5)*4
 if level<36 and row<256:
  existing=m.read(durable);bit=1<<(row&31);m.poke(durable,(existing|bit) if found else (existing&~bit))
 m.r[29]=0x801EFFF0;m.r[31]=STOP;m.r[16]=0xABC123;m.r[17]=0xDEF456;m.writes.clear();m.reads.clear()
 return m,actor,durable

def frame(m,actor,row,level):
 effects=[]
 m.r[31]=STOP
 pc=entry
 while True:
  stopped=m.run(pc,{STOP,PARTICLE,MARK,FREE},maximum=10000)
  if stopped==STOP:break
  effects.append(stopped)
  if stopped==PARTICLE:
   check(m.r[4:8]==[0x10,0x4D,actor+0xC,0],'particle ABI arguments are exact')
  elif stopped==MARK:
   check(m.r[4]==0 and m.r[5]==actor,'MarkCollected zero gem value binds exact actor')
   # Explicit narrow effect double; native award arithmetic is verified separately.
   for word in [0x800778B8+(row>>5)*4,0x80077908+level*32+(row>>5)*4]:m.write(word,m.read(word)|(1<<(row&31)),4)
  elif stopped==FREE:
   check(m.r[4]==actor,'FreeMoby receives exact actor');m.write(actor+0x48,255,1)
  pc=m.r[31]
 return effects

# All 512 valid class IDs: only the seven intended IDs may enter pickup logic.
for cls in range(512):
 m,actor,_=fixture(cls);effects=frame(m,actor,195,2)
 check(effects==([PARTICLE,MARK,FREE] if cls in classes else []),f'class{cls:03X} selection is exact')
 check(m.bytes(actor+0x3E,2)==bytes([0xAB,0xCD]),'update never rewrites either animation-frame byte')
 check((m.bytes(actor+0x46,1)[0]==20)==(cls in classes),'only selected egg classes spin')

for cls in sorted(classes):
 for level,row in [(0,0),(0,31),(1,32),(2,127),(29,128),(30,195),(35,255)]:
  for found in [False,True]:
   for near in [False,True]:
    m,actor,durable=fixture(cls,row,level,found,near);before=m.bytes(0x80077908,0x480);m.writes.clear();effects=frame(m,actor,row,level)
    expected=[FREE] if found else [PARTICLE,MARK,FREE] if near else []
    check(effects==expected,f'cls{cls:03X} level{level} row{row} found{found} near{near}: exact effect sequence')
    after=m.bytes(0x80077908,0x480)
    if not found and near:
     expect=bytearray(before);expect[level*32+row//8]|=1<<(row&7);check(after==expect,'pickup mutates only exact durable bit')
    else:check(after==before,'retirement/spin preserves every durable bit')
    if found:check(not any(addr==actor+0x46 for addr,_,_ in m.writes),'found egg retired before spin')
    check(m.bytes(actor+0x3E,2)==bytes([0xAB,0xCD]),'blink frame bytes remain unchanged')
    check(m.r[29]==0x801EFFF0 and m.r[31]==STOP and m.r[16]==0xABC123 and m.r[17]==0xDEF456,'caller stack and preserved registers survive update')
    if not found and near:
     m.poke(actor+0x48,0,1) # cold/reentry actor reconstruction, while durable RAM persists
     m.writes.clear();second=frame(m,actor,row,level);check(second==[FREE],'reconstructed already-collected actor retires without second pickup')

# Invalid placed owners and dynamic pointers fail before touching progress or pose.
for row,level,count,pointer,state in [(256,2,258,None,0),(257,2,258,None,0),(195,36,256,None,0),(195,0xFFFFFFFF,256,None,0),(195,2,256,0x80120000,0),(0,2,256,BASE+4,0),(195,2,195,None,0),(195,2,256,None,128),(195,2,256,None,255)]:
 m,actor,_=fixture(next(iter(classes)),row,level,count=count,pointer=pointer,state=state);before=m.bytes(0x80077908,0x480);effects=frame(m,actor,row,level)
 check(effects==[] and m.bytes(0x80077908,0x480)==before,'invalid owner/state never issues pickup/retirement or changes bitmap')
 check(not any(addr in range(actor,actor+0x58) for addr,_,_ in m.writes),'invalid owner/state never changes actor pose')
print(json.dumps({'marker':'UNIVERSAL_EGG_UPDATE_SCALAR_OK','checks':len(checks),'classIds':sorted(classes),'codeBytes':len(b),'codeSha256':hashlib.sha256(b).hexdigest(),'helperEffectsMocked':True,'nativeUnlinkExecuted':False,'nativeCardIO':False,'nativeRuntime':False},indent=2))
