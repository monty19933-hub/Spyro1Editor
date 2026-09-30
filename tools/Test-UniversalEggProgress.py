"""Independent MIPS execution of generated count and percentage words; no native runtime."""
from pathlib import Path
import json,struct,base64,random,hashlib,subprocess,sys,argparse
import SpyroEggScalarMips as o
o.sha=lambda b:hashlib.sha256(b).hexdigest()
root=Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dotnet',default='dotnet')
parser.add_argument('--dll',type=Path,default=root/'src/Spyro.Editor.UniversalIdentitySmoke/bin/Release/net10.0/Spyro.Editor.UniversalIdentitySmoke.dll')
args=parser.parse_args()
f={'cases':[],'dataAddress':0x8003A7F8}
for n in [1,2,6,7,32,116]:
 result=subprocess.run([args.dotnet,str(args.dll),'--emit-progress',str(n)],capture_output=True,text=True,check=True,timeout=30)
 emitted=json.loads(result.stdout)
 f['countAddress']=emitted['countAddress'];f['percentAddress']=emitted['percentAddress']
 f['cases'].append({'total':n,'counter':base64.b64encode(bytes.fromhex(emitted['count'])).decode(),'percentage':base64.b64encode(bytes.fromhex(emitted['percent'])).decode(),'entries':[{'level':(i*13)%36,'row':(i*17)%256} for i in range(n)]})
results=[];checks=[]
def check(ok,label):o.require(ok,label);checks.append(label)
# Engine-independent reference for the requested policy; no C# formula is invoked.
def expected(g,d,e,k,n):
 if k==n and d==80 and e==12 and g>=12000:return 100+(g-12000)//100
 return (min(g,12000)*50+d*6000+(e+k)*10000)//(12000+100*n)
# CPU arithmetic controls for unsigned HI/LO and branch/load delay support.
ctrl=o.Scalar({0x100:0x00850019,0x104:0x00001012,0x108:0x0085001B,0x10c:0x00001812});ctrl.r[4]=0xfffffffd;ctrl.r[5]=7;ctrl.run(0x100,{0x110});check(ctrl.r[2]==0xffffffeb and ctrl.r[3]==0xfffffffd//7,'independent unsigned mul/div HI-LO microfixture')
for fixture in f['cases']:
 n=fixture['total'];counter=base64.b64decode(fixture['counter']);percent=base64.b64decode(fixture['percentage']);code={}
 for origin,b in [(f['countAddress'],counter),(f['percentAddress'],percent)]:
  for i in range(0,len(b),4):code[origin+i]=struct.unpack_from('<I',b,i)[0]
 check(len(counter)<=128 and len(percent)<=192,f'N{n} emitted code fits the separately reserved owners')
 scenarios=set()
 for k in {0,n//2,max(0,n-1),n}:
  for g in [0,1,11999,12000,12001,13999,14000]:
   for d,e in [(0,0),(79,12),(80,11),(80,12)]:scenarios.add((g,d,e,k))
 rand=random.Random(n)
 for _ in range(100):scenarios.add((rand.randrange(14001),rand.randrange(81),rand.randrange(13),rand.randrange(n+1)))
 samples=[]
 for g,d,e,k in sorted(scenarios):
  m=o.Scalar(code);m.map(0x80077908,0x480);m.map(f['dataAddress'],4*n);m.map(0x80075860,4);m.map(0x80075750,4);m.map(0x80075810,4);m.map(0x801EF000,0x1000)
  for i,entry in enumerate(fixture['entries']):
   level,row=entry['level'],entry['row'];m.poke(f['dataAddress']+i*4,(level<<16)|row)
   if i<k:
    word=0x80077908+level*32+(row>>5)*4;m.poke(word,m.read(word)|(1<<(row&31)))
  m.poke(0x80075860,g);m.poke(0x80075750,d);m.poke(0x80075810,e);m.r[29]=0x801efff0;m.r[31]=0xdeadbee0;m.r[16]=0xabab1234
  before=m.bytes(0x80077908,0x480);m.writes.clear();m.run(f['percentAddress'],{0xdeadbee0},maximum=30000)
  exp=expected(g,d,e,k,n);check(m.r[2]==exp,f'N{n} G{g} D{d} E{e} K{k}: emitted percent={m.r[2]} reference={exp}')
  check(m.bytes(0x80077908,0x480)==before and m.r[29]==0x801efff0 and m.r[31]==0xdeadbee0 and m.r[16]==0xabab1234,f'N{n} fixture does not mutate progress and restores stack/ra/s0')
  if (g,d,e,k) in [(12000,80,12,n),(14000,80,12,n),(14000,80,12,max(0,n-1))]:samples.append(dict(gems=g,dragons=d,retailEggs=e,requiredCollected=k,percentage=m.r[2]))
 results.append(dict(total=n,cases=len(scenarios),counterBytes=len(counter),percentageBytes=len(percent),counterSHA256=o.sha(counter),percentageSHA256=o.sha(percent),samples=samples))
print(json.dumps(dict(marker='NATIVE_REQUIRED_EGG_PERCENT_SCALAR_OK',checks=len(checks),results=results,fullCpuEmulation=False,nativeRuntime=False,cardIO=False),indent=2))
