// Exact raw-source research WAD oracle. No editor DLL and no payload writes.
import fs from'node:fs';import crypto from'node:crypto';import{spawnSync}from'node:child_process';
const source=process.argv[2]??'Spyro the Dragon (USA).bin';
const {overlayBytes,executableBytes}=await import('../Spyro.Editor.NativeMobyDependencyClosureSmoke/source_overlay_oracle.mjs');
const run=spawnSync('python3',['-c',`import contextlib,io,runpy,sys
sys.argv=['source_composition_oracle.py',sys.argv[1]]
with contextlib.redirect_stdout(io.StringIO()): result=runpy.run_path('src/Spyro.Editor.NativeResourceCompositionSmoke/source_composition_oracle.py')
sys.stdout.buffer.write(result['with_particle'])`,source],{maxBuffer:0x400000});
if(run.status!==0)throw Error(run.stderr.toString());
const data=Buffer.from(run.stdout),exe=Buffer.from(executableBytes),sha=b=>crypto.createHash('sha256').update(b).digest('hex'),check=(v,m)=>{if(!v)throw Error(m);};
check(data.length===0x2A0800,'complete resource length');
const scene=data.readUInt32LE(0x18),priorData=Buffer.from(data);
const mutations=[[0xB838+12,92832],[0xB838+16,25248],[0xB838+20,10445],
 [0xEED4+0x30,92832],[0xEED4+0x34,25248],[0xEED4+0x38,10732],
 [0xEED4+0x40,91808],[0xEED4+0x44,25248],[0xEED4+0x48,10732]];
for(const [at,value]of mutations)data.writeInt32LE(value,scene+at);
const inverse=Buffer.from(data);for(const [at]of mutations)inverse.writeInt32LE(priorData.readInt32LE(scene+at),scene+at);
check(inverse.equals(priorData),'placement inverse');
check(exe.readUInt16LE(0x5FC3C)===100,'target preimage');exe.writeUInt16LE(110,0x5FC3C);
check(sha(exe)==='ec1e8ae38fed404f9a28ccc7fbf17d81e92a341aab89906e94c5e6c793f4c3b4','reward EXE');
const fd=fs.openSync(source,'r');function read(lba,offset,length){const b=Buffer.alloc(length);for(let i=0;i<length;){const q=lba*2048+offset+i,k=Math.min(length-i,2048-q%2048);check(fs.readSync(fd,b,i,k,Math.floor(q/2048)*2352+24+q%2048)===k,'short raw read');i+=k;}return b;}
const header=read(37,0,0x800),finalHeader=Buffer.from(header),entries=[];
for(let i=0;i<256;i++){const offset=header.readUInt32LE(i*8),length=header.readUInt32LE(i*8+4);if(offset||length)entries.push({index:i,offset,length});}
entries.sort((a,b)=>a.offset-b.offset);const sourceHash=crypto.createHash('sha256'),finalHash=crypto.createHash('sha256');sourceHash.update(header);
const blocks=[];let old=0x800,cursor=0x800;
for(const entry of entries){check(entry.offset===old,'source packed owner');const before=read(37,entry.offset,entry.length);sourceHash.update(before);
 const after=entry.index===13?overlayBytes:entry.index===14?data:before;
 blocks.push(after);finalHeader.writeUInt32LE(cursor,entry.index*8);finalHeader.writeUInt32LE(after.length,entry.index*8+4);
 entry.finalOffset=cursor;entry.finalLength=after.length;entry.beforeSha=sha(before);entry.afterSha=sha(after);cursor+=after.length;old+=before.length;
}
check(old===0x6927000&&cursor-old===0x14800,'derived41sector growth');finalHash.update(finalHeader);for(const b of blocks)finalHash.update(b);
const exeLba=37+cursor/2048;check(exeLba===53916&&exeLba+exe.length/2048===54120,'derived ISO placement');
const pvd=read(16,0,0x800),rootLba=pvd.readUInt32LE(158),rootLength=pvd.readUInt32LE(166),root=read(rootLba,0,rootLength);
const beforeRoot=Buffer.from(root);let patched=0;
for(let at=0;at<root.length;){const length=root[at];if(!length){at=(Math.floor(at/2048)+1)*2048;continue;}
 const name=root.subarray(at+33,at+33+root[at+32]).toString('ascii').replace(/;1$/,'');
 const spec=name==='WAD.WAD'?[37,cursor]:name==='SCUS_942.28'?[exeLba,exe.length]:null;
 if(spec){for(const [field,value]of [[2,spec[0]],[10,spec[1]]]){root.writeUInt32LE(value,at+field);root.writeUInt32BE(value,at+field+4);}patched++;}
 at+=length;
}
check(patched===2,'two ISO root file owners');
console.log('NATIVE_WIZARD_RESEARCH_RAW_SOURCE_ORACLE '+JSON.stringify({sourceImageSha:'fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37',
 originalWadSha:sourceHash.digest('hex'),wadSha:finalHash.digest('hex'),rootSha:sha(root),dataSha:sha(data),overlaySha:sha(overlayBytes),exeSha:sha(exe),
 growth:cursor-old,exeLba,exeEndLba:exeLba+exe.length/2048,oldWadLast:53874,oldExeLast:54078,newWadLast:exeLba-1,newExeLast:exeLba+exe.length/2048-1,
 entries:entries.filter(e=>e.index===13||e.index===14),outputsWritten:0,runtime:false,normalCreateBin:false}));fs.closeSync(fd);
