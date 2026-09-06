// Independent original-only expected physical disc. No Core/DLL/candidate reads,
// no payload writes, no emulator. Old raw oracles and their CLIs stay unchanged.
import fs from 'node:fs';
import crypto from 'node:crypto';
import path from 'node:path';
import {fileURLToPath, pathToFileURL} from 'node:url';
import {spawnSync} from 'node:child_process';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const source = path.resolve(process.argv[2] ?? 'Spyro the Dragon (USA).bin');
const SIZE = 661547040, SECTORS = SIZE / 2352;
const SOURCE_SHA = 'fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37';
const sha = b => crypto.createHash('sha256').update(b).digest('hex');
let checks = 0;
function check(ok, why) { if (!ok) throw Error(why); checks++; }
const helpers = {
  composition: ['src/Spyro.Editor.NativeResourceCompositionSmoke/source_composition_oracle.py', '4db4d62e30e4184c297e957e2244c46c6030016a0f43703cf3c08ccab8b297fc'],
  append: ['src/Spyro.Editor.NativeWizardAppendSmoke/source_wizard_append_oracle.py', '10cf2dce864a98366d94f48ba3dc356e0cb4379ff5db991bbbf8b6fdc190768f'],
  overlay: ['src/Spyro.Editor.NativeMobyDependencyClosureSmoke/source_overlay_oracle.mjs', '394d5971720dc808c35705dcb79201d24f7ada49d877dd10b826c43fbb6d84df'],
  frozenTwo: ['src/Spyro.Editor.NativeWizardCompositionSmoke/source_two_wizard_resource_oracle.py', '3447e0cbcf5657223abc7bd5ba177c48f3c560fff6e2f1bafe885fb130191ae4'],
};
function requireHelpers() {
  for (const [name, [relative, expected]] of Object.entries(helpers))
    check(sha(fs.readFileSync(path.join(root, relative))) === expected, 'Reviewed helper unchanged: ' + name);
}
requireHelpers();

// Same-pass raw-sector identity precedes every local metadata/payload read. The
// imported helpers also require the full original before/after reconstruction;
// their returned original data is rebound to these indexed sectors below.
const fd = fs.openSync(source, 'r'), index = Buffer.alloc(SECTORS * 32);
function readExact(offset, length) {
  check(Number.isSafeInteger(offset) && Number.isSafeInteger(length) && offset >= 0 && length >= 0 && offset + length <= SIZE, 'Bounded physical extent');
  const out = Buffer.alloc(length);
  check(fs.readSync(fd, out, 0, length, offset) === length, 'Exact physical read');
  return out;
}
function fullIdentity(makeIndex) {
  check(fs.fstatSync(fd).size === SIZE, 'Original physical length');
  const h = crypto.createHash('sha256');
  for (let first = 0; first < SECTORS; first += 256) {
    const count = Math.min(256, SECTORS - first), bytes = readExact(first * 2352, count * 2352);
    h.update(bytes);
    if (makeIndex) for (let i = 0; i < count; i++)
      crypto.createHash('sha256').update(bytes.subarray(i * 2352, (i + 1) * 2352)).digest().copy(index, (first + i) * 32);
  }
  check(h.digest('hex') === SOURCE_SHA, 'Full original source identity');
  check(fs.readSync(fd, Buffer.alloc(1), 0, 1, SIZE) === 0 && fs.fstatSync(fd).size === SIZE, 'Actual original EOF and final length');
}
fullIdentity(true);
function rawSector(lba) {
  check(Number.isInteger(lba) && lba >= 0 && lba < SECTORS, 'Source LBA bound');
  const raw = readExact(lba * 2352, 2352);
  check(crypto.createHash('sha256').update(raw).digest().equals(index.subarray(lba * 32, (lba + 1) * 32)), 'Same-pass sector identity before use');
  return raw;
}
function logical(lba, offset, length) {
  check(Number.isInteger(offset) && offset >= 0 && Number.isInteger(length) && length >= 0, 'Logical range');
  const result = Buffer.alloc(length);
  for (let i = 0; i < length;) {
    const q = lba * 2048 + offset + i, n = Math.min(length - i, 2048 - q % 2048);
    rawSector(Math.floor(q / 2048)).copy(result, i, 24 + q % 2048, 24 + q % 2048 + n); i += n;
  }
  return result;
}

// Independently replay the original-source append, not a hidden function-local
// extraction from the frozen two-owner oracle and not its reported C# hashes.
// Only immutable byte owners cross this subprocess boundary. stdout is a pipe,
// not a payload file; exact lengths and indexed original owners are checked.
const python = String.raw`
import contextlib, hashlib, importlib.util, io, runpy, struct, sys
sys.dont_write_bytecode=True
assert __debug__
with contextlib.redirect_stdout(io.StringIO()):
    sys.argv=['src/Spyro.Editor.NativeResourceCompositionSmoke/source_composition_oracle.py',sys.argv[1]]
    prior=runpy.run_path(sys.argv[0])
spec=importlib.util.spec_from_file_location('raw_append','src/Spyro.Editor.NativeWizardAppendSmoke/source_wizard_append_oracle.py')
raw=importlib.util.module_from_spec(spec); spec.loader.exec_module(raw)
original,donor,shared=map(bytes,(prior['original'],prior['donor'],prior['with_particle']))
u=lambda b,o:struct.unpack_from('<I',b,o)[0]
def put(b,o,v):struct.pack_into('<I',b,o,v)
start,length=struct.unpack_from('<II',original,24)
scene=original[start:start+length]
donor_start,donor_length=struct.unpack_from('<II',donor,24)
donor_scene=donor[donor_start:donor_start+donor_length]
moby,count,props,end,fixups,fixup_end,fields=raw.scene_layout(scene)
d=raw.scene_layout(donor_scene,require_zero_tail=False)
row_at=d[0]+8+6*0x58; private_at=u(donor_scene,row_at)
row=donor_scene[row_at:row_at+0x58]; private=donor_scene[private_at:private_at+0x50]
assert (moby,count,props,end,fixups,len(fields))==(0x8C30,128,0xE038,0xEED4,0x10EF0,147)
assert (row_at,private_at)==(0x6388,0xC350)
assert [f for f in d[-1] if row_at<=f<row_at+len(row)]==[row_at]
assert [f for f in d[-1] if private_at<=f<private_at+len(private)]==[private_at]
assert hashlib.sha256(row).hexdigest()=='59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82'
assert hashlib.sha256(private).hexdigest()=='5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b'
assert u(private,0x38)-u(row,20)==287 and row[0x48:0x4A]==bytes(2) and row[0x53]==0x56
final_start,final_length=struct.unpack_from('<II',shared,24)
assert final_length==length==0x11800 and final_start==start+0x13000
intents=[(92832,25248,10445,91808,25248),(86688,25248,10445,87712,25248)]
outputs=[]
for n in (1,2):
    after=bytearray(scene); delta=n*len(private)
    after[end+delta:fixups+delta]=scene[end:fixups]
    put(after,moby+4,count+n); put(after,props,u(scene,props)+delta)
    ff=list(fields)
    for i in range(n):
        r=moby+8+(count+i)*0x58; p=end+i*len(private)
        assert not any(scene[r:r+0x58])
        after[r:r+0x58]=row; after[p:p+0x50]=private
        put(after,r,p); put(after,p,p+0x28); ff.extend((r,p))
    put(after,fixups+delta,len(ff))
    for i,f in enumerate(ff):put(after,fixups+delta+4+4*i,f)
    assert raw.scene_layout(after)[-1]==ff
    if n==1:assert shared[final_start:final_start+length]==after
    append=bytes(after); patches=[]
    for i,(x,y,z,ex,ey) in enumerate(intents[:n]):
        r=moby+8+(count+i)*0x58; p=end+i*len(private)
        for at,value in [(r+12,x),(r+16,y),(r+20,z),(p+0x30,x),(p+0x34,y),(p+0x38,z+287),(p+0x40,ex),(p+0x44,ey),(p+0x48,z+287)]:
            patches.append((at,u(after,at)));put(after,at,value)
    inverse=after.copy()
    for at,value in patches:put(inverse,at,value)
    assert inverse==append and len(set(at for at,_ in patches))==9*n
    assert after[moby+8:moby+8+count*0x58]==scene[moby+8:moby+8+count*0x58]
    assert after[props+4:end]==scene[props+4:end] and after[end+delta:fixups+delta]==scene[end:fixups]
    output=shared[:final_start]+bytes(after)+shared[final_start+length:]
    assert len(output)==len(shared) and raw.scene_layout(after)[1]==128+n
    outputs.append(output)
for data in (original,donor,*outputs):sys.stdout.buffer.write(data)
`;
const run = spawnSync('python3', ['-c', python, source], {cwd: root, maxBuffer: 0x1000000, timeout: 120000});
check(run.status === 0 && !run.error && run.stderr.length === 0, 'Raw resource reconstruction: ' + run.stderr);
const lengths = [0x28D800, 0x28A000, 0x2A0800, 0x2A0800];
check(run.stdout.length === lengths.reduce((a, b) => a + b, 0), 'Exact returned immutable owner sizes');
let at = 0;
const [originalData, donorData, oneData, twoData] = lengths.map(n => {const b = Buffer.from(run.stdout.subarray(at, at + n)); at += n; return b;});
check(originalData.equals(logical(37, 0xF01000, originalData.length)), 'Source data bound to indexed original');
check(donorData.equals(logical(37, 0x2F6B000, donorData.length)), 'Donor data bound to indexed original');
check(sha(oneData) === '6ed74dbce04d6712e0d9ac12cfd05ae6ac334e0ec39fb7420f2105076132e537', 'Prior one-owner complete data golden');
check(sha(twoData) === '375b9dc41a76d507ded9ffdf442e4febbb1916e68bb5d4597477bb9031e2fd90', 'Frozen independently derived two-owner data golden');
const log = console.log;
let overlayBytes, executableBytes;
try {
  console.log = () => {}; // Existing overlay CLI is unchanged; this consumer uses its explicit exports.
  ({overlayBytes, executableBytes} = await import(pathToFileURL(path.join(root, helpers.overlay[0]))));
} finally { console.log = log; }
const originalExe = logical(53875, 0, 0x66000), originalOverlay = logical(37, 0xEF6000, 0xB000);
check(sha(originalExe) === 'a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998', 'Indexed original EXE');
check(sha(originalOverlay) === '9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32', 'Indexed original overlay');
check(sha(overlayBytes) === '10f82ee1028a9ad4fa9045d948a88e76545847031b954ac08ad0e74e6e9c7083' && overlayBytes.length === 0xC800, 'Shared raw overlay');
const copyInverse = Buffer.from(executableBytes);
originalExe.copy(copyInverse, 0x4B048, 0x4B048, 0x4B050);
check(copyInverse.equals(originalExe), 'Only source-proven copy-buffer instruction pair');
const exes = [110, 120].map(target => {
  const b = Buffer.from(executableBytes); check(b.readUInt16LE(0x5FC3C) === 100, 'Original target100');
  b.writeUInt16LE(target, 0x5FC3C); return b;
});
check(sha(exes[0]) === 'ec1e8ae38fed404f9a28ccc7fbf17d81e92a341aab89906e94c5e6c793f4c3b4', 'Legacy reward110 EXE');
check(sha(exes[1]) === 'ce9c99ba467b089603850989bd141f374a7f86cf8e4deeab3fea1a3fc30164d1', 'Independent reward120 EXE');

const pvd = logical(16, 0, 2048);
check(pvd.subarray(0, 7).equals(Buffer.from('01434430303101', 'hex')), 'Primary ISO descriptor');
function both32(b, p) {check(b.readUInt32LE(p) === b.readUInt32BE(p + 4), 'ISO both-endian equality'); return b.readUInt32LE(p);}
const rootLba = both32(pvd, 158), rootLength = both32(pvd, 166);
check(rootLba === 22 && rootLength === 2048, 'Exact source ISO root owner');
const originalRoot = logical(rootLba, 0, rootLength), rows = new Map();
for (let p = 0; p < originalRoot.length && originalRoot[p];) {
  const n = originalRoot[p], nameLength = originalRoot[p + 32];
  check(n >= 34 && p + n <= originalRoot.length && 33 + nameLength <= n, 'Bounded ISO row before parse');
  const name = originalRoot.subarray(p + 33, p + 33 + nameLength).toString('ascii').replace(/;1$/, '');
  check(!rows.has(name), 'Unique ISO file owner');
  rows.set(name, {at: p, lba: both32(originalRoot, p + 2), length: both32(originalRoot, p + 10)}); p += n;
}
check(rows.get('WAD.WAD').lba === 37 && rows.get('WAD.WAD').length === 0x6927000 &&
      rows.get('SCUS_942.28').lba === 53875 && rows.get('SCUS_942.28').length === 0x66000 && rows.get('PETEXA0.STR').lba === 60000, 'Exact original physical file owners');
const header = logical(37, 0, 2048), entries = [];
for (let i = 0; i < 256; i++) {
  const offset = header.readUInt32LE(i * 8), length = header.readUInt32LE(i * 8 + 4);
  if (offset || length) {check(offset >= 2048 && length > 0 && !(offset % 2048) && !(length % 2048), 'Whole-sector WAD owner'); entries.push({index: i, offset, length});}
}
entries.sort((a, b) => a.offset - b.offset);
const finalHeader = Buffer.from(header), finalRoot = Buffer.from(originalRoot), originalWadHash = crypto.createHash('sha256');
originalWadHash.update(header);
let oldCursor = 2048, cursor = 2048;
for (const e of entries) {
  check(e.offset === oldCursor && e.offset + e.length <= rows.get('WAD.WAD').length, 'Complete original packed WAD partition');
  e.before = logical(37, e.offset, e.length); originalWadHash.update(e.before);
  e.after = e.index === 13 ? overlayBytes : e.index === 14 ? oneData : e.before;
  e.finalOffset = cursor; e.finalLength = e.after.length;
  finalHeader.writeUInt32LE(cursor, e.index * 8); finalHeader.writeUInt32LE(e.finalLength, e.index * 8 + 4);
  cursor += e.finalLength; oldCursor += e.length;
}
check(oldCursor === 0x6927000 && cursor - oldCursor === 41 * 2048, 'Source-derived 41-sector WAD growth');
check(entries.find(e => e.index === 13).before.equals(originalOverlay) && entries.find(e => e.index === 14).before.equals(originalData), 'Exact replacement source ownership');
const exeLba = 37 + cursor / 2048, exeEndLba = exeLba + exes[0].length / 2048;
check(exeLba === 53916 && exeEndLba === 54120 && exeEndLba < rows.get('PETEXA0.STR').lba, 'Relocated EXE bounded by next file');
for (const [name, lba, length] of [['WAD.WAD', 37, cursor], ['SCUS_942.28', exeLba, exes[0].length]])
  for (const [field, value] of [[2, lba], [10, length]]) {
    const p = rows.get(name).at + field; finalRoot.writeUInt32LE(value, p); finalRoot.writeUInt32BE(value, p + 4);
  }
const rootInverse = Buffer.from(finalRoot), headerInverse = Buffer.from(finalHeader);
for (const name of ['WAD.WAD', 'SCUS_942.28']) {const e = rows.get(name); for (const field of [2, 10]) originalRoot.copy(rootInverse, e.at + field, e.at + field, e.at + field + 8);}
for (const e of entries) {headerInverse.writeUInt32LE(e.offset, e.index * 8); headerInverse.writeUInt32LE(e.length, e.index * 8 + 4);}
check(rootInverse.equals(originalRoot) && headerInverse.equals(header), 'Exact metadata inverse/complement');
const wadHashes = [oneData, twoData].map(data => {
  const h = crypto.createHash('sha256'); h.update(finalHeader);
  for (const e of entries) h.update(e.index === 14 ? data : e.after); return h.digest('hex');
});
// Derived by the first original-only run, retained in the local first-run JSON;
// none of these physical goldens came from the new C# candidate implementation.
check(wadHashes[0] === 'd07c91d13d9a7653a3708afd5d6de9e0279e92b495883d519e967b618e83d1d3' &&
      wadHashes[1] === '4e7c47911822087eb30e531b8491b7548cfdf18d069e0a24a12e8711be971a94', 'Independently derived complete WAD goldens');
check(sha(finalRoot) === '7a881ab875392b331f65dc294368ebc6963998b5c24632acf2f63f3bff0cc6a0' &&
      sha(finalHeader) === 'c02eddf8b2b2185b5027d6429deb24ee9db88fd94bd14b8401a9d664ce278411', 'Independently derived ISO root and packed WAD header');

// Native CD-ROM Mode2 Form1 parity, independently ported from the reviewed raw
// Python oracle; input address bytes are zero for ECC only, not on-disc MSF.
const edcTable = new Uint32Array(256), forward = new Uint8Array(256), backward = new Uint8Array(256);
for (let i = 0; i < 256; i++) {
  let n = i; for (let bit = 0; bit < 8; bit++) n = ((n >>> 1) ^ (n & 1 ? 0xD8018001 : 0)) >>> 0;
  edcTable[i] = n; const f = (i << 1) ^ (i & 128 ? 0x11D : 0); forward[i] = f; backward[i ^ f] = i;
}
function edc(bytes) {let v = 0; for (const b of bytes) v = ((v >>> 8) ^ edcTable[(v ^ b) & 255]) >>> 0; return v;}
function ecc(bytes, majorCount, minorCount, majorMult, minorInc) {
  const size = majorCount * minorCount, out = Buffer.alloc(majorCount * 2);
  check(bytes.length === size, 'ECC input domain');
  for (let major = 0; major < majorCount; major++) {
    let p = (major >>> 1) * majorMult + (major & 1), a = 0, b = 0;
    for (let minor = 0; minor < minorCount; minor++) {const x = bytes[p]; p = (p + minorInc) % size; a ^= x; b ^= x; a = forward[a];}
    a = backward[forward[a] ^ b]; out[major] = a; out[major + majorCount] = a ^ b;
  }
  return out;
}
const bcd = n => Math.floor(n / 10) * 16 + n % 10;
function requireAddress(raw, lba) {
  check(raw.subarray(0, 12).equals(Buffer.from('00ffffffffffffffffffff00', 'hex')) &&
    raw.subarray(12, 16).equals(Buffer.from([bcd(Math.floor((lba + 150) / 4500)), bcd(Math.floor((lba + 150) / 75) % 60), bcd((lba + 150) % 75), 2])), 'Destination LBA/MSF/sync/Mode2');
}
function form1(raw, payload, terminal) {
  check(payload.length === 2048 && raw.length === 2352, 'Physical owner sizes');
  const out = Buffer.from(raw);
  Buffer.from([0, 0, terminal ? 0x89 : 8, 0, 0, 0, terminal ? 0x89 : 8, 0]).copy(out, 16);
  payload.copy(out, 24); out.writeUInt32LE(edc(out.subarray(16, 2072)), 2072);
  const zeroAddress = Buffer.concat([Buffer.alloc(4), out.subarray(16, 2076)]);
  ecc(zeroAddress, 86, 24, 2, 86).copy(out, 2076);
  ecc(Buffer.concat([zeroAddress, out.subarray(2076, 2248)]), 52, 43, 86, 88).copy(out, 2248);
  check(out.subarray(0, 16).equals(raw.subarray(0, 16)), 'Destination MSF retained, never copied from moved EXE');
  return out;
}
const whole = [crypto.createHash('sha256'), crypto.createHash('sha256')], inverse = [crypto.createHash('sha256'), crypto.createHash('sha256')];
const changes = [[], []], pairChanges = [], boundaries = [], gapHash = crypto.createHash('sha256');
let ownerIndex = 0, parityReproduced = 0, gapSectors = 0;
for (let lba = 0; lba < SECTORS; lba++) {
  const before = rawSector(lba);
  let payloads = null;
  if (lba === rootLba) payloads = [finalRoot, finalRoot];
  else if (lba >= 37 && lba < exeLba) {
    const offset = (lba - 37) * 2048;
    if (!offset) payloads = [finalHeader, finalHeader];
    else {
      while (offset >= entries[ownerIndex].finalOffset + entries[ownerIndex].finalLength) ownerIndex++;
      const e = entries[ownerIndex], p = offset - e.finalOffset;
      check(p >= 0 && p + 2048 <= e.finalLength, 'Final WAD sector belongs to exactly one complete owner');
      payloads = e.index === 14 ? [oneData.subarray(p, p + 2048), twoData.subarray(p, p + 2048)] : [e.after.subarray(p, p + 2048), e.after.subarray(p, p + 2048)];
    }
  } else if (lba >= exeLba && lba < exeEndLba) {
    const p = (lba - exeLba) * 2048; payloads = exes.map(exe => exe.subarray(p, p + 2048));
  }
  let after = [before, before];
  if (payloads) {
    requireAddress(before, lba);
    check(before.subarray(16, 20).equals(before.subarray(20, 24)), 'Original duplicated XA subheader');
    if (lba < 54079) {
      check(before[16] === 0 && before[17] === 0 && before[19] === 0 && [8, 0x89].includes(before[18]), 'Exact source Form1 XA identity');
      check(form1(before, before.subarray(24, 2072), before[18] === 0x89).equals(before), 'Independent EDC/P/Q reproduce original raw Form1'); parityReproduced++;
    } else {
      check(lba < exeEndLba && before.subarray(16, 24).equals(Buffer.from('0000200000002000', 'hex')) && before.subarray(24, 2348).equals(Buffer.alloc(2324)) && before.readUInt32LE(2348) === edc(before.subarray(16, 2348)), 'Canonical original Form2 gap including its EDC');
      for (const [name, e] of rows) if (name !== '\x00' && name !== '\x01') check(lba < e.lba || lba >= e.lba + Math.ceil(e.length / 2048), 'Consumed gap is not a source ISO owner');
      gapHash.update(before); gapSectors++;
    }
    const terminal = lba === rootLba || lba === exeLba - 1 || lba === exeEndLba - 1;
    // Exact identity equality permits retaining original bytes after parity was
    // independently reproduced; this avoids pointless parity work for prefixes.
    after = payloads.map(payload => payload.equals(before.subarray(24, 2072)) && before[18] === (terminal ? 0x89 : 8) ? before : form1(before, payload, terminal));
    for (const b of after) check(b.subarray(16, 24).equals(Buffer.from([0, 0, terminal ? 0x89 : 8, 0, 0, 0, terminal ? 0x89 : 8, 0])), 'Final terminal ownership');
  }
  if (!after[0].equals(after[1])) pairChanges.push(lba);
  for (let n = 0; n < 2; n++) {
    whole[n].update(after[n]);
    // Original raw preimages are the inverse physical owners, including Form2
    // gap and old EXE addresses. This is expected-afterimage construction, not
    // an independent readback of a C#-written candidate (none is opened here).
    check(after[n].length === before.length, 'Physical inverse owner size');
    inverse[n].update(before);
    if (!after[n].equals(before)) changes[n].push(lba);
  }
  if ([53874, 53915, 54078, 54119].includes(lba)) boundaries.push({lba, beforeFlags: before[18], afterFlags: after[1][18], beforeSha256: sha(before), afterSha256: sha(after[1]), destinationMsf: after[1].subarray(12, 16).toString('hex')});
}
const imageHashes = whole.map(h => h.digest('hex')), inverseHashes = inverse.map(h => h.digest('hex'));
check(imageHashes[0] === '2a87dc12f9398ef873267e0f17792cfe160952c9f1bf12c137f64029863a39db', 'Full physical legacy one-Wizard golden independently reproduced');
check(imageHashes[1] === '612e68e1a8642d42f2417fd6153368c9549094ac391a3835c9cf6dfee7808cba', 'First-run independent two-Wizard full physical golden');
check(inverseHashes.every(h => h === SOURCE_SHA), 'Complete raw physical inverse source identities');
check(boundaries.map(b => b.afterFlags).join(',') === '8,137,8,137' && gapSectors === 41, 'All four EOF/EOR transfers and original Form2 gap extent');
check(changes.every(c => c[0] === 22 && c.at(-1) === 54119), 'First/last full-image changed sectors');
check(changes.every(c => c.length === 46341) && pairChanges.join(',') === '8585,8591,8596,8597,8601,8602,54107', 'Independent exact original/pair changed-sector counts');
requireHelpers(); fullIdentity(false); fs.closeSync(fd);
function ranges(values) {
  const result = [];
  for (const v of values) {const last = result.at(-1); if (last && last.endExclusive === v) last.endExclusive++; else result.push({start: v, endExclusive: v + 1});}
  return result;
}
console.log(JSON.stringify({marker: 'NATIVE_TWO_WIZARD_DISC_RAW_ORACLE_OK', checks,
  sourceImageSha256: SOURCE_SHA, physicalByteLength: SIZE, sourceSectors: SECTORS,
  helperPins: helpers, sourceUnchanged: true, originalWadSha256: originalWadHash.digest('hex'),
  sourceRootSha256: sha(originalRoot), finalRootSha256: sha(finalRoot), sourceHeaderSha256: sha(header), finalHeaderSha256: sha(finalHeader),
  finalWadLength: cursor, wadGrowth: cursor - oldCursor, growthSectors: (cursor - oldCursor) / 2048, exeLba, exeEndLba,
  replacementOwners: entries.filter(e => [13, 14].includes(e.index)).map(e => ({index: e.index, originalOffset: e.offset, originalLength: e.length, finalOffset: e.finalOffset, finalLength: e.finalLength, beforeSha256: sha(e.before)})),
  outputs: [oneData, twoData].map((data, n) => ({familyCount: n + 1, profile: n ? 'native-dark-two-wizards-dry-corridors-research-v1' : 'legacy-single-control',
    dataSha256: sha(data), overlaySha256: sha(overlayBytes), executableSha256: sha(exes[n]), treasureTarget: 110 + n * 10,
    wholeWadSha256: wadHashes[n], wholeImageSha256: imageHashes[n], inverseSourceSha256: inverseHashes[n],
    changedSectorCount: changes[n].length, changedSectorRanges: ranges(changes[n])})),
  oneVersusTwoChangedSectorCount: pairChanges.length, oneVersusTwoChangedSectorRanges: ranges(pairChanges),
  parityReproducedOriginalForm1Sectors: parityReproduced, canonicalForm2GapSectors: gapSectors, consumedRawGapSha256: gapHash.digest('hex'), boundaries,
  explicitLimits: ['No candidate file or Core/DLL is read; these are independently constructed expected bytes, not production readback',
    'Full raw inverse uses authenticated original-sector preimages, not reconstructed logical-only Form1 for the original gap',
    'The frozen 229-check oracle owns the separate visible-list/child-guard scalar evidence; this script does not duplicate or extend it',
    'Temporal family labels are external fixture identities, not accepted project/card history or native tombstone proof',
    'No CUE/receipt/publication transaction, emulator, rendering, audio or native gameplay acceptance is exercised'],
  candidateFilesWritten: 0, nativeEmulatorExecution: false, runtimeApproved: false, publicationApproved: false}, null, 2));
