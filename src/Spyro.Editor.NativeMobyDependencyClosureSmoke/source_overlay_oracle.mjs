// Read-only independent raw-disc oracle. No editor assemblies and no ROM writes.
import fs from 'node:fs';
import crypto from 'node:crypto';
const path=process.argv[2]??'Spyro the Dragon (USA).bin', fd=fs.openSync(path,'r');
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const assert=(ok,why)=>{if(!ok)throw Error(why);};
const stream=fs.createReadStream(path), full=crypto.createHash('sha256');
for await(const chunk of stream)full.update(chunk);
assert(fs.fstatSync(fd).size===661547040&&full.digest('hex')==='fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37','original image');
function logical(at,n){const b=Buffer.alloc(n);for(let i=0;i<n;){let q=at+i,k=Math.min(n-i,2048-q%2048);assert(fs.readSync(fd,b,i,k,Math.floor(q/2048)*2352+24+q%2048)===k,'short read');i+=k;}return b;}
const base=0x8007AA38, wizard=logical(37*2048+0x2F5B000,0x10000),dark=logical(37*2048+0xEF6000,0xB000),exe=logical(53875*2048,0x66000);
assert(sha(wizard)==='5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1','donor');
assert(sha(dark)==='9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32','destination');
assert(sha(exe)==='a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998','exe');
const jump=pc=>(0x08000000|((pc>>>2)&0x3FFFFFF))>>>0;
function words(w){const b=Buffer.alloc(w.length*4);w.forEach((v,i)=>b.writeUInt32LE(v>>>0,i*4));return b;}
const jbridge=pc=>words([jump(pc),0]);
function transfer(w,pc){let op=w>>>26;
 if(op===2||op===3)return {kind:op===2?'jump':'call',target:(((pc+4)&0xF0000000)|((w&0x3FFFFFF)<<2))>>>0};
 if([1,4,5,6,7].includes(op))return {kind:'branch',target:(pc+4+((w<<16)>>16)*4)>>>0};
 if(op===0&&(w&63)===8)return {kind:'return'};
 if(op===0&&(w&63)===9)return {kind:'callback'};
 return null;
}
function packageOf(spans,patch){const fragments=spans.map(([pc,n])=>({pc,bytes:Buffer.from(wizard.subarray(pc-base,pc-base+n))}));
 for(const f of fragments)if(patch)patch(f);
 const edges=[];
 for(const f of fragments){for(let i=0;i<f.bytes.length;i+=4){const t=transfer(f.bytes.readUInt32LE(i),f.pc+i);if(t)edges.push({...t,pc:f.pc+i});}
  let w=f.bytes.readUInt32LE(f.bytes.length-8),t=transfer(w,f.pc+f.bytes.length-8);
  f.falls=!(t&&(t.kind==='jump'||t.kind==='return'||(w>>>26)===4&&((w>>>21)&31)===((w>>>16)&31)));
 }
 for(const f of fragments)if(edges.some(t=>t.target===f.pc+f.bytes.length-4))f.falls=true;
 for(const f of fragments)if(f.falls)edges.push({kind:'fall',pc:f.pc+f.bytes.length,target:f.pc+f.bytes.length});
 return {fragments,edges,length:fragments.reduce((n,f)=>n+f.bytes.length+(f.falls?8:0),0)};
}
const main=packageOf([[0x8007D118,0x1C],[0x8007EAD0,0x1A0],[0x80082818,0x668],[0x800842CC,0x24],[0x8008434C,0x14],[0x80085A4C,0x4C],[0x80086A50,0x10]]);
const spawn=packageOf([[0x80086DD8,0x94],[0x800870F0,0x230],[0x80087B08,0x38]],f=>{if(f.pc===0x80086DD8){assert(f.bytes.readUInt32LE(0x8C)===0x10400033,'class26 before');f.bytes.writeUInt32LE(jump(0x800870F0),0x8C);}});
let basePatches=0,stackPatches=0;
const particle=packageOf([[0x80088370,0x98],[0x800888E8,0xFC],[0x80089934,0x11C],[0x80089F28,0xD0]],f=>{
 if(f.pc>0x800888E8)return;
 for(let i=0;i<f.bytes.length;i+=4){let w=f.bytes.readUInt32LE(i),op=w>>>26,rs=(w>>>21)&31;
  if(rs===18&&[0x21,0x24,0x25,0x28,0x29].includes(op)){w=((w&~(31<<21))|(17<<21))>>>0;basePatches++;}
  else if(rs===29&&[0x25,0x29].includes(op)&&[0x70,0x72,0x74].includes(w&65535)){w-=0x40;stackPatches++;}
  f.bytes.writeUInt32LE(w,i);
 }
});
assert(basePatches===37&&stackPatches===12,'particle field ownership');
const blocks=[];let cursor=0xB000;
function owner(id,length){let o={id,offset:cursor,pc:base+cursor,length};cursor+=length;blocks.push(o);return o;}
function mapping(pkg,o){const m=new Map();let k=0;for(const f of pkg.fragments){for(let i=0;i<f.bytes.length;i+=4)m.set(f.pc+i,o.pc+k+i);k+=f.bytes.length+(f.falls?8:0);}return m;}
const mo=owner('native-main',main.length),so=owner('private-spawn26',spawn.length),po=owner('native-particles',particle.length);
const bridgeKeys=[0x8008442C,0x80084448,0x80084450,0x8008553C],bridges=new Map(bridgeKeys.map(pc=>[pc,owner('particle-return-'+pc.toString(16),8)]));
const effect=owner('effect-call',8),exit=owner('main-exit',0x98),le=owner('lightning-adapter',0x98),we=owner('wizard-adapter',0x98),dispatch=owner('class-dispatch',0x44);
const end=(cursor+0x7FF)&~0x7FF;owner('owned-sector-padding',end-cursor);
const mm=mapping(main,mo),sm=mapping(spawn,so),pm=mapping(particle,po);
const continuations=new Map([[0x800893EC,0x80084448],[0x800893F4,0x80084450],[0x800893D0,0x8008442C],[0x8008A848,0x8008553C]]);
const spawnCallers=new Set([0x8007EBDC,0x80082DB4,0x80082DF0,0x80082E38]);
function link(pkg,o,map){const b=Buffer.alloc(o.length),positions=new Map();let k=0;
 for(const f of pkg.fragments){f.bytes.copy(b,k);for(let i=0;i<f.bytes.length;i+=4)positions.set(f.pc+i,k+i);if(f.falls)positions.set(f.pc+f.bytes.length,k+f.bytes.length);k+=f.bytes.length+(f.falls?8:0);}
 for(const t of pkg.edges){if(t.kind==='return')continue;let target=map.get(t.target);
  if(target===undefined){if(t.kind==='call'){target=t.target;assert(target>=0x80010000&&target<0x80075800,'external EXE call');}
   else if(t.kind==='callback'){if(spawnCallers.has(t.pc))target=sm.get(0x80086DD8);else {assert([0x8007EC60,0x80082840,0x8008728C].includes(t.pc),'callback source');target=effect.pc;}}
   else if(t.target===0x80086D8C)target=exit.pc;
   else {assert(pkg===particle&&continuations.has(t.target),'unknown continuation');target=bridges.get(continuations.get(t.target)).pc;}
  }
  const i=positions.get(t.pc),pc=o.pc+i;
  if(t.kind==='branch'){const d=(target-pc-4)/4;assert(Number.isInteger(d)&&d>=-32768&&d<=32767,'branch range');b.writeUInt32LE(((b.readUInt32LE(i)&0xFFFF0000)|(d&65535))>>>0,i);}
  else b.writeUInt32LE((jump(target)|(t.kind==='call'||t.kind==='callback'?0x04000000:0))>>>0,i);
 }
 return b;
}
const regs=Array.from({length:31},(_,i)=>i+1).filter(i=>i!==29);
const I=(op,rs,rt,n)=>((op<<26)|(rs<<21)|(rt<<16)|(n&65535))>>>0;
function entry(target){return words([I(9,29,29,-0x2B0),...regs.map((r,i)=>I(0x2B,29,r,0x230+4*i)),0x00004010,I(0x2B,29,8,0x2A8),0x00004012,I(0x2B,29,8,0x2AC),I(9,0,30,1),jump(target),0]);}
function leave(){return words([I(0x23,29,8,0x2A8),0,0x01000011,I(0x23,29,8,0x2AC),0,0x01000013,...regs.map((r,i)=>I(0x23,29,r,0x230+4*i)),jump(0x80082914),I(9,29,29,0x2B0)]);}
const result=Buffer.alloc(end);dark.copy(result);
function put(o,b){assert(b.length===o.length,'block length');b.copy(result,o.offset);}
put(mo,link(main,mo,mm));put(so,link(spawn,so,sm));put(po,link(particle,po,pm));
for(const [pc,o]of bridges)put(o,jbridge(pc));
put(effect,jbridge(0x800844A0));put(exit,leave());put(le,entry(mm.get(0x8007EAD0)));put(we,entry(mm.get(0x80082818)));
put(dispatch,words([0x24020026,0x14620003,0,jump(le.pc),0,0x2402011B,0x14620003,0,jump(we.pc),0,0x240200FB,0x10620003,0x286200FC,jump(0x8007AF30),0,jump(0x800812FC),0]));
assert(dark.subarray(0x4EC,0x4FC).toString('hex')==='fb000224f4186210fc00622847004010','native dispatch');jbridge(dispatch.pc).copy(result,0x4EC);
for(const [at,source,before]of [[0x158,0x80088370,0x80084450],[0x240,0x800888E8,0x80084450],[0x29C,0x80089934,0x8008553C],[0x384,0x80089F34,0x8008553C]]){assert(dark.readUInt32LE(at)===before,'hook preimage');result.writeUInt32LE(pm.get(source),at);}
const finalExe=Buffer.from(exe),copy=base+end,low=(copy<<16)>>16,high=(copy-low)/65536;
assert(exe.subarray(0x4B048,0x4B058).toString('hex')==='0880023c945542240880013cd88522ac','copy-buffer preimage');
finalExe.writeUInt32LE((0x3C020000|high)>>>0,0x4B048);finalExe.writeUInt32LE((0x24420000|(low&65535))>>>0,0x4B04C);
const overlaySha=sha(result),exeSha=sha(finalExe);
assert(overlaySha==='10f82ee1028a9ad4fa9045d948a88e76545847031b954ac08ad0e74e6e9c7083','independent overlay exact oracle');
assert(exeSha==='14aa937f198740373b37312eb3054cad46dd1ae05f1920c145d6bc1fde9a8635','independent executable exact oracle');
console.log(JSON.stringify({sourceImageSha:'fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37',overlaySha,exeSha,copyBuffer:copy.toString(16),overlayLength:end,blocks:blocks.map(o=>({...o,pc:o.pc.toString(16),sha:sha(result.subarray(o.offset,o.offset+o.length))})),runtime:false,publication:false},null,2));
fs.closeSync(fd);
