using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Analysis;

internal sealed record NativeMipsBehaviorFragmentSource(string Id,uint SourcePc,NativeMobyDependencyRead Read);
internal sealed record NativeMipsBehaviorExternalBinding(uint SourcePc,uint? ExpectedTargetPc,uint TargetPc,NativeByteExtent Target,
    NativeMobyDependencyRead? Evidence=null,uint? SourceRuntimeBase=null);
internal enum NativeMipsBehaviorTransferKind { Branch16,Jump26,Call26,CallbackRegister,ReturnRegister,Fallthrough }
internal sealed record NativeMipsBehaviorTransfer(uint SourcePc,uint? TargetPc,bool HasDelaySlot,
    NativeMipsBehaviorTransferKind Kind=NativeMipsBehaviorTransferKind.Jump26,uint SourceWord=0,uint DelayWord=0);
internal sealed record NativeMipsBehaviorStackAccess(uint SourcePc,int Offset,int ByteWidth,bool IsLoad);
internal sealed class NativeMipsCapturedBehaviorFragment
{
    private readonly byte[] _bytes;
    internal NativeMipsCapturedBehaviorFragment(NativeMipsBehaviorFragmentSource source,byte[] bytes,bool fallthrough)
    {Source=source;_bytes=bytes.ToArray();HasFallthrough=fallthrough;}
    public NativeMipsBehaviorFragmentSource Source { get; }
    public int ByteLength=>_bytes.Length;
    public bool HasFallthrough { get; }
    public byte[] CopyBytes()=>_bytes.ToArray();
}
internal sealed class NativeMipsBehaviorLinkedCode
{
    private readonly byte[] _bytes;
    internal NativeMipsBehaviorLinkedCode(byte[] bytes,IReadOnlyDictionary<uint,uint> mapping,IEnumerable<NativeByteExtent> reservations)
    {_bytes=bytes.ToArray();SourceToFinalPc=new System.Collections.ObjectModel.ReadOnlyDictionary<uint,uint>(new Dictionary<uint,uint>(mapping));
     UnmaterializedExternalTargets=Array.AsReadOnly(reservations.Distinct().ToArray());}
    public byte[] CopyBytes()=>_bytes.ToArray();
    public IReadOnlyDictionary<uint,uint> SourceToFinalPc { get; }
    public IReadOnlyList<NativeByteExtent> UnmaterializedExternalTargets { get; }
    public bool CompleteInstalledCodeOwnerProven=>false;
}
internal sealed class NativeMipsBehaviorFragmentPackage
{
    private readonly Func<NativeByteExtent,byte[]> _reader;
    internal NativeMipsBehaviorFragmentPackage(IReadOnlyList<NativeMipsCapturedBehaviorFragment> fragments,
        IReadOnlyList<NativeMipsBehaviorTransfer> transfers,IReadOnlyList<NativeMipsBehaviorStackAccess> stack,
        IReadOnlyList<uint> entries,IReadOnlyList<uint> exits,Func<NativeByteExtent,byte[]> reader)
    {Fragments=Array.AsReadOnly(fragments.ToArray());Transfers=Array.AsReadOnly(transfers.ToArray());StackAccesses=Array.AsReadOnly(stack.ToArray());
     EntryPcs=Array.AsReadOnly(entries.ToArray());InterceptedExits=Array.AsReadOnly(exits.ToArray());_reader=reader;}
    public int InstructionCount=>Fragments.Sum(f=>f.ByteLength)/4;
    public IReadOnlyList<NativeMipsCapturedBehaviorFragment> Fragments { get; }
    public IReadOnlyList<NativeMipsBehaviorTransfer> Transfers { get; }
    public IReadOnlyList<NativeMipsBehaviorStackAccess> StackAccesses { get; }
    public IReadOnlyList<uint> EntryPcs { get; }
    public IReadOnlyList<uint> InterceptedExits { get; }
    public bool OwnsPc(uint pc)=>Fragments.Any(f=>pc>=f.Source.SourcePc&&(ulong)pc<f.Source.SourcePc+(ulong)f.ByteLength);

    /// <summary>Internal relocation only. The native issuer/integrator must own
    /// the reservation and semantic external bindings; no public address API.</summary>
    public NativeMipsBehaviorLinkedCode Link(NativeByteExtent owner,uint runtimeBase,IReadOnlyList<NativeMipsBehaviorExternalBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(owner);ArgumentNullException.ThrowIfNull(bindings);
        if(owner.Space.Revision!=NativeAddressRevision.Final||owner.Offset%4!=0||owner.ByteLength%4!=0||runtimeBase%4!=0||
            runtimeBase<0x80000000||(ulong)runtimeBase+(ulong)owner.ByteLength>0x80200000)
            throw new InvalidDataException("Behavior code requires one bounded aligned canonical final code domain.");
        var bindingSnapshot=NativeMipsBehaviorFragmentCodec.BoundedSnapshot(bindings,8192,"external binding");
        if(bindingSnapshot.Any(b=>b==null)||bindingSnapshot.Select(b=>b.SourcePc).Distinct().Count()!=bindingSnapshot.Length)
            throw new InvalidDataException("Behavior external binding budget or duplicate site is invalid.");
        var bound=bindingSnapshot.ToDictionary(b=>b.SourcePc);HashSet<uint> consumed=[];
        int length=Fragments.Sum(f=>f.ByteLength+(f.HasFallthrough?8:0));
        if(length>owner.ByteLength)throw new InvalidDataException("Owned behavior code capacity is exhausted.");
        byte[] output=new byte[length];Dictionary<uint,uint> mapped=[];Dictionary<uint,int> offsets=[];int cursor=0;
        foreach(var fragment in Fragments)
        {
            var read=fragment.Source.Read;byte[] current=_reader(read.Extent)??throw new InvalidDataException("Behavior source preimage is absent.");
            if(current.LongLength!=read.Extent.ByteLength||NativeMipsBehaviorFragmentCodec.Hash(current)!=read.ExpectedSha256)
                throw new InvalidDataException("Behavior source preimage changed before linking.");
            fragment.CopyBytes().CopyTo(output,cursor);
            for(int at=0;at<fragment.ByteLength;at+=4){mapped.Add(fragment.Source.SourcePc+(uint)at,runtimeBase+(uint)cursor+(uint)at);offsets.Add(fragment.Source.SourcePc+(uint)at,cursor+at);}
            if(fragment.HasFallthrough)offsets.Add(fragment.Source.SourcePc+(uint)fragment.ByteLength,cursor+fragment.ByteLength);
            cursor+=fragment.ByteLength+(fragment.HasFallthrough?8:0);
        }
        foreach(var transfer in Transfers)
        {
            if(transfer.Kind==NativeMipsBehaviorTransferKind.ReturnRegister)continue;
            uint target;
            if(transfer.TargetPc is uint sourceTarget&&mapped.TryGetValue(sourceTarget,out uint internalTarget))target=internalTarget;
            else
            {
                if(!bound.TryGetValue(transfer.SourcePc,out var binding))throw new InvalidDataException($"Unresolved native behavior {transfer.Kind} at {transfer.SourcePc:X8}.");
                if(binding.ExpectedTargetPc!=transfer.TargetPc)throw new InvalidDataException("Behavior external target preimage changed.");
                if(binding.Target==null||binding.Target.ByteLength<4||binding.Target.Offset%4!=0||binding.TargetPc%4!=0||binding.TargetPc<0x80000000||binding.TargetPc>=0x80200000)
                    throw new InvalidDataException("Behavior target lacks an aligned owned instruction.");
                if(binding.Target.Space.Revision==NativeAddressRevision.Final)
                {
                    if(binding.Target.Space!=owner.Space)throw new InvalidDataException("Behavior external target has another canonical final domain.");
                    if(binding.Target.Offset<owner.Offset||binding.Target.EndExclusive>owner.EndExclusive||
                        binding.TargetPc!=(ulong)runtimeBase+(ulong)(binding.Target.Offset-owner.Offset))
                        throw new InvalidDataException("Behavior final target runtime/address mapping is not its owned extent.");
                    if(binding.Target.Offset<owner.Offset+length&&binding.Target.EndExclusive>owner.Offset)
                        throw new InvalidDataException("Unmaterialized behavior target has generated code overlap; external reservations cannot own emitted instructions.");
                }
                else
                {
                    var evidence=binding.Evidence;
                    if(binding.TargetPc!=transfer.TargetPc||binding.Target.Space.Kind!=NativeAddressKind.ExecutableFile||evidence==null||evidence.Extent.Space!=binding.Target.Space||
                        binding.SourceRuntimeBase==null||binding.TargetPc!=(ulong)binding.SourceRuntimeBase.Value+(ulong)binding.Target.Offset||
                        binding.Target.Offset<evidence.Extent.Offset||binding.Target.EndExclusive>evidence.Extent.EndExclusive)
                        throw new InvalidDataException("Behavior global target requires its exact original executable code-domain certificate.");
                    byte[] actual=_reader(evidence.Extent)??throw new InvalidDataException("Behavior global target preimage is absent.");
                    if(actual.LongLength!=evidence.Extent.ByteLength||NativeMipsBehaviorFragmentCodec.Hash(actual)!=evidence.ExpectedSha256)
                        throw new InvalidDataException("Behavior global target complete preimage changed.");
                }
                target=binding.TargetPc;consumed.Add(binding.SourcePc);
            }
            int offset=offsets[transfer.SourcePc];uint pc=runtimeBase+(uint)offset,word;
            if(transfer.Kind==NativeMipsBehaviorTransferKind.Branch16)
            {
                long delta=(long)target-pc-4;
                if(delta%4!=0||delta/4<short.MinValue||delta/4>short.MaxValue)
                    throw new InvalidDataException("Native branch displacement exceeds its signed 16-bit range; no invented veneer.");
                word=(transfer.SourceWord&0xFFFF0000)|(ushort)(short)(delta/4);
            }
            else
            {
                if(((pc+4)^target)>>28!=0)throw new InvalidDataException("Native jump target crosses its 256MiB encoding region.");
                uint opcode=transfer.Kind is NativeMipsBehaviorTransferKind.Call26 or NativeMipsBehaviorTransferKind.CallbackRegister?3u:2u;
                word=(opcode<<26)|((target>>2)&0x3FFFFFF);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(offset),word);
            if(transfer.Kind==NativeMipsBehaviorTransferKind.Fallthrough)BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(offset+4),0);
        }
        if(consumed.Count!=bound.Count)throw new InvalidDataException("Unused behavior external binding attempts to override an internal or absent edge.");
        return new(output,mapped,bindingSnapshot.Where(b=>b.Target.Space.Revision==NativeAddressRevision.Final).Select(b=>b.Target));
    }
}
internal static class NativeMipsBehaviorFragmentCodec
{
    public static NativeMipsBehaviorFragmentPackage Capture(IEnumerable<NativeMipsBehaviorFragmentSource> fragments,
        IReadOnlyList<uint> entryPcs,IReadOnlyList<uint> interceptedExits,Func<NativeByteExtent,byte[]> reader)
    {
        ArgumentNullException.ThrowIfNull(fragments);ArgumentNullException.ThrowIfNull(entryPcs);ArgumentNullException.ThrowIfNull(interceptedExits);ArgumentNullException.ThrowIfNull(reader);
        List<NativeMipsBehaviorFragmentSource> sources=[];
        foreach(var f in fragments){if(sources.Count==64)throw new InvalidDataException("Native fragment request budget exceeded.");sources.Add(f);}
        if(sources.Count==0||sources.Any(f=>f==null||f.Read==null||f.Read.Extent==null||string.IsNullOrWhiteSpace(f.Id))||sources.Select(f=>f.Id).Distinct().Count()!=sources.Count)
            throw new InvalidDataException("Native fragments have missing or duplicate source identity.");
        var entries=BoundedSnapshot(entryPcs,64,"entry");var exits=BoundedSnapshot(interceptedExits,64,"intercepted exit");
        if(entries.Length==0||entries.Distinct().Count()!=entries.Length||exits.Distinct().Count()!=exits.Length)
            throw new InvalidDataException("Native behavior roots/exits exceed their unique bounded schema.");
        var ordered=sources.OrderBy(f=>f.SourcePc).ToArray();ulong previous=0;int total=0;
        List<NativeMipsCapturedBehaviorFragment> captured=[];List<NativeMipsBehaviorTransfer> transfers=[];List<NativeMipsBehaviorStackAccess> stack=[];
        foreach(var f in ordered)
        {
            if(f.SourcePc%4!=0||f.SourcePc<0x80000000||f.Read.Extent.Space.Revision!=NativeAddressRevision.Original||
                f.Read.Extent.ByteLength<=0||f.Read.Extent.ByteLength%4!=0||f.Read.Extent.ByteLength>0x20000||
                f.SourcePc<=previous||(ulong)f.SourcePc+(ulong)f.Read.Extent.ByteLength>0x80200000)
                throw new InvalidDataException("Native code fragment source address/overlap/budget is invalid.");
            total+=checked((int)f.Read.Extent.ByteLength);if(total>0x20000)throw new InvalidDataException("Native fragment byte budget exceeded.");
            previous=(ulong)f.SourcePc+(ulong)f.Read.Extent.ByteLength;
            byte[] actual=reader(f.Read.Extent)??throw new InvalidDataException("Native fragment source preimage is absent.");
            if(actual.LongLength!=f.Read.Extent.ByteLength)throw new InvalidDataException("Native fragment source read length differs from its bounded owner before copying.");
            byte[] bytes=actual.ToArray();
            if(Hash(bytes)!=f.Read.ExpectedSha256)throw new InvalidDataException("Native fragment complete source preimage changed.");
            for(int offset=0;offset<bytes.Length;offset+=4)
            {
                uint pc=f.SourcePc+(uint)offset,w=Word(bytes,offset);ValidateInstruction(w);
                var kind=Control(w);
                if(kind is not null)
                {
                    if(offset>bytes.Length-8)throw new InvalidDataException("Native control transfer has a missing delay slot.");
                    uint delay=Word(bytes,offset+4);ValidateInstruction(delay);
                    if(Control(delay)!=null)throw new InvalidDataException("Native control transfer inside a delay slot is unsupported.");
                    uint? target=kind switch
                    {
                        NativeMipsBehaviorTransferKind.Branch16=>unchecked(pc+4+(uint)((short)w*4)),
                        NativeMipsBehaviorTransferKind.Jump26 or NativeMipsBehaviorTransferKind.Call26=>((pc+4)&0xF0000000)|((w&0x3FFFFFF)<<2),
                        _=>null
                    };
                    transfers.Add(new(pc,target,true,kind.Value,w,delay));
                }
                uint op=w>>26;int rs=(int)(w>>21)&31;
                if(rs==29&&op>=0x20)
                {
                    int width=op is 0x20 or 0x24 or 0x28?1:op is 0x21 or 0x25 or 0x29?2:4;
                    stack.Add(new(pc,(short)w,width,op is >=0x20 and <=0x26 or 0x32));
                }
            }
            bool falls=true;
            if(bytes.Length>=8)
            {
                uint penultimate=Word(bytes,bytes.Length-8);var kind=Control(penultimate);
                falls=!(kind is NativeMipsBehaviorTransferKind.Jump26 or NativeMipsBehaviorTransferKind.ReturnRegister||
                    kind==NativeMipsBehaviorTransferKind.Branch16&&(penultimate>>26)==4&&((penultimate>>21)&31)==((penultimate>>16)&31));
            }
            if(falls)transfers.Add(new(f.SourcePc+(uint)bytes.Length,f.SourcePc+(uint)bytes.Length,false,NativeMipsBehaviorTransferKind.Fallthrough));
            captured.Add(new(f,bytes,falls));
        }
        // A native delay-slot word can also have an independent branch/root
        // entry. In that case its ordinary fallthrough must remain explicit.
        for(int i=0;i<captured.Count;i++)
        {
            var f=captured[i];uint last=f.Source.SourcePc+(uint)f.ByteLength-4;
            if(!f.HasFallthrough&&(entries.Contains(last)||transfers.Any(t=>t.TargetPc==last)))
            {
                captured[i]=new(f.Source,f.CopyBytes(),true);
                transfers.Add(new(last+4,last+4,false,NativeMipsBehaviorTransferKind.Fallthrough));
            }
        }
        bool Owns(uint pc)=>pc%4==0&&captured.Any(f=>pc>=f.Source.SourcePc&&(ulong)pc<f.Source.SourcePc+(ulong)f.ByteLength);
        if(entries.Any(p=>!Owns(p))||exits.Any(p=>p%4!=0||Owns(p)))throw new InvalidDataException("Native entry or intercepted exit has invalid code ownership.");
        return new(captured,transfers.OrderBy(t=>t.SourcePc).ToArray(),stack,entries.Order().ToArray(),exits.Order().ToArray(),reader);
    }
    internal static T[] BoundedSnapshot<T>(IEnumerable<T> source,int maximum,string label)
    {
        List<T> result=[];
        foreach(var item in source){if(result.Count==maximum)throw new InvalidDataException($"Native behavior {label} budget exceeded.");result.Add(item);}
        return result.ToArray();
    }
    private static NativeMipsBehaviorTransferKind? Control(uint w)
    {
        uint op=w>>26;
        if(op==2)return NativeMipsBehaviorTransferKind.Jump26;if(op==3)return NativeMipsBehaviorTransferKind.Call26;
        if(op is >=1 and <=7)return NativeMipsBehaviorTransferKind.Branch16;
        if(op==0&&(w&63)==9)return NativeMipsBehaviorTransferKind.CallbackRegister;
        if(op==0&&(w&63)==8)return NativeMipsBehaviorTransferKind.ReturnRegister;
        return null;
    }
    private static void ValidateInstruction(uint w)
    {
        uint op=w>>26,fun=w&63,rs=(w>>21)&31,rt=(w>>16)&31,rd=(w>>11)&31;
        if(op==0)
        {
            if(fun==8&&(rs!=31||rt!=0||rd!=0||(w&0x7C0)!=0)||fun==9&&(rs==0||rt!=0||rd!=31||(w&0x7C0)!=0))
                throw new InvalidDataException("Unclosed computed native transfer/register convention.");
            bool invalid=fun is 0 or 2 or 3?rs!=0:
                fun is 4 or 6 or 7 or >=32?(w&0x7C0)!=0:
                fun is 16 or 18?rs!=0||rt!=0||(w&0x7C0)!=0:
                fun is 17 or 19?rt!=0||rd!=0||(w&0x7C0)!=0:
                fun is >=24 and <=27?rd!=0||(w&0x7C0)!=0:false;
            if(invalid)throw new InvalidDataException("Unsupported native MIPS reserved opcode fields.");
            if(new uint[]{0,2,3,4,6,7,8,9,16,17,18,19,24,25,26,27,32,33,34,35,36,37,38,39,42,43}.Contains(fun))return;
        }
        else if(op==1){if(rt is 0 or 1)return;}
        else if(op is >=2 and <=15)
        {if(op is 6 or 7&&rt!=0||op==15&&rs!=0)throw new InvalidDataException("Unsupported native branch/immediate opcode fields.");return;}
        else if(op==18){if(rs is 0 or 2 or 4 or 6 or >=16)return;}
        else if(op is 0x20 or 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x28 or 0x29 or 0x2A or 0x2B or 0x2E or 0x32 or 0x3A)return;
        throw new InvalidDataException("Unsupported native MIPS opcode/control variant.");
    }
    internal static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static uint Word(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
internal sealed class NativeMipsOwnedFrameAdapter
{
    private readonly byte[] _entry,_exit;
    private NativeMipsOwnedFrameAdapter(uint entryTarget,uint exitTarget)
    {
        if(entryTarget<0x80000000||entryTarget>=0x80200000||exitTarget<0x80000000||exitTarget>=0x80200000||(entryTarget|exitTarget)%4!=0)
            throw new InvalidDataException("Owned-frame adapter targets must be aligned native main-RAM instructions.");
        int[] registers=Enumerable.Range(1,31).Where(r=>r!=29).ToArray();List<uint> entry=[I(9,29,29,-0x2B0)],exit=[];
        for(int i=0;i<registers.Length;i++)entry.Add(I(0x2B,29,registers[i],0x230+i*4));
        entry.AddRange([R(0,0,8,16),I(0x2B,29,8,0x2A8),R(0,0,8,18),I(0x2B,29,8,0x2AC),I(9,0,30,1),Jump(entryTarget),0]);
        // R3000 load-delay slots are explicit before mthi/mtlo consumers.
        exit.AddRange([I(0x23,29,8,0x2A8),0,R(8,0,0,17),I(0x23,29,8,0x2AC),0,R(8,0,0,19)]);
        for(int i=0;i<registers.Length;i++)exit.Add(I(0x23,29,registers[i],0x230+i*4));
        exit.AddRange([Jump(exitTarget),I(9,29,29,0x2B0)]);
        _entry=Bytes(entry);_exit=Bytes(exit);
    }
    public int FrameByteLength=>0x2B0;
    public bool RuntimeStackFitProven=>false;
    public byte[] CopyEntry()=>_entry.ToArray();
    public byte[] CopyExit()=>_exit.ToArray();
    public static NativeMipsOwnedFrameAdapter Create(uint entryTarget,uint exitTarget)=>new(entryTarget,exitTarget);
    private static uint I(int op,int rs,int rt,int immediate)=>(uint)(op<<26|rs<<21|rt<<16)|(ushort)(short)immediate;
    private static uint R(int rs,int rt,int rd,int function)=>(uint)(rs<<21|rt<<16|rd<<11|function);
    private static uint Jump(uint pc)=>0x08000000|((pc>>2)&0x3FFFFFF);
    private static byte[] Bytes(List<uint> words){byte[] bytes=new byte[words.Count*4];for(int i=0;i<words.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4),words[i]);return bytes;}
}
