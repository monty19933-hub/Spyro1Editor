"""Offline bounded R3000/GTE subset replay. Never connects to emulator.

Test callers supply authenticated original EXE and production emission bytes.
RAM writes occur solely in the private bytearray and scratchpad below.
Unknown instructions and MMIO fail instead of being approximated.
Only integer GTE arithmetic is modeled. GPU raster, FLAG/RGB FIFO and SPU
playback are outside this helper and cannot be certified by it.
"""
def s32(x):return (x&0xffffffff)-0x100000000 if x&0x80000000 else x&0xffffffff
def s16(x):return (x&65535)-65536 if x&32768 else x&65535
class Replay:
 def __init__(self,ram):
  self.mem=bytearray(ram);self.scratch=bytearray(1024);self.r=[0]*32;self.c=[0]*32;self.d=[0]*32;self.pc=0x8004ae38;self.branch=None;self.load=None;self.lo=self.hi=0;self.steps=0;self.visits={}
 def buf(self,a):
  if 0x1f800000<=a<0x1f800400:return self.scratch,a-0x1f800000
  if a&0x1fffffff<0x200000:return self.mem,a&0x1fffff
  raise Exception('unowned memory '+hex(a))
 def rd(self,a,n,s=False):
  b,i=self.buf(a);return int.from_bytes(b[i:i+n],'little',signed=s)
 def wr(self,a,n,v):
  b,i=self.buf(a);b[i:i+n]=(v&((1<<(8*n))-1)).to_bytes(n,'little')
 def gte(self,w):
  cmd=w&63;sf=12 if w>>19&1 else 0;lm=w>>10&1;ir=[s16(self.d[i]) for i in [9,10,11]]
  if cmd==12:
   a=[s16(self.c[i]) for i in [0,2,4]];v=[a[1]*ir[2]-a[2]*ir[1],a[2]*ir[0]-a[0]*ir[2],a[0]*ir[1]-a[1]*ir[0]]
  elif cmd==18:
   mx,vi,cv=w>>17&3,w>>15&3,w>>13&3
   assert mx==0 and cv in [0,3],hex(w)
   vec=ir if vi==3 else [s16(self.d[vi*2]),s16(self.d[vi*2]>>16),s16(self.d[vi*2+1])]
   mat=[s16(x) for v in self.c[:5] for x in [v,v>>16]][:9];v=[sum(mat[i*3+j]*vec[j] for j in range(3))+(s32(self.c[5+i])*4096 if cv==0 else 0) for i in range(3)]
  elif cmd==62:v=[s32(self.d[25+i])*(1<<sf)+s16(self.d[8])*ir[i] for i in range(3)]
  elif cmd==40:v=[x*x for x in ir]
  elif cmd==61:v=[s16(self.d[8])*x for x in ir]
  else:raise Exception('unsupported GTE '+hex(w))
  for i,x in enumerate(v):
   x=x>>sf;self.d[25+i]=x&0xffffffff;self.d[9+i]=max(0 if lm else -32768,min(32767,x))&0xffffffff
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
  elif op==50:self.d[rt]=self.rd((a+imm)&0xffffffff,4)
  elif op==58:self.wr((a+imm)&0xffffffff,4,self.d[rt])
  elif op==18:
   if rs>=16:self.gte(w)
   elif rs in [0,2]:self.load=(rt,(self.d if rs==0 else self.c)[rd])
   elif rs in [4,6]:
    if rs==6:self.c[rd]=b
    else:
     self.d[rd]=b
     if rd==30:
      x=(~b)&0xffffffff if b&0x80000000 else b;self.d[31]=32-x.bit_length()
   else:raise Exception('cop2 '+hex(w))
  else:raise Exception('instruction '+hex(w))
  if oldload and oldload[0] not in written and oldload[0]:self.r[oldload[0]]=oldload[1]
  self.pc=oldbranch if oldbranch is not None else pc+4;self.steps+=1
