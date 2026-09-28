using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

int checks = 0;
void Check(bool yes, string message) { checks++; if (!yes) throw new Exception(message); }
void Refuse(Action action)
{ try { action(); } catch (Exception e) when (e is InvalidDataException or ArgumentException) { checks++; return; } throw new Exception("Expected compile refusal"); }
Guid Id(int i) => Guid.Parse(i.ToString("x32"));
DetachedCollectibleManifest Manifest(int n) => DetachedCollectibleManifest.CreateNew(Id(9999), Enumerable.Range(1,n).Select(Id));
const uint Code = 0x800D0000, HashCode = 0x800E0000, Input = 0x80100000, Shadow = 0x80100800,
    Context = 0x80100A40, Digest = 0x80100A80, Workspace = 0x80100B00, Stack = 0x801FF000, Return = 0x80010000;
var domain = new NativeAddressSpace(NativeAddressKind.LevelData, "test-only-envelope-load", NativeAddressRevision.Final, 0x20000);
var hash = NativeSha256Kernel.Compile(new(domain,0x10000,0x8000),HashCode,Input,1024,Digest,Workspace);
NativeCollectibleEnvelopeLoadKernel Compile(DetachedCollectibleManifest m, int slot = 1, uint shadow = Shadow, uint context = Context) =>
    NativeCollectibleEnvelopeLoadKernel.Compile(m,slot,new(domain,0,0x8000),Code,shadow,context,hash);
byte[] Snapshot(RenderProbeCpu cpu,uint at,int count) => Enumerable.Range(0,count).Select(i=>(byte)cpu.Get(at+(uint)i,1)).ToArray();
RenderProbeCpu Load(NativeCollectibleEnvelopeLoadKernel kernel, byte[] envelope, uint controller=1, uint marker=0xABCD, uint epoch=0xFFFFFFFF)
{
    var cpu = new RenderProbeCpu { StrictSignedArithmetic=true };
    cpu.Copy(Code,kernel.CopyBytes()); cpu.Copy(HashCode,hash.CopyBytes());
    cpu.Copy(Input-16,Enumerable.Repeat((byte)0xA5,1056).ToArray()); cpu.Copy(Input,envelope);
    cpu.Copy(Shadow-16,Enumerable.Repeat((byte)0xA6,544).ToArray());
    cpu.Copy(Digest-4,Enumerable.Repeat((byte)0xB7,40).ToArray());
    cpu.Copy(Workspace-16,Enumerable.Repeat((byte)0xC8,hash.WorkspaceByteLength+32).ToArray());
    cpu.Copy(Stack-(uint)kernel.MaximumStackByteLength-16,Enumerable.Repeat((byte)0xD9,kernel.MaximumStackByteLength+32).ToArray());
    cpu.Copy(0x801FF600,Enumerable.Repeat((byte)0xEC,512).ToArray());
    cpu.Put(Context,(uint)kernel.OrdinarySlot); cpu.Put(Context+4,controller); cpu.Put(Context+8,marker); cpu.Put(Context+12,epoch);
    for(int r=1;r<32;r++) cpu.Regs[r]=0xB1000000u+(uint)r;
    cpu.Regs[4]=Input; cpu.Regs[5]=(uint)envelope.Length; cpu.Regs[6]=Context;
    cpu.Regs[29]=Stack; cpu.Regs[31]=Return; cpu.Hi=0x12345678; cpu.Lo=0x89ABCDEF;
    return cpu;
}
void Run(NativeCollectibleEnvelopeLoadKernel kernel, byte[] envelope, byte[]? expected, Action<RenderProbeCpu>? mutate=null,
    NativeCollectibleEnvelopeLoadResult? refusal=null)
{
    var cpu=Load(kernel,envelope); mutate?.Invoke(cpu);
    var before=cpu.Regs.ToArray(); byte[] source=Snapshot(cpu,Input,1024), tuple=Snapshot(cpu,Context,16), shadow=Snapshot(cpu,Shadow,512);
    cpu.Run(Code,Return);
    Check(expected is null ? cpu.Regs[2]!=0 : cpu.Regs[2]==0,"Preparation result matches acceptance/refusal.");
    if(refusal is not null) Check(cpu.Regs[2]==(uint)refusal,"Exact typed refusal distinguishes arguments, header, unused bits and checksum failure.");
    Check(Snapshot(cpu,Input,1024).SequenceEqual(source)&&Snapshot(cpu,Context,16).SequenceEqual(tuple),"Complete input and selection context stay immutable.");
    Check(Snapshot(cpu,Shadow,512).SequenceEqual(expected??shadow),"Success matches all512 host-codec ABI bytes; refusal preserves the entire existing candidate.");
    Check(Snapshot(cpu,0x801FF600,512).All(b=>b==0xEC),"Active collectible reservation is never modified.");
    Check(new[]{4,5,6,7,16,17,18,19,20,21,22,23,26,27,28,29,30,31}.All(r=>cpu.Regs[r]==before[r])&&cpu.Hi==0x12345678&&cpu.Lo==0x89ABCDEF,"Arguments, preserved GPRs, SP/RA and HI/LO survive every result.");
    bool In(uint a,uint start,int count)=>(ulong)a>=start&&(ulong)a<start+(ulong)count;
    Check(cpu.Writes.All(a=>In(a,Shadow,512)||In(a,Digest,32)||In(a,Workspace,hash.WorkspaceByteLength)||In(a,before[29]-(uint)kernel.MaximumStackByteLength,kernel.MaximumStackByteLength)),"Every write belongs to shadow, digest, hash workspace or the declared nested stack.");
    Check(!cpu.Reads.Any(a=>In(a,Input+(uint)envelope.Length,1024-envelope.Length)),"Hash and loader never read beyond the exact envelope.");
    Check(cpu.Get(Shadow-4)==0xA6A6A6A6&&cpu.Get(Shadow+512)==0xA6A6A6A6&&cpu.Get(Digest-4)==0xB7B7B7B7&&cpu.Get(Digest+32)==0xB7B7B7B7&&
        cpu.Get(Workspace-4)==0xC8C8C8C8&&cpu.Get(Workspace+(uint)hash.WorkspaceByteLength)==0xC8C8C8C8,"Shadow, digest and workspace canaries survive.");
    Check(cpu.Get(Stack-(uint)kernel.MaximumStackByteLength-4)==0xD9D9D9D9&&cpu.Get(Stack)==0xD9D9D9D9,"Both complete nested-stack canaries survive.");
    if(expected is not null) Check(cpu.Writes[^1]==Shadow+0x16,"Success publishes the prepared phase byte in shadow last, never to the active ledger.");
}
byte[] Expected(DetachedCollectibleManifest m, DetachedCollectibleSave s,int slot=1,uint controller=1,uint marker=0xABCD,uint epoch=0xFFFFFFFF) =>
    DetachedCollectibleLedgerImage.Hydrate(NativeCollectibleSelectedSlotResidence.Describe(m,slot),m,DetachedCollectibleSaveCodec.Serialize(s),(int)controller,marker,epoch).CopyBytes();
foreach(int count in new[]{1,7,8,9,127,128,129,255,256,257,1408})
{
    var m=Manifest(count); var state=DetachedCollectibleSave.CreateNew(m).WithCollected(0,Id(1)).WithCollected(1,Id(count)).WithCollected(2,Id(1));
    for(int slot=0;slot<3;slot++)
    {
        var k=Compile(m,slot); byte[] bytes=DetachedCollectibleSaveCodec.Serialize(state);
        Check(k.ManifestSha256==m.Sha256&&k.EnvelopeByteLength==bytes.Length&&!k.NativeResidenceIssued&&!k.NativeBindingValidated&&!k.PublicationApproved,"Exact schema/manifest binding is not a native admission certificate.");
        Run(k,bytes,Expected(m,state,slot));
    }
}
var manifest=Manifest(257); var save=DetachedCollectibleSave.CreateNew(manifest).WithCollected(1,Id(256)).WithCollected(2,Id(257));
var kernel=Compile(manifest); var envelope=DetachedCollectibleSaveCodec.Serialize(save);
// Every header/body/checksum byte can reject independently. No host-inspected
// success flag is passed to the emitted code; every SHA round executes normally.
for(int at=0;at<envelope.Length;at++)
{ byte[] bad=envelope.ToArray(); bad[at]^=1; Run(kernel,bad,null,refusal:at<96?NativeCollectibleEnvelopeLoadResult.HeaderMismatch:NativeCollectibleEnvelopeLoadResult.ChecksumMismatch); }
foreach(int slot in Enumerable.Range(0,3))
{
    byte[] bad=envelope.ToArray(); bad[96+slot*33+32]|=0x80; DetachedCollectibleBinary.WriteChecksum(bad); Run(kernel,bad,null,refusal:NativeCollectibleEnvelopeLoadResult.InvalidBits);
}
for(int at=0;at<96;at++)
{ byte[] bad=envelope.ToArray(); bad[at]^=1; DetachedCollectibleBinary.WriteChecksum(bad); Run(kernel,bad,null,refusal:NativeCollectibleEnvelopeLoadResult.HeaderMismatch); }
foreach(uint controller in new uint[]{0,1}) foreach(uint marker in new uint[]{0,0xFFFF}) foreach(uint epoch in new uint[]{0,uint.MaxValue})
    Run(kernel,envelope,Expected(manifest,save,controller:controller,marker:marker,epoch:epoch),c=>{c.Put(Context+4,controller);c.Put(Context+8,marker);c.Put(Context+12,epoch);});
int guardIndex=0;
foreach(var mutate in new Action<RenderProbeCpu>[] { c=>c.Regs[4]++,c=>c.Regs[5]--,c=>c.Regs[5]++,c=>c.Regs[5]=uint.MaxValue,
    c=>c.Regs[6]++,c=>c.Put(Context,0),c=>c.Put(Context+4,2),c=>c.Put(Context+4,16),c=>c.Put(Context+8,0x10000),
    c=>c.Regs[29]=Shadow+128,c=>c.Regs[29]=Context+16,c=>c.Regs[29]=Digest+32,c=>c.Regs[29]=Workspace+32,
    c=>c.Regs[29]=Input+32,c=>c.Regs[29]=Code+32,c=>c.Regs[29]=HashCode+32,c=>c.Regs[29]=0x801FF700,c=>c.Regs[29]=1,c=>c.Regs[29]=0x80200008,
    c=>c.Regs[29]=0x801FF840,c=>c.Regs[29]=Shadow+0x230 })
{
    var c=Load(kernel,envelope); mutate(c); c.Run(Code,Return); Check(c.Regs[2]==(uint)NativeCollectibleEnvelopeLoadResult.InvalidArguments&&c.Writes.Count==0,"Bad pointers, tuple bounds or whole nested stack refuse before any write.");
    if(guardIndex<5||guardIndex>=9) Check(c.Reads.Count==0,"Pointer and complete nested-stack refusal happens before any data read.");
    guardIndex++;
}
var retired=manifest.Advance([],new[]{Id(256)}); var migrated=save.MigrateTo(retired); Run(Compile(retired),DetachedCollectibleSaveCodec.Serialize(migrated),Expected(retired,migrated));
var allRetired=manifest.Advance([],manifest.Entries.Select(e=>e.InstanceId)); var history=save.MigrateTo(allRetired);
Run(Compile(allRetired),DetachedCollectibleSaveCodec.Serialize(history),Expected(allRetired,history));
var denseManifest=Manifest(1408); var dense=DetachedCollectibleSave.FromValidatedBits(denseManifest,Enumerable.Repeat((byte)255,3*176).ToArray());
Run(Compile(denseManifest),DetachedCollectibleSaveCodec.Serialize(dense),Expected(denseManifest,dense));
foreach(int count in new[]{0,1409,4096}) Refuse(()=>Compile(Manifest(count)));
foreach(int slot in new[]{-1,3}) Refuse(()=>Compile(manifest,slot));
foreach(uint shadow in new uint[]{Input,Context,Digest,Workspace,Code,HashCode,Shadow+1,0x801FF600,0x801FF500,0x801FFFF0,0x7FFFFFF0}) Refuse(()=>Compile(manifest,shadow:shadow));
foreach(uint ctx in new uint[]{Input,Shadow,Digest,Workspace,Code,HashCode,Context+1,0x801FF600,0x801FFFF8}) Refuse(()=>Compile(manifest,context:ctx));
byte[] mutable=kernel.CopyBytes(); mutable[0]^=1; Check(!mutable.SequenceEqual(kernel.CopyBytes()),"Returned code does not mutate the kernel.");
Console.WriteLine($"NATIVE_COLLECTIBLE_ENVELOPE_LOAD_OK checks={checks} codeBytes={kernel.CopyBytes().Length} nestedStackBytes={kernel.MaximumStackByteLength} nativeReady=false cardIo=false publication=false");
