"""Bounded original/current ordinary MIPS controls, no emulator/live RAM/GTE output.
Usage: python3 Test-HauntedSuperflameNative.py SMOKE_SUPERFLAME_OUTPUT ORIGINAL_EXE NEW_PROOF.json
"""
from pathlib import Path
import json,struct,sys,hashlib
T=Path(__file__).with_name('Test-HauntedDarkHollowNative.py')
exec(T.read_text().split('SPAWN=')[0],globals())
A=(P/'final-executable.bin').read_bytes();ck(A[0x3939c:0x3b938]==E[0x3939c:0x3b938],'complete actual native power/combat/render-caller owner')
SPAWN={int(k,16):v for k,v in L['SpawnOriginals'].items()};UPDATE={int(k,16):v for k,v in L['UpdateOriginals'].items()};POOL=0x80120000;VEC=0x80140000;ROW=0x80150000;PROP=0x80160000;RET=0x0bad0000;records=[]
def put(m,a,b):
 for i,v in enumerate(b):m.seed(a+i,v,1)
def machine(b,rng=1):
 m=blank();m.limit=50000;m.word=lambda p:word(b,p)if OB<=p<OB+len(b)else struct.unpack_from('<I',A if b is F else E,p-EB)[0]
 m.r[31]=RET;put(m,0x8006cbf8,E[0x8006cbf8-EB:0x8006cbf8-EB+640]);put(m,OB,b);put(m,POOL,bytes(8192));m.seed(POOL+1,255,1);m.seed(0x80075824,POOL);m.seed(0x80075738,POOL);m.seed(0x80075ac0,rng);m.seed(0x800757d8,8);m.seed(0x80078bbc,0);m.seed(0x80078764,0);put(m,VEC,struct.pack('<6i',34000,42000,18766,128,-256,512));return m
def state(m):return bytes(m.mem[POOL+i]for i in range(8192)),m.read(0x80075738,4),m.read(0x80075ac0,4)
for typ in(1,31,66,79):
 for count in(1,2,7):
  for seed in(0,1,0x12345678,0xffffffff):
   for power in(0,1):
    states=[]
    for b,entry in[(O,0x800881d8),(F,SPAWN[0x800881d8]if typ==31 else 0x800844a0)]:
     m=machine(b,seed);m.seed(0x80078764,power);m.r[4]=count;m.r[5]=typ;m.r[6]=VEC;m.r[7]=VEC+12 if typ in(1,79)else 2;run(m,entry,{RET});states.append(state(m));ck(m.r[29]==0x801ff000,'actual complete native constructor restores SP')
    ck(states[0]==states[1],f'complete source/final native ctor, allocator, vectors, RNG {typ}/{count}/{seed}/{power}');records.append({'Kind':'complete-native-constructor','Type':typ,'Count':count,'Seed':seed,'Powered':power})
 for delta in(0,1,2,8,16):
  for age in(0,1,7,23,24,31,63,64,255):
   for flags in(0,1,2,3):
    states=[]
    for b,entry in[(O,0x80086754),(F,L['ParticleChain'])]:
     m=machine(b,0x12345678);put(m,POOL,bytes((i*7+19)%256 for i in range(32)));m.seed(POOL,typ,1);m.seed(POOL+1,3 if typ==31 else 2 if typ==1 else 1,1);m.seed(POOL+2,flags,1);m.seed(POOL+3,1,1);m.seed(POOL+12,age,1);m.seed(POOL+30,0,2);m.seed(POOL+33,255,1);m.seed(0x80075738,POOL+32);m.r[4]=delta;run(m,entry,{RET});states.append(state(m));ck(m.r[29]==0x801ff000,'actual complete source+native update chain restores SP')
    ck(states[0]==states[1],f'complete native state, velocity, RNG and expiry {typ}/{delta}/{age}/{flags}');records.append({'Kind':'complete-native-lifetime','Type':typ,'Delta':delta,'Age':age,'Flags':flags})
# Dark31 is an actual no-op; source31 remains live in the installed owner.
for delta in(1,8,16):
 m=machine(F);m.seed(POOL,31,1);m.seed(POOL+1,3,1);m.seed(POOL+33,255,1);m.seed(0x80075738,POOL+32);m.r[4]=delta;before=state(m);run(m,0x80083274,{RET});ck(state(m)==before,'complete original Dark31 update cannot double-step the copied effect')
# Complete raw fairy grant packet, with actual installed branch/J relocations.
start=0x80080590;anchor=0x800805b8;signature=O[anchor-OB:anchor-OB+16];lo=L['CodeBase']-OB;positions=[i for i in range(lo,len(F)-16,4)if F[i:i+16]==signature];ck(len(positions)==1,'unique current emitted source fairy grant packet');grant=OB+positions[0];copied=grant-0x48;M=lambda pc:grant+pc-anchor
ck(word(F,copied)==word(O,start) and word(F,copied+8)==(0x24630000|((-L['ClassMap']['142'])&65535)) and word(F,copied+20)==0x2403008e and word(F,M(0x80080594))==0x240200f1,'actual source-class alias shim preserves native F1 distinction')
for frame in(0,6,7,8,14,255):
 for before in(0,1,899,900):
  states=[]
  for b,pc,cls in[(O,start,0x8e),(F,copied,L['ClassMap']['142'])]:
   m=machine(b);put(m,ROW,bytes(88));put(m,PROP,bytes(132));m.r[19]=ROW;m.r[17]=PROP;m.seed(ROW+54,cls,2);m.seed(ROW+63,frame,1);m.seed(PROP+32,0);m.seed(0x80078768,before);run(m,pc,{M(0x80080604)if b is F else 0x80080604,M(0x80080994)if b is F else 0x80080994});states.append(m.read(0x80078768,4))
  expected=900 if frame>=7 and before==0 else before;ck(states==[expected,expected],'actual source/current alias frame gate + exact900 native temporary grant, existing power untouched');records.append({'Kind':'native-fairy-power-grant','Frame':frame,'Before':before,'After':expected})
# Mutate the real emitted duration operand; the native result must change.
mut=bytearray(F);at=M(0x800805f8)-OB;struct.pack_into('<I',mut,at,struct.unpack_from('<I',mut,at)[0]^1);m=machine(mut);put(m,ROW,bytes(88));put(m,PROP,bytes(132));m.r[19]=ROW;m.r[17]=PROP;m.seed(ROW+54,L['ClassMap']['142'],2);m.seed(ROW+63,7,1);m.seed(0x80078768,0);run(m,copied,{M(0x80080604)});ck(m.read(0x80078768,4)==901,'actual emitted duration mutation changes result')
# Native power latch and powered/normal factory selector retain original inputs.
for power in(0,1,900):
 m=machine(F);m.seed(0x80078768,power);m.r[16]=1;run(m,0x80049ad8,{0x80049b0c});ck(m.read(0x80078764,4)==int(power!=0),'actual retained native flame power latch');m.r[16]=VEC+12;m.r[29]=0x801ff000;m.seed(0x800758e4,0x800844a0);run(m,0x80049bb0,{0x800844a0});ck(m.r[4]==1 and m.r[5]==int(power!=0),'actual EXE powered flame remains native particle1, normal0')
# Complete native flame renderer and presentation reload bytes remain original.
for pc,n in ((0x80058db4,0x6c0),(0x80013b88,0xec),(0x80013c38,0x60)):
 ck(A[pc-EB:pc-EB+n]==E[pc-EB:pc-EB+n],'complete current native flame renderer/presentation owner')
def subfiles(b):
 out=[]
 for i in range(10):
  at,size=struct.unpack_from('<II',b,i*8)
  if at==0 and size==0:break
  ck(at>=2048 and size>0 and at+size<=len(b),'independent full level subfile extent');out.append((at,size))
 return out
level=(P/'final-level-data.bin').read_bytes();scene=subfiles(level)[3][0];header=level[scene:scene+0x88];tile=header[0x78:0x80];ck(tile!=bytes(8),'actual scene13 is available independently of particle records')
# Execute every real presentation-header copy instruction for all15 slots.
# The unrelated native vector-copy call between segments is not substituted.
m=machine(F);HEAD=0x80190000;put(m,HEAD,header);m.r[16]=HEAD+16;run(m,0x80013b88,{0x80013ba4});ck(m.read(0x800786d8,4)==struct.unpack_from('<I',header,16)[0]and m.read(0x800786dc,4)==struct.unpack_from('<I',header,20)[0],'actual native normal material reload');m.r[16]=HEAD+24;run(m,0x80013bac,{0x80013c48});ck(m.read(0x800786e0,4)==struct.unpack_from('<I',tile)[0]and m.read(0x800786e4,4)==struct.unpack_from('<I',tile,4)[0],'actual native Superflame scene13 reload')
for power in (0,1,900):
 for frameV in range(256):
  m.r[18]=power;m.r[31]=0x800786c8;m.r[1]=frameV;m.pending=None;run(m,0x80059264,{0x80059284});chosen=header[0x10:0x18]if power==0 else tile;expected=[(struct.unpack_from('<I',chosen,i)[0]+(frameV<<8))&0xffffffff for i in (0,4)];ck([m.r[3],m.r[4]]==expected,'actual full native flame material selector/load-delay/frame UV addition');ck(power==0 or (m.r[3]>>16)==struct.unpack_from('<H',chosen,2)[0],'full powered page selection never carries into another CLUT');records.append({'Kind':'native-scene-render-selector','Powered':power,'FrameV':frameV})
# Missing scene13 reproduces zero resource selection; a particle13 record cannot repair it.
broken=machine(F);broken.r[31]=0x800786c8;broken.r[18]=1;broken.r[1]=0;put(broken,0x800786e0,bytes(8));run(broken,0x80059264,{0x80059284});ck(broken.r[3]==0 and broken.r[4]==0,'actual missing scene13 fails native render resource selection')
OUT.write_text(json.dumps({'Status':'CURRENT_ORDINARY_SOURCE_AND_EMITTED_NATIVE_INTEGER_INSTRUCTION_CONTROLS_NOT_GAMEPLAY','Checks':checks,'Controls':len(records),'BinSHA':R['BinSHA'],'FinalOverlaySHA':H(F),'Records':records,'Limitations':['Complete native effect constructors/lifetime functions execute with declared isolated memory, original vector/RNG/allocator helpers','Fairy grant packet/frame gate executes; full native approach/camera/kiss motion and flight are parent gameplay gates','Complete native flame renderer bytes and all-frame native scene reload/selector instructions verified; GTE/GPU image and audio output not executed','No live power globals or game RAM were written']},indent=2)+'\n');print('HAUNTED_SUPERFLAME_NATIVE_OK',checks,len(records),R['BinSHA'])
