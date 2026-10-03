#!/usr/bin/env python3
"""Replay actual original rescue-loader words against current smoke/export output.

Usage: Test-AlpineArtisansRescue.py SMOKE_OUTPUT [NORMAL_EXPORT_OUTPUT]
No emulator, private RAM/expected-result fixtures, capstone or external packages.
CD/SPU hardware calls are boundaries; authenticated disc bytes supply each read.
The bounded integer CPU preserves native branch and load delays; unsupported
instructions/memory accesses fail. Native gameplay acceptance remains separate.
"""
from pathlib import Path
import struct,json,hashlib,sys
P=Path(sys.argv[1]).resolve();M=json.loads((P/'rescue-replay-input.json').read_text())
E=(P/'rescue-original.exe').read_bytes();O=(P/'rescue-original.data').read_bytes();B=(P/'rescue-former-order.data').read_bytes()
if len(sys.argv)>2:
 X=Path(sys.argv[2]).resolve();F=(X/'rescue-export.data').read_bytes();R=json.loads((X/'rescue-export-readback.json').read_text());assert R['DataSHA256']==M['CorrectedDataSHA256'] and R['LevelEntryOffset']==M['LevelEntryOffset'] and R['WadLba']==M['WadLba'],'actual export readback differs from current compose replay input'
else:F=(P/'artisans.data').read_bytes()
def s32(x):return (x&0xffffffff)-0x100000000 if x&0x80000000 else x&0xffffffff
def s16(x):return (x&65535)-65536 if x&32768 else x&65535
class Scalar:
 def __init__(self,ram):
  self.mem=bytearray(ram);self.r=[0]*32;self.c=[0]*32;self.d=[0]*32;self.pc=0;self.branch=None;self.load=None;self.lo=self.hi=0;self.steps=0;self.visits={}
 def buf(self,a):
  if a&0x1fffffff<0x200000:return self.mem,a&0x1fffff
  raise Exception('unowned memory '+hex(a))
 def rd(self,a,n,s=False):
  b,i=self.buf(a);return int.from_bytes(b[i:i+n],'little',signed=s)
 def wr(self,a,n,v):
  b,i=self.buf(a);b[i:i+n]=(v&((1<<(8*n))-1)).to_bytes(n,'little')
 def step(self):
  pc=self.pc;self.visits[pc]=self.visits.get(pc,0)+1;w=self.rd(pc,4);op=w>>26;rs=w>>21&31;rt=w>>16&31;rd=w>>11&31;sh=w>>6&31;f=w&63;imm=s16(w);a=self.r[rs];b=self.r[rt];oldbranch=self.branch;oldload=self.load;self.branch=self.load=None;written=set()
  def reg(i,v):
   if i:self.r[i]=v&0xffffffff;written.add(i)
  def branch(cond):
   if cond:self.branch=(pc+4+imm*4)&0xffffffff
  if op==0:
   if f in [0,2,3]:reg(rd,[b<<sh,b>>sh,s32(b)>>sh][[0,2,3].index(f)])
   elif f in [4,6,7]:reg(rd,[b<<(a&31),b>>(a&31),s32(b)>>(a&31)][[4,6,7].index(f)])
   elif f==8:self.branch=a
   elif f==9:reg(rd,pc+8);self.branch=a
   elif f==16:reg(rd,self.hi)
   elif f==18:reg(rd,self.lo)
   elif f in [24,25]:
    v=(s32(a)*s32(b) if f==24 else a*b)&0xffffffffffffffff;self.lo=v&0xffffffff;self.hi=v>>32
   elif f in [26,27]:
    aa,bb=(s32(a),s32(b)) if f==26 else (a,b)
    if not bb:raise Exception('divide zero')
    q=abs(aa)//abs(bb);q=-q if (aa<0)!=(bb<0) else q;self.lo=q&0xffffffff;self.hi=(aa-q*bb)&0xffffffff
   elif f in [32,33]:reg(rd,a+b)
   elif f in [34,35]:reg(rd,a-b)
   elif f==36:reg(rd,a&b)
   elif f==37:reg(rd,a|b)
   elif f==38:reg(rd,a^b)
   elif f==39:reg(rd,~(a|b))
   elif f==42:reg(rd,int(s32(a)<s32(b)))
   elif f==43:reg(rd,int(a<b))
   else:raise Exception('special '+hex(w))
  elif op==1:
   if rt==0:branch(s32(a)<0)
   elif rt==1:branch(s32(a)>=0)
   else:raise Exception('regimm '+hex(w))
  elif op in [2,3]:
   self.branch=(pc+4)&0xf0000000|(w&0x3ffffff)<<2
   if op==3:reg(31,pc+8)
  elif op==4:branch(a==b)
  elif op==5:branch(a!=b)
  elif op==6:branch(s32(a)<=0)
  elif op==7:branch(s32(a)>0)
  elif op in [8,9]:reg(rt,a+imm)
  elif op==10:reg(rt,int(s32(a)<imm))
  elif op==11:reg(rt,int(a<(imm&0xffffffff)))
  elif op==12:reg(rt,a&(w&65535))
  elif op==13:reg(rt,a|(w&65535))
  elif op==14:reg(rt,a^(w&65535))
  elif op==15:reg(rt,(w&65535)<<16)
  elif op in [32,33,35,36,37]:
   n={32:1,33:2,35:4,36:1,37:2}[op];v=self.rd((a+imm)&0xffffffff,n,op in [32,33]);self.load=(rt,v&0xffffffff)
  elif op in [40,41,43]:self.wr((a+imm)&0xffffffff,{40:1,41:2,43:4}[op],b)
  else:raise Exception('instruction '+hex(w))
  if oldload and oldload[0] not in written and oldload[0]:self.r[oldload[0]]=oldload[1]
  self.pc=oldbranch if oldbranch is not None else pc+4;self.steps+=1

H=lambda b:hashlib.sha256(b).hexdigest();U=lambda b,a:struct.unpack_from('<I',b,a)[0]
assert H(E)=='a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998'
assert H(O)=='86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f'
MODEL=M['ModelStart'];STOP=0x80001000;checks=0;cases=[]
def check(ok,why):
 global checks
 assert ok,why;checks+=1
class Replay(Scalar):
 def __init__(self,ram):super().__init__(ram);self.writes=[]
 def wr(self,a,n,v):self.writes.append((a,n));return super().wr(a,n,v)
def machine(data=F):
 mem=bytearray(0x200000);mem[0xf800:0xf800+len(E)]=E;mem[0x76c00:0x76e00]=data[:0x200];c=Replay(mem);c.r[29]=0x801ff000;c.wr(0x800785e0,4,MODEL);c.wr(0x8007a720,4,M["LevelEntryOffset"]);return c
def run(c,start,stop,bound=300000):
 c.pc=start;c.branch=c.load=None;c.steps=0;c.visits={}
 while c.pc!=stop:
  assert c.steps<bound,(hex(c.pc),c.steps);c.step()
 return c.steps
def regs(data):
 return [(i,struct.unpack_from('<H',data,0x150+2*i)[0],U(data,0x50+4*i)) for i in range(1,64) if U(data,0x50+4*i)>0]
def dragon(data):return next(root for slot,cls,root in regs(data) if cls==250)-U(data,16)+MODEL
for data,label in [(B,'former-failed'),(F,'fixed')]:
 for index in range(5):
  c=machine(data);c.wr(0x80077054,4,index);c.wr(0x80076760,4,dragon(data));run(c,0x80014c04,0x80016698)
  size=c.r[6];sf=4+index;offset,length=U(data,sf*8),U(data,sf*8+4);original_offset=U(O,sf*8);stream=data[offset:offset+length]
  check(stream==O[original_offset:original_offset+length],label+' stream exact')
  check(size==min(length,(dragon(data)-MODEL)//2048*2048),label+' original native sector-rounded model250 boundary')
  check(c.r[5]==MODEL,label+' native CD target model start');check(c.r[7]==offset+M["LevelEntryOffset"],label+' retained level WAD+stream CD source')
  if c.rd(0x80077064,4)==0:
   cases.append(dict(Layout=label,Stream=sf,Mode='whole stream',ReadBytes=size));continue
  # Supply exact authenticated CD read bytes; execute the actual native header
  # copy. No CD/SPU implementation or callback is substituted.
  c.mem[MODEL&0x1fffff:(MODEL&0x1fffff)+size]=stream[:size];run(c,0x80014cdc,0x80014d94)
  check(bytes(c.mem[0x77030:0x77054])==stream[:36],label+' native header memcpy exact')
  c.r[16]=0x8007703c;c.r[17]=0x800785e0;run(c,0x80014dd4,0x80016958)
  extent=U(stream,12);requested=c.r[6]
  check(requested==(size-extent)&0xffffffff,label+' native underflow/subtraction argument exact')
  if requested&0x80000000:
   check(label=='former-failed',label+' no fixed underflow');cases.append(dict(Layout=label,Stream=sf,ReadBytes=size,HeaderExtent=extent,NativeCopyBytes=hex(requested),Refused=True));continue
  check(requested>0 and requested%4==0,label+' valid native word-copy count');before=bytes(c.mem);src=c.r[5];dst=c.r[4];c.r[31]=STOP;steps=run(c,0x80016958,STOP)
  check(c.mem[dst&0x1fffff:(dst&0x1fffff)+requested]==before[src&0x1fffff:(src&0x1fffff)+requested],label+' actual native overlap copy exact')
  check(all(MODEL<=a<dragon(data) for a,n in c.writes if a>=MODEL and a<0x80100000+0x80000),label+' copy never writes statue/live model boundary')
  check(c.mem[0x10000:0x6f000]==before[0x10000:0x6f000],label+' all executable code remains intact')
  cases.append(dict(Layout=label,Stream=sf,ReadBytes=size,HeaderExtent=extent,NativeCopyBytes=requested,NativeCopySteps=steps))
# Actual native model registration/reload loop, including 133E0 full pointer and
# animated mesh/texture fixup. It uses all retained ascending header slots.
c=machine();mo,ml=U(F,16),U(F,20);c.mem[MODEL&0x1fffff:(MODEL&0x1fffff)+ml]=F[mo:mo+ml];c.mem[0x76378:0x76b78]=bytes(2048);steps=run(c,0x800152c8,0x80015334,700000);loaded=[]
for slot,cls,root in regs(F):
 ptr=c.rd(0x80076378+4*cls,4);expected=MODEL+root-mo;check(ptr&0x1fffff==expected&0x1fffff,'actual native full reload pointer class'+str(cls));loaded.append(dict(Class=cls,Slot=slot,Pointer=hex(ptr)))
count=M.get('ExpectedModelCount',25);required=M.get('RequiredModelClasses',[509,503,502]);check(len(loaded)==count,'all complete native/imported models reload through original63slot inventory');check(all(any(x['Class']==cls for x in loaded) for cls in required),'complete authenticated model reload owners');check(c.mem[0x10000:0x6f000]==E[0x800:0x6f000-0xf800],'native full model reload preserves code')
check(H(F)==M['CorrectedDataSHA256'] and H(O)==M['OriginalDataSHA256'] and H(E)==M['NativeExeSHA256'],'actual current smoke/output/input hashes match')
receipt=dict(Status='ACTUAL_NATIVE_RESCUE_COPY_AND_MODEL_RELOAD_REPLAY_PASS_GAMEPLAY_PENDING',Checks=checks,NativeExeSHA256=H(E),OriginalDataSHA256=H(O),FixedDataSHA256=H(F),FormerDataSHA256=H(B),Cases=cases,NativeReloadSteps=steps,ReloadedModels=loaded,Scope='Native integer instructions and real package/stream bytes only. No CD/SPU hardware, rescue dialogue, gameplay, or whole-RAM lifetime acceptance.')
(P/'native-rescue-replay.json').write_text(json.dumps(receipt,indent=2)+'\n');print(('MAGIC_ARTISANS' if M.get('ExpectedModelCount')==33 else 'ALPINE_ARTISANS')+'_NATIVE_RESCUE_REPLAY_OK checks='+str(checks))
