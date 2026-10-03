"""Execute current production actor emission and resources in private CPU arrays.
No emulator, live state, native-callee stubs, or gameplay claim.
"""
from pathlib import Path
import json,struct,sys,hashlib
from SpyroNativeIntegerGteReplay import Replay
if len(sys.argv)!=3:raise SystemExit("Usage: Test-GreenDruidLinkedActors.py smoke-output NEW-report.json")
R=Path(sys.argv[1]);out=Path(sys.argv[2])
if out.exists():raise FileExistsError("Preserve existing receipt: "+str(out))
U=lambda b,a:struct.unpack_from('<I',b,a)[0]
D=(R/'source-data.bin').read_bytes();E=(R/'native-exe.bin').read_bytes();O=(R/'source-overlay.bin').read_bytes()
for body,sha in [(D,'507c7b327bdfe9e4c230bef7d93da5d8c935a9a9f033ad99de22a5af8dfd0ddf'),(E,'a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998'),(O,'f7c9a174f64959fb53b4058f9786df223a9c142b2289563ca31edc6a24c6d383')]:
 assert hashlib.sha256(body).hexdigest()==sha,'Complete original source owner'
STOP=0x80070000;TABLE=0x801a0000;HEAP=0x801b0000;PROPTOP=0x801d0000;LIST=0x801c0000
B=bytearray(0x200000);B[0xf800:0x75800]=E
class Trace(Replay):
 def __init__(self,b):super().__init__(b);self.reads=[];self.writes=[];self.total=0
 def rd(self,a,n,s=False):
  if (a&0x1fffffff)<0xf800:raise Exception('unowned low memory read '+hex(a)+' at '+hex(self.pc))
  self.reads.append((self.pc,a,n));return super().rd(a,n,s)
 def wr(self,a,n,v):self.writes.append((self.pc,a,n,v));return super().wr(a,n,v)
 def step(self):super().step();self.total+=1
def put(c,a,b):c.mem[a&0x1fffff:(a&0x1fffff)+len(b)]=b
def run(c,pc,args=(),stop=STOP,limit=1000000):
 c.pc=pc;c.r[31]=stop;c.branch=c.load=None;c.steps=0;c.visits={};c.r[29]=0x801f0000
 for i,v in enumerate(args):c.r[4+i]=v
 while c.pc!=stop:
  assert c.steps<limit,(hex(c.pc),c.steps);c.step()
 return c.steps
def parent(c,index,xyz=(146000,82000,23520),yaw=32):
 row=TABLE+index*88;c.wr(row,4,0x801a8000);c.wr(row+70,1,yaw)
 for i,x in enumerate(xyz):c.wr(row+12+4*i,4,x)
 for i,x in enumerate((146500,82500,23800)):c.wr(0x80078a58+4*i,4,x)
 c.wr(0x80078af8,4,24000);return row
receipt=json.loads((R/'receipt.json').read_text());code=(R/'actor-code.bin').read_bytes();assert hashlib.sha256(code).hexdigest()==receipt['CodeSha'];current=(R/'resources-data.bin').read_bytes();host=(R/'host-overlay.bin').read_bytes();address=receipt['Address'];labels=receipt['Labels'];origin={int(k):v for k,v in receipt['ActorOrigins'].items()};mapping={int(k):v for k,v in receipt['ClassMap'].items()};checks=0;cases=[]
def check(v,n):
 global checks
 assert v,n;checks+=1
def native_model(c,data,cls,base):
 regs=[]
 for s in range(1,64):
  r=U(data,0x50+4*s)
  if not r:break
  regs.append((struct.unpack_from('<H',data,0x150+2*s)[0],r))
 r=next(r for k,r in regs if k==cls);end=min([x for k,x in regs if x>r]+[U(data,16)+U(data,20)]);base+=(r-base)%8;put(c,base,data[r:end]);run(c,0x800133e0,[base],stop=0x80013640);c.wr(0x80076378+4*cls,4,c.r[2]);return c.r[2]
def blank(mapped):
 c=Trace(B);put(c,0x8007aa38,O)
 for cls,base in [(0x127,0x80180000),(0x13E,0x80181000),(0x146,0x80182000)]:native_model(c,D,cls,base)
 c.wr(0x80075828,4,TABLE);c.wr(0x80075890,4,HEAP);c.wr(0x8007573c,4,HEAP);c.wr(HEAP+72,1,255);c.wr(0x80075930,4,PROPTOP);c.wr(PROPTOP-1,1,255);c.wr(0x80075778,4,LIST);c.wr(0x800758cc,4,0x8008a36c);c.wr(0x800756a8,4,100);c.wr(0x800756cc,4,1);c.wr(0x800756c4,4,1);c.wr(0x800758e4,4,0x8008c9d8)
 if mapped:
  put(c,0x8007aa38,host);put(c,address,code)
  for cls,base in [(0x127,0x80180000),(0x13E,0x80181000),(0x146,0x80182000)]:
   c.wr(0x80076378+4*cls,4,0);native_model(c,current,mapping[cls],base)
  c.wr(0x800758cc,4,labels['wrap_init']);c.wr(0x800758e4,4,0x80088f68)
 return c

def constructor(c,mapped,cls,parent):run(c,labels['wrap_init']if mapped else 0x8008a36c,[mapping[cls]if mapped else cls,parent]);return c.r[2]
def update(c,mapped):run(c,origin[0x8007bb94]if mapped else 0x8007bb94,stop=origin[0x8008a320]if mapped else 0x8008a320,limit=100000)
def world(c):
 sf=U(D,8);put(c,0x80100000,D[sf:sf+U(D,12)]);run(c,0x80012d58,[0x80100000,0])
def pool(c):base=0x801e0000;c.wr(0x80075824,4,base);c.wr(0x80075738,4,base);c.wr(base+1,1,255);return base
for cls in [0x127,0x13E,0x146]:
 for idx in [3,44,254,255,256]:
  pair=[]
  for mapped in [False,True]:
   c=blank(mapped);p=parent(c,idx);row=constructor(c,mapped,cls,p);props=c.rd(row,4)
   check(c.rd(row+54,2)==(mapping[cls]if mapped else cls),'actual physical class constructor')
   check(c.rd(row+86,1)==(idx if idx<256 else 0),'physical parent byte width')
   raw=bytearray(c.mem[row&0x1fffff:(row&0x1fffff)+88]);raw[54:56]=bytes(2);raw[56:58]=bytes(2) # native sound index follows physical registration
   pair.append((raw,bytes(c.mem[props&0x1fffff:(props&0x1fffff)+(24 if cls==0x127 else 16 if cls==0x13E else 40)]),c))
  check(pair[0][0]==pair[1][0]and pair[0][1]==pair[1][1],'complete source/copied constructor row and properties')
  cases.append(dict(Kind='emitted-constructor',Class=cls,PhysicalClass=mapping[cls],Parent=idx,Instructions=sum(p[2].total for p in pair)))
for timer in [1,2,32,63]:
 for vz in [-200,-128,-16,0,110]:
  pair=[]
  for mapped in [False,True]:
   c=blank(mapped);p=parent(c,44);row=constructor(c,mapped,0x13E,p);props=c.rd(row,4);c.wr(props+12,4,timer);c.wr(props+8,4,vz);c.wr(row+81,1,1);c.r[19]=row;c.r[16]=props;update(c,mapped)
   pair.append(([c.rd(row+12+4*i,4)for i in range(3)]+[c.rd(row+68,1),c.rd(row+70,1),c.rd(row+72,1),c.rd(0x800756a4,4)],[c.rd(props+4*i,4)for i in range(4)],c))
  check(pair[0][:2]==pair[1][:2],'complete emitted13E lifetime/gravity/vector/spin/release')
  cases.append(dict(Kind='emitted13E-update',Timer=timer,Vz=vz,Instructions=sum(p[2].total for p in pair)))
for linked in [False,True]:
 for timer in [1,2,15,90]:
  for available in [20,21,22,23,24]:
   pair=[]
   for mapped in [False,True]:
    c=blank(mapped);world(c);particle=pool(c);p=parent(c,44);row=constructor(c,mapped,0x127,p);props=c.rd(row,4);c.wr(props+4,4,timer);c.wr(0x800756a8,4,available+1)
    for i,x in enumerate((140000,75000,25000)):c.wr(0x80078a58+4*i,4,x)
    if linked:
     target=TABLE+53*88
     for i,x in enumerate([c.rd(row+12,4),c.rd(row+16,4),c.rd(row+20,4)-700]):c.wr(target+12+4*i,4,x)
     c.wr(props,4,target)
    c.r[19]=row;c.r[30]=0x80078a58;update(c,mapped)
    spawned=[v for pc,a,n,v in c.writes if n==2 and HEAP<=a<HEAP+88*8 and v==(mapping[0x13E]if mapped else 0x13E)]
    pair.append(([c.rd(row+12+4*i,4)for i in range(3)]+[c.rd(row+72,1),c.rd(props+4,4),c.rd(0x800756a4,4),len(spawned)],bytes(c.mem[particle&0x1fffff:(particle&0x1fffff)+32]),c))
   check(pair[0][:2]==pair[1][:2],'emitted127 actual movement/hit/linked target/reserve/debris/type5')
   cases.append(dict(Kind='emitted127-update',Linked=linked,Timer=timer,Available=available,Instructions=sum(p[2].total for p in pair)))
# Parse complete original rows and their exact authenticated property allocations.
scene=D[U(D,24):U(D,24)+U(D,28)];rows_at=0x88
for _ in range(7):rows_at+=U(scene,rows_at)
actors=[]
for n,length,internal in [(11,164,[(0,92)]),(44,204,[(12,52)]),(53,4,[])]:
 raw=scene[rows_at+8+n*88:rows_at+8+(n+1)*88];prop=U(raw,0)
 actors.append(dict(Row=n,RowHex=raw.hex(),PropertiesHex=scene[prop:prop+length].hex(),InternalPointers=[dict(Item1=a,Item2=b)for a,b in internal]))
def cohort(c,mapped,indices):
 pp={11:0x801a8000,44:0x801a8200,53:0x801a8400}
 for a in actors:
  n=a['Row'];r=TABLE+indices[n]*88;raw=bytearray.fromhex(a['RowHex']);struct.pack_into('<I',raw,0,pp[n])
  if mapped:struct.pack_into('<H',raw,54,mapping[struct.unpack_from('<H',raw,54)[0]])
  put(c,r,raw);props=bytearray.fromhex(a['PropertiesHex'])
  for ptr in a['InternalPointers']:struct.pack_into('<I',props,ptr['Item1'],pp[n]+ptr['Item2'])
  for off,target in ({8:53,12:44}if n==11 else {8:11}if n==44 else {}).items():struct.pack_into('<I',props,off,indices[target])
  if n==11 and mapped:struct.pack_into('<I',props,4,receipt['Groups']['8'])
  put(c,pp[n],props)
 run(c,0x800526a8,[TABLE+indices[44]*88]);return {n:TABLE+v*88 for n,v in indices.items()},pp
for indices in [{11:11,44:44,53:53},{11:0,44:1,53:2},{11:253,44:254,53:255}]:
 for state in [0,1,2,3,4,5,6,7,8,9,10,96,97,99]:
  pair=[]
  for mapped in [False,True]:
   c=blank(mapped);world(c);pool(c);rows,pp=cohort(c,mapped,indices);row=rows[44];c.wr(row+72,1,state);c.wr(pp[44],4,8);c.r[19]=row;c.r[30]=0x80078a58
   for i,x in enumerate((150000,86000,26000)):c.wr(0x80078a58+4*i,4,x)
   update(c,mapped)
   pair.append(([c.rd(rows[n]+off,4)for n in [11,44,53]for off in [12,16,20]]+[c.rd(row+72,1),c.rd(pp[53],4),c.rd(pp[11]+16,4),c.rd(pp[11]+24,4),c.rd(pp[44]+16,4),c.rd(pp[44],4)],c))
  check(pair[0][0]==pair[1][0],'emitted146 exact selected state/reset currentDRUIDXYZ/cache/links')
  cases.append(dict(Kind='emitted146-state',PhysicalIndices=indices,State=state,Instructions=sum(x[1].total for x in pair)))
for mode,phase,state in [(1,1,0),(0,0,4)]:
 for count in [0,1,2,3]:
  pair=[]
  for mapped in [False,True]:
   c=blank(mapped);world(c);pool(c);rows,pp=cohort(c,mapped,{11:0,44:1,53:2});row=rows[44];c.wr(row+72,1,state);c.wr(pp[44]+16,4,mode);c.wr(pp[44]+32,4,phase);c.wr(pp[44],4,1);c.wr(pp[44]+4,4,count);c.wr(row+61,1,4);c.wr(row+63,1,6)
   player=(180000,100000,26000)if mode==1 else tuple(c.rd(row+12+i*4,4)+500 for i in range(3))
   for i,x in enumerate(player):c.wr(0x80078a58+i*4,4,x)
   c.r[19]=row;c.r[30]=0x80078a58;update(c,mapped);spawn=c.rd(0x800756a4,4);values=[spawn,c.rd(row+72,1),c.rd(pp[44],4),c.rd(pp[44]+4,4)]
   if spawn:
    childprops=c.rd(HEAP,4);check(c.rd(HEAP+54,2)==(mapping[0x127]if mapped else 0x127),'emitted146 real127 factory alias');values += [c.rd(childprops,4),c.rd(childprops+4,4),c.rd(HEAP+80,1),c.rd(HEAP+86,1)]+[c.rd(HEAP+12+4*i,4)for i in range(3)]
   pair.append((values,c))
  check(pair[0][0]==pair[1][0],'emitted146 complete linkedDRUID/player cast/timer/counter packet')
  cases.append(dict(Kind='emitted146-cast',Mode=mode,Count=count,Instructions=sum(x[1].total for x in pair)))
for seed in [0,1,0x12345678,0xffffffff]:
 for age in [0,47,48]:
  for drawn in [0,1]:
   for dt in [0,1,8]:
    pair=[]
    for mapped in [False,True]:
     c=blank(mapped);particle=pool(c);p=parent(c,44);c.wr(0x80075ac0,4,seed);run(c,labels['particle_alpineridge_spawn']if mapped else 0x8008c9d8,[1,5,p+12,0]);c.wr(particle+2,1,age);c.wr(particle+3,1,drawn);run(c,labels['wrap_owned_particles']if mapped else 0x8008af54,[dt]);pair.append((bytes(c.mem[particle&0x1fffff:(particle&0x1fffff)+8192]),[c.rd(g,4)for g in [0x80075738,0x80075ac0]],c))
    check(pair[0][:2]==pair[1][:2],'emitted5 full particle pool/update/native manager once/RNG/free')
    cases.append(dict(Kind='emitted-particle5-chain',Seed=seed,Age=age,Drawn=drawn,DeltaTime=dt,Instructions=sum(x[2].total for x in pair)))
for typ in [11,72]:
 for age in [0,1,47,48]:
  for drawn in [0,1]:
   for dt in [0,1,3,8]:
    pair=[]
    for mapped in [False,True]:
     c=blank(mapped);particle=pool(c);p=bytearray(32);p[0]=typ;p[1]=2 if typ==11 else 1;p[2]=age;p[3]=drawn
     if typ==11:
      p[4:10]=bytes.fromhex('903f90279017');p[10]=12;p[11]=40;p[12:15]=bytes([128]*3);p[15]=46;p[17]=4;p[18:24]=bytes.fromhex('020004000600')
     else:p[4:24]=bytes.fromhex('903f90279017913f912790171080f0f0c0804002')
     put(c,particle,p);c.wr(particle+33,1,255);c.wr(0x80075738,4,particle+32);c.wr(0x80075ac0,4,0x12345678);run(c,labels['wrap_owned_particles']if mapped else 0x8008af54,[dt]);pair.append((bytes(c.mem[particle&0x1fffff:(particle&0x1fffff)+8192]),[c.rd(g,4)for g in [0x80075738,0x80075ac0]],c))
    check(pair[0][:2]==pair[1][:2],'emitted5+8 skips native11/72: native retained lifetime runs once')
    cases.append(dict(Kind='retained-native-poof-chain',Type=typ,Age=age,Drawn=drawn,DeltaTime=dt,Instructions=sum(x[2].total for x in pair)))
for indices in [{11:11,44:44,53:53},{11:0,44:1,53:2},{11:253,44:254,53:255}]:
 pair=[]
 for mapped in [False,True]:
  c=blank(mapped);world(c);pool(c);rows,pp=cohort(c,mapped,indices);row=rows[44]
  for i,x in enumerate((180000,100000,26000)):c.wr(0x80078a58+i*4,4,x)
  sequence=[];calls=0
  for tick in range(36):
   c.r[19]=row;c.r[30]=0x80078a58;update(c,mapped);calls+=c.visits.get(0x80039e94,0);sequence.append(dict(XYZ=[c.rd(row+12+4*i,4)for i in range(3)],State=c.rd(row+72,1),PathHeader=bytes(c.mem[(pp[44]+52)&0x1fffff:((pp[44]+52)&0x1fffff)+8]).hex(),Phase=c.rd(pp[44]+32,4)))
  check(calls==36,'complete native counted146 path helper executes every tick');pair.append((sequence,c))
 check(pair[0][0]==pair[1][0],'emitted full146 path/reciprocal map exactly source-equivalent over36 ticks')
 cases.append(dict(Kind='emitted146-native-path',PhysicalIndices=indices,Sequence=pair[1][0],Instructions=sum(x[1].total for x in pair)))
report=dict(Marker='ALPINE_LINKED_ACTORS_ACTUAL_EMITTED_NATIVE_CONTROLS_OK',Checks=checks,Cases=cases,Instructions=sum(x['Instructions']for x in cases),CodeSha256=receipt['CodeSha'],Gameplay=False,Scope='Actual production assembler output and current imported complete model registrations/hydration. Original native EXE helpers execute unchanged. Private CPU inputs; GPU raster/SPU playback and full world/encounter acceptance remain root-owned.')
out.write_text(json.dumps(report,indent=2)+'\n');print(report['Marker'],checks,len(cases),report['Instructions'])
