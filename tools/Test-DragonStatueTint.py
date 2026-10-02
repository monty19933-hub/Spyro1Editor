#!/usr/bin/env python3
"""Execute shipped tint words with R3000 load/branch delays and HI/LO hazards."""
import json,struct,random,pathlib,hashlib,runpy,itertools
root=pathlib.Path(__file__).resolve().parent;h=runpy.run_path(str(root/'Build-DragonStatueTint.py'))['hook'];rng=random.Random(934112)
MASK=0xFFFFFFFF
checks=0
for colour in [None,0,0xFFFFFF,0x0000FF,0x00FF00,0xFF0000,0xB82AE6]+[rng.randrange(1<<24) for _ in range(100)]:
 for inplace,count in itertools.product([False,True],[1,2,255,256]):
  regs=[rng.getrandbits(32) for _ in range(32)];regs[0]=0;regs[28]=0x80090000;regs[31]=0x80092000;regs[25]=0x80070DF4 if inplace else 0x80091000
  original=regs.copy();raw=0x00818600 if colour is None else 0x7E000000|colour
  mem=bytearray(2*1024*1024);addr=lambda x:x&0x1FFFFF
  def w(a,n,v):mem[addr(a):addr(a)+n]=(v&((1<<(n*8))-1)).to_bytes(n,'little')
  def rd(a,n):return int.from_bytes(mem[addr(a):addr(a)+n],'little')
  for k,word in h['WordsByAddress'].items():w(int(k),4,word)
  w(regs[28]+0x30,4,raw);w(regs[31]+2,2,count)
  colours=[rng.getrandbits(32) for _ in range(count)]
  for k,c in enumerate(colours):w(regs[25]+k*4,4,c)
  before=mem.copy();pc=h['Host'];pendingLoad=None;pendingBranch=None;hi=rng.getrandbits(32);lo=regs[28];steps=0;writes=[];lastmflo=-100
  while pc!=h['Return']:
   word=rd(pc,4);op=word>>26;rs=(word>>21)&31;rt=(word>>16)&31;rdd=(word>>11)&31;sh=(word>>6)&31;fn=word&63;imm=word&65535;simm=imm if imm<32768 else imm-65536
   src=regs.copy();newload=None;branch=None;dest=None
   def setr(d,v):
    global dest
    if d:regs[d]=v&MASK;dest=d
   if op==0:
    if fn==0:setr(rdd,src[rt]<<sh)
    elif fn==2:setr(rdd,src[rt]>>sh)
    elif fn==0x21:setr(rdd,src[rs]+src[rt])
    elif fn==0x25:setr(rdd,src[rs]|src[rt])
    elif fn==0x2B:setr(rdd,int(src[rs]<src[rt]))
    elif fn==0x19:
     assert steps-lastmflo>=3,'R3000 mflo/mult hazard'
     p=src[rs]*src[rt];hi=p>>32;lo=p&MASK
    elif fn==0x12:setr(rdd,lo);lastmflo=steps
    elif fn==0x13:lo=src[rs]
    else:raise AssertionError(('fn',hex(pc),hex(word)))
   elif op==15:setr(rt,imm<<16)
   elif op==9:setr(rt,src[rs]+simm)
   elif op==12:setr(rt,src[rs]&imm)
   elif op==14:setr(rt,src[rs]^imm)
   elif op in (35,36,37):newload=(rt,rd(src[rs]+simm,{35:4,36:1,37:2}[op]))
   elif op==43:w(src[rs]+simm,4,src[rt]);writes.append((src[rs]+simm,4))
   elif op in (4,5):branch=(pc+4+simm*4) if (src[rs]==src[rt])==(op==4) else pc+8
   elif op==2:branch=0x80000000|((word&0x3FFFFFF)<<2)
   else:raise AssertionError(('op',hex(pc),hex(word)))
   if pendingLoad is not None and dest!=pendingLoad[0]:regs[pendingLoad[0]]=pendingLoad[1]
   nextpc=pendingBranch if pendingBranch is not None else pc+4;pendingLoad=newload;pendingBranch=branch;pc=nextpc;steps+=1
   assert steps<20000
  assert pendingLoad is None
  if colour is None:
   assert regs[1]==raw and all(regs[n]==original[n] for n in range(32) if n!=1);assert mem==before
  else:
   assert all(regs[n]==original[n] for n in range(32) if n not in (1,2,3,4,5,6,7,25))
   assert regs[25]==0x80070DF4
   r,g,b=colour&255,(colour>>8)&255,(colour>>16)&255
   assert regs[1]==0x800000|(r>>2)<<12|(g>>2)<<6|(b>>2)
   for k,c in enumerate(colours):
    lum=max(c&255,(c>>8)&255,(c>>16)&255)
    expect=(c&0xFF000000)|(r*lum>>8)|((g*lum>>8)<<8)|((b*lum>>8)<<16)
    assert rd(0x80070DF4+k*4,4)==expect
   assert all((a>=h['Data'] and a+n<=h['Data']+16) or (a>=0x80070DF4 and a+n<=0x80070DF4+count*4) for a,n in writes)
   # No shared model mutation. The in-place case is already the native private fog scratch.
   if not inplace:assert mem[0x91000:0x91400]==before[0x91000:0x91400]
  assert lo==original[28];checks+=1
print('DRAGON_TINT_R3000_OK checks='+str(checks))
