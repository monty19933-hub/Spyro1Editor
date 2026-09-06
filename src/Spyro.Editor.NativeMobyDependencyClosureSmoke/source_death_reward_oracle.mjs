// Independent read-only reward/target oracle. No editor DLL, ROM or payload writes.
import fs from 'node:fs';import crypto from 'node:crypto';import {spawnSync} from 'node:child_process';
const source=process.argv[2]??'Spyro the Dragon (USA).bin';
const baseRun=spawnSync(process.execPath,['src/Spyro.Editor.NativeMobyDependencyClosureSmoke/source_overlay_oracle.mjs',source],{encoding:'utf8'});
if(baseRun.status!==0)throw Error(baseRun.stderr);const base=JSON.parse(baseRun.stdout);
const fd=fs.openSync(source,'r'),sha=b=>crypto.createHash('sha256').update(b).digest('hex'),check=(v,m)=>{if(!v)throw Error(m);};
function read(lba,off,n){const b=Buffer.alloc(n);for(let i=0;i<n;){const q=lba*2048+off+i,k=Math.min(n-i,2048-q%2048);check(fs.readSync(fd,b,i,k,Math.floor(q/2048)*2352+24+q%2048)===k,'short raw read');i+=k;}return b;}
const original=read(53875,0,0x66000),dark=read(37,0xEF6000,0xB000),donor=read(37,0x2F6B000+0x1CE800+0x6388,0x58),pete=read(37,0x7B8800,0x800);
check(sha(original)==='a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998','original EXE');
check(sha(dark)==='9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32','original Dark overlay');
check(sha(donor)==='59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82','donor row');
check(donor[0x53]===0x56&&donor[0x3A]===0x7F,'unclaimed deterministic reward');
// This lies inside the independently linked main fragment 0x80082818+0x668:
// a0=current Wizard, a1=3, a2=0, JAL native death emitter, delay a3=0.
const emitterCall=read(37,0x2F62E90,0x14);
check(emitterCall.toString('hex')==='212060020300052421300000f0ea000c21380000','source Wizard death-emitter call and delay');
check(sha(emitterCall)==='ca83fe86286714e440334c486bb64ca44d014dbd616c8bbbafd9d51503081ad4','source Wizard emitter call certificate');
check(pete.readUInt32LE(20)===0x33724&&pete.readUInt32LE(24)===0x56&&pete.readUInt32LE(28)===0x3382C,'shared PETE56 owner');
const model=read(37,0x7EBF24,0x108),modelSha=sha(model);
check(modelSha==='52404b4a45b86d26298f62a8b97fa92703fdd7ca6ea5cbde025e17ac8ce46ea8','complete model56');
// Execute only the two source-certified straight compare chains, including
// branch/JAL delay slots. Memory operations are restricted to the exact class
// load and emitted owner-byte store in these chains.
function chain(code,basis,start,actorClass,parentIndex=128){const r=new Uint32Array(32);r[16]=actorClass;r[19]=0x10000;r[21]=0x20000+parentIndex*0x58;let pc=start,pending=null,owner=null;
 for(let step=0;step<128;step++){
  if(pc<basis||pc>=basis+code.length)return {pc,r,owner};
  const w=code.readUInt32LE(pc-basis),op=w>>>26,rs=(w>>>21)&31,rt=(w>>>16)&31,rd=(w>>>11)&31,imm=(w<<16)>>16,next=pending;pending=null;
  if(w===0){}
  else if(op===9)r[rt]=(r[rs]+imm)>>>0;
  else if(op===10)r[rt]=(r[rs]|0)<imm?1:0;
  else if(op===11)r[rt]=r[rs]<(imm>>>0)?1:0;
  else if(op===15)r[rt]=(imm<<16)>>>0;
  else if(op===4||op===5)pending=((op===4?r[rs]===r[rt]:r[rs]!==r[rt])?pc+4+imm*4:pc+8)>>>0;
  else if(op===2||op===3){if(op===3)r[31]=pc+8;pending=(((pc+4)&0xF0000000)|((w&0x3FFFFFF)<<2))>>>0;}
  else if(op===0&&(w&63)===33)r[rd]=(r[rs]+r[rt])>>>0;
  else if(op===0&&(w&63)===35)r[rd]=(r[rs]-r[rt])>>>0;
  else if(op===0&&(w&63)===0)r[rd]=(r[rt]<<((w>>>6)&31))>>>0;
  else if(op===0&&(w&63)===3)r[rd]=((r[rt]|0)>>((w>>>6)&31))>>>0;
  else if(op===0x23){check(rs===4&&rt===4&&((r[rs]+imm)>>>0)===0x80075828,'unexpected placed-row origin read');r[rt]=0x20000;}
  else if(op===0x21){check(rs===16&&rt===3&&imm===0x36,'unexpected class read');r[rt]=actorClass;}
  else if(op===0x28){check(rs===19&&rt===3&&imm===0x56,'unexpected owner store');owner=r[rt]&255;}
  else throw Error('unowned comparison instruction '+w.toString(16));
  r[0]=0;pc=next??pc+4;
 }
 throw Error('bounded comparison exceeded');
}
// Begin before native parent-pointer -> byte owner arithmetic, not with a
// preselected owner value. Row128 is at original placed-row base +128*0x58.
const spawn=chain(dark.subarray(0x82998-0x7AA38,0x82B14-0x7AA38),0x80082998,0x80082998,0x56);
check(spawn.pc===0x80082C38&&spawn.owner===128,'source selector56 initializer/owner');
for(const index of [0,1,127,128,255,256]){
 const actual=chain(dark.subarray(0x82998-0x7AA38,0x82B14-0x7AA38),0x80082998,0x80082998,0x56,index);
 check(actual.pc===0x80082C38&&actual.owner===(index<256?index:0),'native byte-owner boundary');
}
const award=chain(original.subarray(0x2C264,0x2C2BC),0x8003BA64,0x8003BA64,0x56);
check(award.pc===0x8003B854&&award.r[4]===10,'source class56 award');
const copy=parseInt(base.copyBuffer,16),overlayExe=Buffer.from(original),low=(copy<<16)>>16,high=(copy-low)/65536;
overlayExe.writeUInt32LE((0x3C020000|high)>>>0,0x4B048);overlayExe.writeUInt32LE((0x24420000|(low&65535))>>>0,0x4B04C);
check(sha(overlayExe)===base.exeSha,'independent overlay base');
const levelId=12,slot=6*(Math.floor(levelId/10)-1)+levelId%10,targetOffset=0x5FC38+slot*2;
check(slot===2&&targetOffset===0x5FC3C&&overlayExe.readUInt16LE(targetOffset)===100,'native level slot/target');
const final=Buffer.from(overlayExe);final.writeUInt16LE(100+award.r[4],targetOffset);
check(final.subarray(0,targetOffset).equals(overlayExe.subarray(0,targetOffset))&&final.subarray(targetOffset+2).equals(overlayExe.subarray(targetOffset+2)),'unchanged complement');
const reverted=Buffer.from(final);reverted.writeUInt16LE(100,targetOffset);check(reverted.equals(overlayExe),'exact revert');
console.log(JSON.stringify({sourceImageSha:base.sourceImageSha,baseExeSha:base.exeSha,finalExeSha:sha(final),selector:86,value:award.r[4],nativeEmitterCall:'800828d4',emitterCallSha:sha(emitterCall),nativeSpawnEntry:spawn.pc.toString(16),emittedOwnerByte:spawn.owner,sharedModelSha:modelSha,slot,targetOffset:targetOffset.toString(16),targetBefore:100,targetAfter:110,runtime:false,publication:false},null,2));fs.closeSync(fd);
