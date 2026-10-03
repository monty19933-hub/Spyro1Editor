"""Actual native parser controls on current cleared-Haunted output, no live RAM/GTE.
Usage: python3 Test-PlayerAnimationAlignmentNative.py HAUNTED_INTEGRATION_OUTPUT ORIGINAL_BIN NEW_PROOF.json
"""
from pathlib import Path
import struct,json,hashlib,sys
if len(sys.argv)!=4:raise SystemExit(__doc__)
P=Path(sys.argv[1]);B=Path(sys.argv[2]);OUT=Path(sys.argv[3]);assert not OUT.exists()
H=lambda b:hashlib.sha256(b).hexdigest();U=lambda b,o:struct.unpack_from('<I',b,o)[0];checks=0

def ck(x,m):
 global checks;checks+=1
 if not x:raise ValueError(m)
ck(H(B.read_bytes())=='fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37','complete untouched original USA')
def raw(lba,n):
 with B.open('rb')as f:
  result=bytearray()
  for i in range((n+2047)//2048):f.seek((lba+i)*2352+24);result.extend(f.read(2048))
 return bytes(result[:n])
def wad(offset,n):
 with B.open('rb')as f:
  result=bytearray();at=offset
  while len(result)<n:
   f.seek((37+at//2048)*2352+24+at%2048);size=min(2048-at%2048,n-len(result));result.extend(f.read(size));at+=size
 return bytes(result)
E=raw(53875,0x66000);EB=0x8000f800;ck(H(E)=='a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998','complete native executable')
ck(H(E[0x3a30:0x3be0])=='0cca46f0e44c16b8adea088e3081637ec5b04a512dc2c8eac494f9d16732cefb','complete native stream parser')
directory=wad(0,2048);town=wad(U(directory,16*8),U(directory,16*8+4));haunted=wad(U(directory,64*8),U(directory,64*8+4));ck(H(town)=='7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0','complete original Town fallback source');ck(H(haunted)=='970bc9e977d679f01d3ad65c8f0d07dee746ea2ede5307351dadb1c8887fb79d','complete original Haunted reaction source')
base=(P/'variant0-baseline-data.bin').read_bytes();final=(P/'variant0-final-data.bin').read_bytes();A=(P/'variant0-final-executable.bin').read_bytes();ck(A[0x3a30:0x3be0]==E[0x3a30:0x3be0],'actual composed native parser retained')
def roots(b):return {struct.unpack_from('<H',b,0x150+2*s)[0]:U(b,0x50+4*s)for s in range(1,64)if U(b,0x50+4*s)}
r0,r1,rt=roots(base),roots(final),roots(town)
for c,n in ((14,2028),(15,1932),(16,816)):
 ck(final[r1[c]:r1[c]+n]==town[rt[c]:rt[c]+n]==base[r0[c]:r0[c]+n],'actual complete Town fallback package identity');ck(r1[c]%8==rt[c]%8==r0[c]%8,'actual native fallback rootmod8 retained')
ck(r1[14]+2028==r1[15]and final[r1[14]+2028:r1[14]+2036]==town[rt[15]:rt[15]+8],'actual source E-to-F read-ahead owner')
for c,r in r0.items():ck(c in r1 and r1[c]%8==r%8,'every retained model rootmod8')
for cls,root in r0.items():
 end=min([v for v in r0.values()if v>root]+[U(base,16)+U(base,20)]);length=end-root
 ck(base[root:end]==final[r1[cls]:r1[cls]+length],'every retained complete native model allocation')
def parse(b):
 at=U(b,80);end=U(b,84);out={}
 while True:
  ck(at+4<=end,'complete bounded native terminator');ident=struct.unpack_from('<i',b,at)[0]
  if ident==-1:return out,b[U(b,80):at+4],b[at+4:end]
  ck(0<=ident<46 and ident not in out,'distinct source-owned native animation ID');n=U(b,at+4);ck(n>=40 and n%4==0 and at+n+8<=end,'full record extent');out[ident]=b[at:at+n+8];at+=n+8
old,oldstream,oldpad=parse(base);new,stream,pad=parse(final);source,_,_=parse(haunted)
ck(oldpad==b''and pad==bytes(4),'actual newly issued exactly four zero postterminator bytes');ck(set(new)==set(old)|{26,32},'actual source reaction set');
for k,v in old.items():ck(new[k]==v,'all existing exact player records')
for k in (26,32):ck(new[k]==source[k],'complete source reaction record bytes')
# Reuse only the repository's strict integer MIPS/load-delay interpreter definitions.
# No frozen candidate, baseline RAM, mocked helper or current emulator is required.
W=lambda p:U(A,p-EB)
text=Path(__file__).with_name('Test-HauntedDarkHollowNative.py').read_text();exec(text[text.index('def signed('):text.index('SPAWN=')],globals())
BASE=0x80100000;TABLE=0x80180000;RET=0x0bad0000
def put(m,a,b):
 for i,v in enumerate(b):m.seed(a+i,v,1)
def native(b):
 m=blank();m.limit=1000000;m.word=W;m.r[4]=BASE;m.r[31]=RET;put(m,BASE,b);put(m,TABLE,bytes(0x38+46*4));m.seed(TABLE+0x34,0x801c0000)
 for k in range(46):m.seed(TABLE+0x38+k*4,0xffffffff)
 m.seed(0x80076378,TABLE);m.seed(0x800785fc,BASE);run(m,0x80013230,{RET});return m
replays=[]
for gap in (b'',bytes(4)):
 m=native(stream+gap);ck(m.r[29]==0x801ff000,'complete actual parser restores SP');ck(all(not(BASE+len(stream)<=a<BASE+len(stream)+len(gap))for _,a,_ in m.loads),'native parser never reads its alignment gap');ck(all(not(BASE+len(stream)<=a<BASE+len(stream)+len(gap))for _,a,_,_ in m.writes),'native parser never modifies its alignment gap');replays.append((bytes(m.mem[BASE+i]for i in range(len(stream))),bytes(m.mem[TABLE+i]for i in range(0x38+46*4)),m.steps))
ck(replays[0]==replays[1],'full native hydration/frame pointers/animation table and step count exactly equal with issued gap')
# This is native ignored-storage evidence only: the production issuer refuses
# nonzero/extra/truncated gaps rather than generalizing the parser's negative-ID exit.
negative=native(stream+struct.pack('<I',0xdeadbeef));ck(bytes(negative.mem[BASE+i]for i in range(len(stream)))==replays[0][0],'native exit genuinely precedes trailing storage')
mut=bytearray(stream+pad);struct.pack_into('<I',mut,len(stream)-4,0)
try:native(mut)
except ValueError as e:ck('Unowned native load'in str(e),'terminator mutation crosses exact declared owner')
else:raise ValueError('mutated source terminator unexpectedly bounded')
OUT.write_text(json.dumps({'Status':'CURRENT_COMPOSED_COMPLETE_NATIVE_PARSER_INSTRUCTION_CONTROLS_NOT_GAMEPLAY','Checks':checks,'BaseSHA':H(base),'FinalSHA':H(final),'NativeParserSHA':H(E[0x3a30:0x3be0]),'RecordBytes':sum(len(source[k])for k in (26,32)),'AlignmentPaddingBytes':len(pad),'NativeSteps':replays[1][2],'LaterRootsmod8Preserved':True,'FullFallbacksAndRetainedModelPackagesEqual':True,'ExactAnimationRecordsEqual':True,'NativeGapRead':False,'Limitations':['Complete native loader integer hydration executes in declared isolated storage; GPU/GTE rendering/audio/gameplay not executed','No live RAM or power/health/card writes; production does not admit nonzero/extra/truncated gaps']},indent=2)+'\n');print('PLAYER_ANIMATION_ALIGNMENT_NATIVE_OK',checks,replays[1][2],H(final))
