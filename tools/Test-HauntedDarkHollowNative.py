"""Declared-memory integer MIPS/load-delay controls against CURRENT ordinary export.
No renderer, collision response, live game RAM or emulation. Never overwrites proof.
Usage: python3 Test-HauntedDarkHollowNative.py SMOKE_OUTPUT ORIGINAL_EXE NEW_PROOF.json
"""
from pathlib import Path
import struct,json,hashlib,sys
if len(sys.argv)!=4:raise SystemExit(__doc__)
P=Path(sys.argv[1]);OUT=Path(sys.argv[3])
if OUT.exists():raise ValueError('Preserve existing proof')
E=Path(sys.argv[2]).read_bytes();EB=0x8000f800;OB=0x8007aa38;checks=0
H=lambda b:hashlib.sha256(b).hexdigest()
def ck(x,m):
 global checks;checks+=1
 if not x:raise ValueError(m)
ck(H(E)=='a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998','original executable')
F=(P/'final-overlay.bin').read_bytes();O=(P/'original-haunted-overlay.bin').read_bytes();D=(P/'original-darkhollow-overlay.bin').read_bytes();L=json.loads((P/'native-replay-map.json').read_text());R=json.loads((P/'receipt.json').read_text())
ck(H(F)==L['FinalOverlaySHA']and H((P/'final-level-data.bin').read_bytes())==L['FinalLevelDataSHA'],'current composition map binds exact final data/code')
ck(H(O)=='e714a2c4aff4a7e8d4980fa091540f4fdcd320b4fe4c532fa8c6c3d36fd7fbfc','whole source overlay')
ck(H(D)=='9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32','whole native destination overlay')
ck(H((P/'ordinary-haunted21-armor1-darkhollow.bin').read_bytes())==R['BinSHA'],'exact actual saved export')
ck(F[:0x80085594-OB]==D[:0x80085594-OB],'all retained original native code')
def word(b,p):
 if OB<=p<OB+len(b):return struct.unpack_from('<I',b,p-OB)[0]
 ck(EB<=p<EB+len(E),'actual instruction owner');return struct.unpack_from('<I',E,p-EB)[0]
def W(p):return word(O,p)
def signed(v,b=32):return v-(1<<b) if v&(1<<(b-1)) else v
class VM:
 def __init__(self):self.word=W;self.limit=300;self.r=[0]*32;self.mem={};self.pending=None;self.hi=0;self.lo=0;self.writes=[];self.loads=[];self.steps=0
 def seed(self,a,v,n=4):
  for k in range(n):self.mem[a+k]=(v>>(8*k))&255
 def read(self,a,n,sg=False):
  ck(all(a+k in self.mem for k in range(n)),f'Unowned native load {a:08X}/{n} at {getattr(self,"pc",0):08X}');v=sum(self.mem[a+k]<<(8*k) for k in range(n));return signed(v,n*8) if sg else v
 def step(self,p):
  self.pc=p;self.steps+=1;ck(self.steps<self.limit,'Finite original producer');w=self.word(p);o=w>>26;s=w>>21&31;t=w>>16&31;d=w>>11&31;i=signed(w&65535,16);fn=w&63;a=self.r[s];b=self.r[t];out=None;load=None;target=None
  if o in(8,9):out=(t,(a+i)&0xffffffff)
  elif o in(10,11):out=(t,int(signed(a)<i if o==10 else a<(i&0xffffffff)))
  elif o==15:out=(t,(w&65535)<<16)
  elif o in(12,13,14):out=(t,a&(w&65535) if o==12 else a|(w&65535) if o==13 else a^(w&65535))
  elif o in(32,33,35,36,37):
   z=4 if o==35 else 2 if o in(33,37) else 1;ad=(a+i)&0xffffffff;load=(t,self.read(ad,z,o in(32,33))&0xffffffff);self.loads.append((p,ad,z))
  elif o in(40,41,43):
   z=1 if o==40 else 2 if o==41 else 4;ad=(a+i)&0xffffffff;self.seed(ad,b,z);self.writes.append((p,ad,z,b&((1<<(8*z))-1)))
  elif o in(2,3):
   target=((p+4)&0xf0000000)|((w&0x3ffffff)<<2)
   if o==3:out=(31,p+8)
  elif o in(1,4,5,6,7):
   q=signed(a)<0 if o==1 and t==0 else signed(a)>=0 if o==1 and t==1 else a==b if o==4 else a!=b if o==5 else signed(a)<=0 if o==6 else signed(a)>0
   target=p+4+i*4 if q else p+8
  elif o==0:
   sh=w>>6&31
   if fn in(0,2,3,4,6,7):out=(d,(b<<(sh if fn==0 else a&31))&0xffffffff if fn in(0,4) else b>>(sh if fn==2 else a&31) if fn in(2,6) else (signed(b)>>(sh if fn==3 else a&31))&0xffffffff)
   elif fn in(32,33,34,35,36,37,38,39,42,43):out=(d,(a+b if fn in(32,33) else a-b if fn in(34,35) else a&b if fn==36 else a|b if fn==37 else a^b if fn==38 else ~(a|b) if fn==39 else int(signed(a)<signed(b)) if fn==42 else int(a<b))&0xffffffff)
   elif fn in(8,9):
    target=a
    if fn==9:out=(d,p+8)
   elif fn==16:out=(d,self.hi)
   elif fn==18:out=(d,self.lo)
   elif fn==17:self.hi=a
   elif fn==19:self.lo=a
   elif fn in(24,25):
    z=(signed(a)*signed(b) if fn==24 else a*b)&0xffffffffffffffff;self.lo=z&0xffffffff;self.hi=z>>32
   elif fn in(26,27):
    av=signed(a) if fn==26 else a;bv=signed(b) if fn==26 else b;ck(bv!=0 and not(av==-0x80000000 and bv==-1),'Original bounded divide defined');q=abs(av)//abs(bv);q=-q if (av<0)!=(bv<0) else q;self.lo=q&0xffffffff;self.hi=(av-q*bv)&0xffffffff
   else:raise ValueError(f'Unsupported native producer {p:08X}/{w:08X}')
  else:raise ValueError(f'Unsupported native producer {p:08X}/{w:08X}')
  if self.pending and (not out or out[0]!=self.pending[0]):self.r[self.pending[0]]=self.pending[1]
  if out:self.r[out[0]]=out[1]
  self.pending=load;self.r[0]=0;return target

def run(m,entry,stops,intercepts=None):
 pc=entry
 while pc not in stops:
  if intercepts and pc in intercepts:
   intercepts[pc](m);pc=m.r[31];continue
  nxt=m.step(pc)
  if nxt is None:pc+=4;continue
  ck(m.step(pc+4) is None,'ordinary native delay slot');pc=nxt
 return pc
def blank():
 m=VM();m.limit=3000;m.r[29]=0x801ff000
 for a in range(0x801fec00,0x801ff010):m.seed(a,0,1)
 return m

SPAWN={int(k,16):v for k,v in L['SpawnOriginals'].items()};UPDATE={int(k,16):v for k,v in L['UpdateOriginals'].items()};BASE=0x80120000;PROP=0x80150000;PART=0x80160000;RET=0xbad0000;records=[]
def machine(b):
 m=blank();m.word=lambda p:word(b,p);m.r[31]=RET
 for a in range(0x8007ab74,0x8007adfc):m.seed(a,b[a-OB],1)
 return m
def bytes_at(m,a,n):return bytes(m.read(a+i,1)for i in range(n))
def put(m,a,b):
 for i,v in enumerate(b):m.seed(a+i,v,1)
def row_fixture(m,copied,physical):
 wizard,armor=(L['Rows']if copied else L['OriginalRows']);wi=physical;ai=armor.get('Physical',armor['SourceRowIndex']);props=bytes.fromhex(wizard['Properties']);put(m,PROP,props)
 for p in wizard['Pointers']:m.seed(PROP+p['Offset'],PROP+p['Target'])
 for i in set([wi,ai,1]):put(m,BASE+i*88,bytes(88))
 put(m,BASE+wi*88,bytes.fromhex(wizard['Row']));m.seed(BASE+wi*88,PROP);m.seed(0x80075828,BASE);put(m,PART,bytes(32));m.seed(PART+24,wi,1);m.r[17]=PART+1;m.r[29]=0x801ff000;m.seed(m.r[29]+0x78,0x80180000);return ai
for physical in (0,21,128,219,255,256):
 for copied in (False,True):
  m=machine(F if copied else O);m.r[18]=PART;m.r[20]=BASE+88*physical;m.seed(0x80075828,BASE);put(m,PART,bytes(32));run(m,SPAWN[0x80088C04]if copied else 0x80088C04,{SPAWN[0x80088C50]if copied else 0x80088C50});ck(m.read(PART+24,1)==physical%256,'actual native constructor reciprocal88 byte index');records.append({'Kind':'constructor-byte','Copied':copied,'Physical':physical,'StoredByte':m.read(PART+24,1),'Instructions':m.steps})
# 256 wrapping is diagnostic only; source-level production guards refuse 256.
for physical in (21,128,219,255):
 for copied in (False,True):
  m=machine(F if copied else O);ai=row_fixture(m,copied,physical);run(m,UPDATE[0x80087394]if copied else 0x80087394,{0x80017c24});ck(m.r[16]==BASE+ai*88,'actual native byte owner resolves translated armor physical row');ck(m.r[18]==PROP+412,'actual source-selected complete armor path pointer');records.append({'Kind':'native-owner-lookup','Copied':copied,'PhysicalWizard':physical,'PhysicalArmor':ai,'Instructions':m.steps})
  for phase in (0,1,2):
   m=machine(F if copied else O);ai=row_fixture(m,copied,physical);m.r[16]=BASE+ai*88;m.r[18]=PROP+412;m.seed(PART+26,phase,1);stop=0x8006272c if phase==2 else UPDATE[0x800874F4]if copied else 0x800874F4;run(m,UPDATE[0x80087490]if copied else 0x80087490,{stop});ck(m.read(PART+26,1)==phase+1,'native path node advances');ck(m.read(BASE+ai*88+73,1)==int(phase==2),'native armor resurrects only at path completion');records.append({'Kind':'native-armor-activation','Copied':copied,'PhysicalWizard':physical,'PhysicalArmor':ai,'Phase':phase,'Instructions':m.steps})
# Stale unrebased link is a failing control of the actual native consumer.
m=machine(F);row_fixture(m,True,128);m.seed(PROP+32,1);run(m,UPDATE[0x80087394],{0x80017c24});ck(m.r[16]==BASE+88 and m.r[16]!=BASE+129*88,'stale source1 link targets wrong retained object')
for b,copied in[(D,False),(F,True)]:
 for count in(1,2,7):
  m=machine(b);m.r[4]=count;m.r[5]=29;m.r[6]=PART;m.r[7]=BASE+128*88;put(m,PART,bytes(range(32)));before=bytes_at(m,PART,32);run(m,0x800844a0,{RET});ck(bytes_at(m,PART,32)==before,'full original destination constructor29 no allocation or particle write');ck(not any(a<0x801fec00 or a>=0x801ff010 for _,a,_,_ in m.writes),'constructor writes stack only');records.append({'Kind':'destination-no-op-spawn','RetainedFinal':copied,'Count':count,'Instructions':m.steps})
  for delta in(1,8,16):
   m=machine(b);m.seed(0x80075824,PART);m.r[4]=delta
   for k in range(count+1):put(m,PART+k*32,bytes(32));m.seed(PART+k*32,29,1);m.seed(PART+k*32+1,255 if k==count else 0,1)
   before=bytes_at(m,PART,(count+1)*32);run(m,0x80083274,{RET});ck(bytes_at(m,PART,(count+1)*32)==before,'full original destination update29 no state write');ck(not any(a<0x801fec00 or a>=0x801ff010 for _,a,_,_ in m.writes),'update writes stack only');records.append({'Kind':'destination-no-op-update','RetainedFinal':copied,'Count':count,'Delta':delta,'Instructions':m.steps})
# Native wrapper instructions execute; intercepted leaf entries only record ABI.
for delta in(1,8,16):
 m=machine(F);m.r[4]=delta;calls=[]
 def call(pc):
  def capture(m):calls.append((pc,m.r[4]));m.r[2]=123;m.r[4]=0xeeee
  return capture
 native=0x80083274;owned=UPDATE[0x80086754];run(m,L['ParticleChain'],{RET}, {owned:call(owned),native:call(native)});ck(calls==[(owned,delta),(native,delta)],'owned update then original destination exactly once, delta preserved');ck(m.r[29]==0x801ff000 and m.r[31]==RET,'wrapper preserves native stack/return address');records.append({'Kind':'chain-ABI','Delta':delta,'Calls':calls,'Instructions':m.steps})

# Exact effects1/70 use the retained equivalent native manager;7/65 use full
# source bodies. Execute complete functions and all real integer native helpers.
POOL=0x80120000;VEC=0x80140000

def full_machine(b,rng=1):
 m=blank();m.limit=50000;m.word=lambda p:word(b,p);m.r[31]=RET;put(m,OB,b);put(m,POOL,bytes(8192));m.seed(POOL+1,255,1);m.seed(0x80075824,POOL);m.seed(0x80075738,POOL);m.seed(0x80075ac0,rng);m.seed(0x800757d8,8);put(m,VEC,struct.pack('<6i',40000,56000,17356,128,-256,512));return m

def pool_state(m):return(bytes(m.mem[POOL+i]for i in range(8192)),m.read(0x80075738,4),m.read(0x80075ac0,4))

for typ in(1,7,65,70):
 for count in(1,2,7):
  for seed in(0,1,0x12345678,0xffffffff):
   for control in(8,9):
    states=[]
    for b,entry in[(O,0x800881d8),(F,0x800844a0 if typ in(1,70)else SPAWN[0x800881d8])]:
     m=full_machine(b,seed);m.seed(0x800757d8,control);m.r[4]=count;m.r[5]=typ;m.r[6]=VEC;m.r[7]=VEC+12 if typ==1 else 512;run(m,entry,{RET});states.append(pool_state(m))
    ck(states[0]==states[1],f'complete actual source/final effect ctor and helpers {typ}/{count}/{seed}/{control}');ck(states[1][0][0]==typ and states[1][0][1]==(2 if typ in(1,7)else 1),'native texture kind2 vs untextured kind1 allocation contract');ck(states[1][0][15]==0x2e if typ in(1,7)else states[1][0][19]==0x50,'native textured quad2E vs untextured Gouraud line50 packet contract');records.append({'Kind':'full-effect-constructor','Type':typ,'Count':count,'Seed':seed,'Control':control})
 for delta in(0,1,2,8,16):
  for age in(0,1,7,23,24,31,255):
   for flags in(0,1):
    for timer in(0,1,255):
     states=[]
     for b,entry in[(O,0x80086754),(F,L['ParticleChain'])]:
      m=full_machine(b,0x12345678);put(m,POOL,bytes((i*7+19)%256 for i in range(32)));m.seed(POOL,typ,1);m.seed(POOL+1,2 if typ in(1,7)else 1,1);m.seed(POOL+2,flags,1);m.seed(POOL+3,1,1);m.seed(POOL+12,age,1);m.seed(POOL+30,timer,2);m.seed(POOL+33,255,1);m.seed(0x80075738,POOL+32);m.r[4]=delta;run(m,entry,{RET});states.append(pool_state(m));ck(m.r[29]==0x801ff000,'actual complete native chain restores SP')
     ck(states[0]==states[1],f'complete source/final lifetime, movement, RNG and free helper {typ}/{delta}/{age}/{flags}/{timer}');records.append({'Kind':'full-effect-update-chain','Type':typ,'Delta':delta,'Age':age,'Flags':flags,'Timer':timer})

for b,retained in[(D,False),(F,True)]:
 for typ in(7,65):
  for count in(1,2,7):
   m=full_machine(b);m.r[4]=count;m.r[5]=typ;m.r[6]=VEC;m.r[7]=0;before=pool_state(m);run(m,0x800844a0,{RET});ck(pool_state(m)==before,'complete original Dark7/65 ctor is authenticated no-op');records.append({'Kind':'destination-7-65-no-op-constructor','Type':typ,'Count':count,'Retained':retained})
   for delta in(1,8,16):
    m=full_machine(b);m.r[4]=delta
    for k in range(count):m.seed(POOL+k*32,typ,1);m.seed(POOL+k*32+1,1,1)
    m.seed(POOL+count*32+1,255,1);before=pool_state(m);run(m,0x80083274,{RET});ck(pool_state(m)==before,'complete original Dark7/65 update is authenticated no-op');records.append({'Kind':'destination-7-65-no-op-update','Type':typ,'Count':count,'Delta':delta,'Retained':retained})
# Mutating a real body word must alter the declared native result.
mut=bytearray(F);off=(SPAWN[0x80088F1C]-OB);struct.pack_into('<I',mut,off,struct.unpack_from('<I',mut,off)[0]^1)
a=full_machine(F);b=full_machine(mut)
for m in(a,b):m.r[4]=1;m.r[5]=65;m.r[6]=VEC;m.r[7]=0;run(m,SPAWN[0x800881d8],{RET})
ck(pool_state(a)!=pool_state(b),'emitted native65 body mutation is observable')
result={'Status':'CURRENT_SAVED_EXPORT_DECLARED_MEMORY_NATIVE_INSTRUCTION_CONTROLS_GAMEPLAY_SEPARATE','Checks':checks,'Controls':len(records),'BinSHA':R['BinSHA'],'FinalOverlaySHA':H(F),'Records':records,'Limitations':['Renderer/player combat/reward/audio/card/camera gameplay not executed','Byte256 wrapping is a rejected-capacity diagnostic, never production admission','Wrapper leaf entries record ABI; real owned byte lookup/activation and complete destination29 no-op functions execute separately']};OUT.write_text(json.dumps(result,indent=2)+'\n');print('HAUNTED_DARKHOLLOW_NATIVE_OK',checks,len(records),R['BinSHA'])
