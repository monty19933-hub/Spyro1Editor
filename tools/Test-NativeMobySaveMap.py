#!/usr/bin/env python3
"""Independent strict scalar native save-map hooks; no ROM/card/emulator I/O.
Native award/audio/unlink are explicit ABI effect doubles. Native word-copy executes.
"""
import argparse,hashlib,json,struct,subprocess
from pathlib import Path
from SpyroEggScalarMips import Scalar,require
ROOT=Path(__file__).resolve().parent.parent
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dll',type=Path,default=ROOT/'src/Spyro.Editor.NativeMobySaveMapSmoke/bin/Release/net10.0/Spyro.Editor.NativeMobySaveMapSmoke.dll');p.add_argument('--dotnet',default='dotnet');p.add_argument('--report',type=Path);p.add_argument('--source-image',type=Path);p.add_argument('--packaged-core',type=Path);p.add_argument('--candidate-image',type=Path);a=p.parse_args()
f=json.loads(subprocess.run([a.dotnet,str(a.dll),'--emit'],capture_output=True,text=True,check=True,timeout=30).stdout)
PREINIT=f['preInitAddress'];QUERY=f['queryAddress'];DELETE=f['deleteAddress'];MARK=f['markAddress'];RELOAD=f['reloadAddress'];GATE=f['mapAddress'];BASE=0x80100000;DYNAMIC=0x80120000;STOP=0xDEADBEE0;UPDATE=f['update'];OVBASE=f['overlayBase']
checks=0;cases=0
sourceVerified=False
packageReceipt=None
candidateReceipt=None
if a.source_image:
 h=hashlib.sha256()
 with a.source_image.open('rb') as stream:
  while chunk:=stream.read(8*1024*1024):h.update(chunk)
  require(h.hexdigest()=='fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37','exact original USA source SHA256')
  def logical(start,length):
   data=bytearray()
   while len(data)<length:
    at=start+len(data);size=min(length-len(data),2048-at%2048);stream.seek(at//2048*2352+24+at%2048);data.extend(stream.read(size))
   return bytes(data)
  for address,key in [(MARK,'originalMark'),(RELOAD,'originalReload'),(DELETE,'originalDelete'),(PREINIT,'originalPreInit'),(QUERY,'originalQuery'),(f['emptySetupAddress'],'originalEmptySetup')]:
   expected=bytes.fromhex(f[key]);require(logical(53875*2048+address-0x8000F800,len(expected))==expected,'actual original native owner '+hex(address))
 sourceVerified=True


def check(b,message):
 global checks
 require(b,message);checks+=1

def words(address,data):return {address+i:struct.unpack_from('<I',data,i)[0]for i in range(0,len(data),4)}
old=words(f['emptySetupAddress'],bytes.fromhex(f['originalEmptySetup']))|words(PREINIT,bytes.fromhex(f['originalPreInit']))|words(QUERY,bytes.fromhex(f['originalQuery']))|words(DELETE,bytes.fromhex(f['originalDelete']))|words(MARK,bytes.fromhex(f['originalMark']))|words(RELOAD,bytes.fromhex(f['originalReload']))
new=words(f['emptySetupAddress'],bytes.fromhex(f['originalEmptySetup']))|words(f['emptyBranchAddress'],bytes.fromhex(f['emptyBranch']))|words(PREINIT,bytes.fromhex(f['preInit']))|words(QUERY,bytes.fromhex(f['query']))|words(DELETE,bytes.fromhex(f['delete']))|words(MARK,bytes.fromhex(f['mark']))|words(RELOAD,bytes.fromhex(f['reload']))|words(OVBASE,bytes.fromhex(f['overlay']))
copy=words(0x80016958,bytes.fromhex(f['wordCopy']));old|=copy;new|=copy

def fixture(code,mapped=False,count=256,overlay=None,update=None):
 m=Scalar(code);m.map(0x80070000,0x10000);m.map(BASE,0x6000);m.map(DYNAMIC,88);m.map(0x801EF000,0x2000)
 m.r=[(0x313579BD+i*0x10203)&0xffffffff for i in range(32)];m.r[0]=0;m.r[29]=0x801EFFF0;m.r[31]=STOP
 m.poke(0x80075828,BASE);m.poke(0x80075890,BASE+count*88);m.poke(0x80075734,(UPDATE if update is None else update) if mapped else OVBASE)
 for i,b in enumerate(bytes.fromhex(f['overlay'] if overlay is None else overlay)):m.poke(OVBASE+i,b,1)
 if not mapped:m.poke(OVBASE+8,0x24420001) # exact unknown metadata: identity gateway
 m.poke(0x800761D4,0x80071000);m.poke(0x80071000,0x5A,1);m.poke(0x80075860,91)
 return m

def run(m,entry,stop=STOP):
 effects=[];pc=entry
 while True:
  target=m.run(pc,{stop,0x800557CC,0x80055A78,0x80052568,0x800D0000,0x800526A8,0x800529CC,0x80017700,0x8004D5EC,0x800533D0})
  if target==stop:break
  effects.append((target,tuple(m.r[4:8])))
  if target==0x80052568:m.write(m.r[4]+0x48,0x80,1)
  if target==0x80017700:
   for off in [0,4,8]:m.write(m.r[4]+off,m.read(m.r[5]+off),4)
  pc=m.r[31]
 return effects

# Gateway ABI is pure and preserves every register except its declared scratch/result.
for mapped in [False,True]:
 for row in [0,1,2,3,4,5,6,31,32,255,256,0xffffffff]:
  m=fixture(new,mapped);before=m.r.copy();m.r[6]=row;before[6]=row;run(m,GATE)
  expected=(f['map'][row]&0xffffffff)if mapped and row<len(f['map'])else 0xffffffff if mapped else row
  check(m.r[6]==expected,'gateway logical ordinal/sentinel');check(not m.writes,'gateway never writes RAM')
  check(all(m.r[i]==before[i]for i in range(32)if i not in [1,2,3,6]),'gateway declared register ABI');cases+=1

# Identity hooks preserve exact native current/durable bitmap and gem state.
for row in range(256):
 for level in [0,35]:
  for gem in [0,5]:
   snapshots=[]
   for code in [old,new]:
    m=fixture(code);actor=BASE+row*88;m.poke(actor+20,100);m.poke(0x80075964,level);m.poke(0x80077420+level*4,7)
    m.r[4]=gem;m.r[5]=actor;preserved=m.r[16:24]+[m.r[28],m.r[29],m.r[30]];effects=run(m,MARK)
    check(m.r[16:24]+[m.r[28],m.r[29],m.r[30]]==preserved,'mark native preserved-register ABI')
    snapshots.append((m.bytes(0x800778B8,32),m.bytes(0x80077908,0x480),m.bytes(0x80075860,4),m.bytes(0x80077420,144),effects))
   check(snapshots[0]==snapshots[1],'identity mark matches original native outputs/effects');cases+=1

for tag in [0,31,32,255]:
 snapshots=[]
 for code in [old,new]:
  m=fixture(code);m.poke(DYNAMIC+0x56,tag,1);m.poke(0x80075964,35);m.r[4]=0;m.r[5]=DYNAMIC;effects=run(m,MARK)
  snapshots.append((m.bytes(0x800778B8,32),m.bytes(0x80077908,0x480),effects))
 check(snapshots[0]==snapshots[1],'native dynamic save tag remains unchanged');cases+=1

for physical,logical in enumerate(f['map']):
 m=fixture(new,True,count=len(f['map']));actor=BASE+physical*88;m.r[4]=0;m.r[5]=actor;m.poke(0x80075964,35);effects=run(m,MARK)
 current=bytearray(32);durable=bytearray(0x480)
 if logical>=0:current[logical//8]|=1<<(logical&7);durable[35*32+logical//8]|=1<<(logical&7)
 check(m.bytes(0x800778B8,32)==current and m.bytes(0x80077908,0x480)==durable,'mapped mark isolates exact transient and durable bit');cases+=1

# Reload exact kind/checkpoint policy and original word-copy, with helper doubles only unlink.
for row in [0,31,32,255]:
 for level in [0,35]:
  for found in [False,True]:
   for checkpoint in [False,True]:
    for kind in [0,1,31,32,0x7D,0x7E,0x7F]:
     snapshots=[]
     for code in [old,new]:
      m=fixture(code);m.r[4]=BASE;m.r[16]=row*88;m.r[17]=row;m.r[18]=0x80000003;m.r[19]=255;m.r[20]=1
      m.poke(0x80075964,level);m.poke(BASE+row*88+0x3A,kind,1);m.poke(BASE+(row+1)*88+0x48,255,1)
      if found:m.poke(0x80077908+level*32+(row>>5)*4,1<<(row&31))
      if checkpoint:m.poke(0x80077898+(row>>5)*4,1<<(row&31))
      effects=run(m,RELOAD,0x80014404);snapshots.append((m.bytes(BASE+row*88,88),m.bytes(0x800778B8,32),effects,m.r[16:24]))
     check(snapshots[0]==snapshots[1],'identity reload matches native kind/checkpoint/copy policy');cases+=1

for physical,logical in enumerate(f['map']):
 for found in [False,True]:
  m=fixture(new,True,count=len(f['map']));m.r[4]=BASE;m.r[16]=physical*88;m.r[17]=physical;m.r[18]=0;m.r[19]=255;m.r[20]=1
  m.poke(BASE+physical*88+0x3A,0x7D,1);m.poke(BASE+(physical+1)*88+0x48,255,1);m.poke(0x80075964,35)
  if found and logical>=0:m.poke(0x80077908+35*32+(logical>>5)*4,1<<(logical&31))
  effects=run(m,RELOAD,0x80014404)
  check(bool(effects)==(logical<0 or found),'mapped reload frees only collected logical row or inactive hole')
  check(not effects or (len(effects)==1 and effects[0][0]==0x80052568 and effects[0][1][0]==BASE+physical*88),'unlink exact physical actor pointer');cases+=1

# Physical-parent child tags and checkpoint deletion writers share the logical identity.
for physical,logical in enumerate(f['map']):
 m=fixture(new,True);m.poke(DYNAMIC+0x56,physical,1);m.poke(0x80075964,35);m.r[4]=0;m.r[5]=DYNAMIC;run(m,MARK)
 expected=bytearray(0x480)
 if logical>=0:expected[35*32+logical//8]|=1<<(logical&7)
 check(m.bytes(0x80077908,0x480)==expected,'spawned physical parent tag maps to its durable logical bit')
 check(m.bytes(0x800778B8,32)==bytes(32),'dynamic reward does not acquire native placed transient side effect');cases+=1
for row in range(256):
 snapshots=[]
 for code in [old,new]:
  m=fixture(code);m.r[4]=BASE+row*88;before=m.r.copy();run(m,DELETE)
  check(all(m.r[i]==before[i] for i in range(32) if i not in [1,2,3,4,5]),'deletion hook preserves native register ABI')
  snapshots.append(m.bytes(0x800778B8,32))
 check(snapshots[0]==snapshots[1],'identity transient deletion writer equals original native bit');cases+=1
for physical,logical in enumerate(f['map']):
 m=fixture(new,True);m.r[4]=BASE+physical*88;run(m,DELETE);expected=bytearray(32)
 if logical>=0:expected[logical//8]|=1<<(logical&7)
 check(m.bytes(0x800778B8,32)==expected,'deletion/checkpoint writer maps exact logical bit');cases+=1
for pointer in [BASE-88,BASE+256*88,DYNAMIC]:
 m=fixture(new,True);m.r[4]=pointer;run(m,DELETE);check(m.bytes(0x800778B8,32)==bytes(32),'dynamic/outside actor never sets placed deletion bit');cases+=1

# The earlier constructor pass also consumes checkpoint bits, before reload suppression.
def initial_fixture(code,row,level,kind,found,checkpoint,mapped=False):
 m=fixture(code,mapped);actor=BASE+row*88;m.r[4]=BASE;m.r[16]=row*88;m.r[17]=row;m.r[18]=0x80000000;m.r[19]=1
 m.poke(0x80075964,level);m.poke(actor+0x36,1,2);m.poke(0x8007637C,0x1000);m.poke(actor+0x3A,kind,1);m.poke(BASE+(row+1)*88+0x48,255,1)
 logical=f['map'][row] if mapped else row
 if logical>=0:
  if found:m.poke(0x80077908+level*32+(logical>>5)*4,1<<(logical&31))
  if checkpoint:m.poke(0x80077898+(logical>>5)*4,1<<(logical&31))
 return m,actor
for row in [0,31,32,255]:
 for level in [0,35]:
  for kind in [0,1,31,32,0x7D,0x7F,0x80,0x81,0x9F,0xA0,0xFF]:
   for found in [False,True]:
    for checkpoint in [False,True]:
     snapshots=[]
     for code in [old,new]:
      m,actor=initial_fixture(code,row,level,kind,found,checkpoint);effects=run(m,PREINIT,0x800142A0)
      check([t for t,_ in effects]==[0x800529CC,0x80017700,0x8004D5EC,0x800533D0],'native initial constructor/position/collision sequence preserved')
      snapshots.append((m.r[16:24],[(t,args[:2] if t in [0x80017700,0x8004D5EC] else args[:1])for t,args in effects],m.bytes(0x80077898,32),m.bytes(0x80077908,0x480)))
     check(snapshots[0]==snapshots[1],'identity initial-pass checkpoint summary matches native');cases+=1
for physical,logical in enumerate(f['map']):
 for kind in [0,1,31,32,0x7D,0x80,0x9F]:
  for found in [False,True]:
   for checkpoint in [False,True]:
    m,actor=initial_fixture(new,physical,35,kind,found,checkpoint,True);effects=run(m,PREINIT,0x800142A0)
    include=logical>=0 and (kind&0x7F)<32 and not(found and checkpoint)
    check(m.r[18]==(0x80000000 | (1<<(kind&31) if include else 0)),'mapped first-pass checkpoint summary uses logical bits')
    check(bool(effects)==(logical>=0),'inactive hole skips ALL constructor, position and collision helpers');cases+=1
for row in [0,31,32,255]:
 for checkpoint in [False,True]:
  for reward in [0,0x56,0xFF]:
   outputs=[]
   for code in [old,new]:
    m=fixture(code);actor=BASE+row*88;m.r[4]=actor;m.poke(actor+0x53,reward,1)
    if checkpoint:m.poke(0x80077898+(row>>5)*4,1<<(row&31))
    run(m,QUERY);outputs.append((m.r[2],m.r[5],m.r[6],m.r[29],m.r[31]))
   check(outputs[0]==outputs[1],'identity checkpoint query matches native Boolean and preserved frame');cases+=1
for physical,logical in enumerate(f['map']):
 for checkpoint in [False,True]:
  m=fixture(new,True);actor=BASE+physical*88;m.r[4]=actor;m.poke(actor+0x53,255,1)
  if checkpoint and logical>=0:m.poke(0x80077898+(logical>>5)*4,1<<(logical&31))
  run(m,QUERY);check(m.r[2]==int(checkpoint and logical>=0),'mapped checkpoint query consumes logical bit, holes false');cases+=1

# The original external empty-table branch enters the exact relocated copy block.
for checkpointFill in [0,0xFF,0xA5,0x5A]:
 snapshots=[]
 for code in [old,new]:
  m=fixture(code,True,count=0);m.poke(BASE+0x48,255,1)
  for i in range(32):m.poke(0x80077898+i,checkpointFill,1)
  effects=run(m,f['emptySetupAddress'],0x80014404)
  check(not effects,'empty table never invokes actor helpers')
  snapshots.append((m.bytes(0x800778B8,32),m.r[17]))
 check(snapshots[0]==snapshots[1]==(bytes([checkpointFill])*32,9),'empty native reload preserves exact checkpoint copy and next phase index');cases+=1

# Optional mapped egg update executes both the emitted gateway and native collected hook.
EGG=f['eggAddress'];PARTICLE=0x800D0000
mappedEgg=new|words(EGG,bytes.fromhex(f['eggCode']))|words(OVBASE,bytes.fromhex(f['eggOverlay']))
for cls in f['classes']:
 for physical,logical in [(0,195),(196,197),(1,-1)]:
  for found in [False,True]:
   for near in [False,True]:
    m=fixture(mappedEgg,True,197,f['eggOverlay'],f['eggUpdate']);actor=BASE+physical*88;level=35
    m.poke(0x800700F4,actor);m.poke(0x800758E4,PARTICLE);m.poke(0x80075964,level)
    m.poke(actor+0x36,cls,2);m.poke(actor+0x46,17,1);m.poke(actor+0x3E,0xCDAB,2)
    for off in [12,16,20]:m.poke(actor+off,0 if near else 0x10000)
    m.poke(0x80077908,0x1249)
    if found and logical>=0:m.poke(0x80077908+level*32+(logical>>5)*4,1<<(logical&31))
    before=m.bytes(0x80077908,0x480);effects=run(m,EGG);targets=[target for target,_ in effects]
    expect=[] if logical<0 else [0x80052568] if found else [PARTICLE,0x80055A78,0x80052568] if near else []
    check(targets==expect,'mapped egg exact suppression/pickup effects')
    expected=bytearray(before)
    if logical>=0 and not found and near:expected[level*32+logical//8]|=1<<(logical&7)
    check(m.bytes(0x80077908,0x480)==expected,'mapped egg/native mark changes only logical durable bit')
    check(m.bytes(actor+0x3E,2)==bytes([0xAB,0xCD]),'mapped egg never writes eye frame bytes')
    check(all(args[0]==actor for target,args in effects if target==0x80052568),'mapped egg unlink binds physical actor')
    check(any(target==MARK for _,target in m.calls)==(logical>=0 and not found and near),'only uncollected near mapped egg calls real MarkCollected')
    if logical>=0 and not found and near:
     m.poke(actor+0x48,0,1);m.r[31]=STOP;second=run(m,EGG)
     check([t for t,_ in second]==[0x80052568],'mapped egg reconstructed after collection retires once')
    cases+=1

if a.packaged_core:
 packageReceipt=json.loads(subprocess.run([a.dotnet,str(a.dll),'--emit-packaged-core',str(a.packaged_core.resolve())],capture_output=True,text=True,check=True,timeout=30).stdout)
 expected={MARK:bytes.fromhex(f['mark']),RELOAD:bytes.fromhex(f['reload']),DELETE:bytes.fromhex(f['delete']),PREINIT:bytes.fromhex(f['preInit']),QUERY:bytes.fromhex(f['query']),f['emptyBranchAddress']:bytes.fromhex(f['emptyBranch'])}
 actual={w['address']:bytes.fromhex(w['code'])for w in packageReceipt['windows']}
 check(actual==expected,'exact packaged Core emits all six canonical native owners')
if a.candidate_image:
 require(a.packaged_core is not None,'candidate readback requires exact packaged Core identity')
 with a.candidate_image.open('rb') as stream:
  def logical(start,length):
   data=bytearray()
   while len(data)<length:
    at=start+len(data);size=min(length-len(data),2048-at%2048);stream.seek(at//2048*2352+24+at%2048);chunk=stream.read(size);require(len(chunk)==size,'candidate raw sector bounds');data.extend(chunk)
   return bytes(data)
  pvd=logical(16*2048,2048);require(pvd[1:6]==b'CD001','candidate ISO PVD')
  rootLba=struct.unpack_from('<I',pvd,158)[0];rootSize=struct.unpack_from('<I',pvd,166)[0];directory=logical(rootLba*2048,rootSize);i=0;matches=[]
  while i<len(directory):
   length=directory[i]
   if not length:i=(i//2048+1)*2048;continue
   record=directory[i:i+length];name=record[33:33+record[32]]
   if name.upper().split(b';')[0]==b'SCUS_942.28':matches.append((struct.unpack_from('<I',record,2)[0],struct.unpack_from('<I',record,10)[0]))
   i+=length
  require(len(matches)==1,'exactly one candidate USA SCUS_942.28 owner')
  lba,size=matches[0];exe=logical(lba*2048,size);require(exe[:8]==b'PS-X EXE','candidate EXE header')
  for address,code in actual.items():check(exe[address-0x8000F800:address-0x8000F800+len(code)]==code,'candidate six-owner readback '+hex(address))
  h=hashlib.sha256();stream.seek(0)
  while chunk:=stream.read(8*1024*1024):h.update(chunk)
 candidateReceipt=dict(imageSha256=h.hexdigest(),exeSha256=hashlib.sha256(exe).hexdigest(),exeLba=lba,ownerCount=len(actual),readOnly=True)

report=dict(marker='NATIVE_MOBY_SAVE_MAP_SCALAR_OK',checks=checks,cases=cases,markBytes=len(bytes.fromhex(f['mark'])),reloadBytes=len(bytes.fromhex(f['reload'])),moduleSha256=hashlib.sha256(a.dll.read_bytes()).hexdigest(),helperEffectsMocked=['native award','audio','unlink','constructor','position copy','collision query','row preparation'],nativeWordCopyExecuted=True,nativeRuntime=False,cardIO=False,sourceVerified=sourceVerified,packagedCore=packageReceipt,candidate=candidateReceipt)
if a.report:a.report.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
