using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalMagicMovementBoundaryReadback(uint Base, int Bytes, string SHA256,
    int MinimumXY, int MaximumXYExclusive, IReadOnlyList<uint> GuardSourcePCs)
{
    public string Scope { get; init; } = "PRIVATE exact Magic12/13 + original DH; original in-domain native movement retained; unsafe proposed grid coordinates return blocked2 before query/XY commit";
}

/// <summary>Private boundary behavior fix, not an original all-input safety or selector pruning certificate.</summary>
internal static class UniversalMagicPairMovementBoundary
{
    internal const int Minimum = 8192, MaximumExclusive = 253952;
    private sealed record Function(uint Entry, uint End, string SHA256, IReadOnlyList<uint> Words);
    private sealed record Fixture(IReadOnlyList<Function> Functions);
    private static readonly Dictionary<uint,uint[]> Guards = new()
    {
        [0x80039398] = [0x80039454,0x800394AC,0x80039500,0x800395D8],
        [0x80039688] = [0x80039738,0x80039788,0x800397DC,0x80039844],
        [0x80039910] = []
    };
    private static readonly Dictionary<uint,uint> Footers = new() { [0x80039398]=0x80039668, [0x80039688]=0x800398F0 };
    internal static string EntryLabel(uint entry) => $"private_magic_native_{entry:X8}";
    private static string PCLabel(uint pc) => $"private_magic_native_pc_{pc:X8}";
    private static string AbortLabel(uint entry) => $"private_magic_native_abort_{entry:X8}";
    private static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool ok,string why) { if(!ok)throw new InvalidDataException("Private Magic movement boundary: "+why); }
    private static Fixture Load()
    {
        using var s=typeof(UniversalMagicPairMovementBoundary).Assembly.GetManifestResourceStream("Spyro.Editor.Core.Universal.PrivateMagicMovementSource.json") ?? throw new InvalidDataException("Missing original private movement fixture");
        using var m=new MemoryStream();s.CopyTo(m);var b=m.ToArray();
        Require(Hash(b)=="c2081bf9a8c08f8a0ac023a6a2d42dba37a77dfe599c1365b2b105b6d41f98d4","whole original movement fixture changed");
        return JsonSerializer.Deserialize<Fixture>(b) ?? throw new InvalidDataException("Invalid original movement fixture");
    }
    internal static UniversalAsmPart Build()
    {
        var fixture=Load();UniversalAsmPart a=new();
        foreach(var f in fixture.Functions)
        {
            Require(Guards.ContainsKey(f.Entry) && f.End-f.Entry==f.Words.Count*4,"original native complete function extent changed");
            byte[] original=new byte[f.Words.Count*4];for(int i=0;i<f.Words.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(original.AsSpan(i*4),f.Words[i]);
            Require(Hash(original)==f.SHA256,"original complete movement packet changed");
            a.Label(EntryLabel(f.Entry));
            for(int i=0;i<f.Words.Count;i++)
            {
                uint pc=f.Entry+(uint)i*4,w=f.Words[i];a.Label(PCLabel(pc));
                Require(!(UniversalMips.IsLoad(w)||UniversalMips.IsStore(w)) || UniversalMips.Rs(w)!=29 || UniversalMips.SImm(w) is not (0 or 4),"guard O32 temporary slots are read/written by original native function");
                if(Guards[f.Entry].Contains(pc))
                {
                    Require(i==0 || !UniversalMips.IsBranch(f.Words[i-1]),"guard splits original control delay slot");
                    EmitGuard(a,f.Entry);
                }
                UniversalFix fix=default;
                if(UniversalMips.IsPcRelative(w) || UniversalMips.Op(w)==2)
                {
                    uint target=UniversalMips.BranchTarget(w,pc);Require(target>=f.Entry && target<f.End,"native direct control leaves whole owned function");
                    fix=new(UniversalMips.Op(w)==2?UniversalFixKind.JumpLabel:UniversalFixKind.BranchLabel,Label:PCLabel(target));
                }
                if(UniversalMips.IsJr(w))Require(w==UniversalMips.JrRa,"original movement gains computed transfer");
                Require(!UniversalMips.IsJalr(w),"original movement gains indirect callback");
                if(pc==0x800399BC)
                {
                    Require(w==UniversalMips.EncodeJ(3,0x80039688),"gravity child producer changed");fix=new(UniversalFixKind.JalLabel,Label:EntryLabel(0x80039688));
                }
                a.Emit(w,fix);
            }
            if(Footers.TryGetValue(f.Entry,out uint footer))
            {
                a.Label(AbortLabel(f.Entry));a.Emit(UniversalMips.EncodeI(35,29,8,0));a.Emit(UniversalMips.EncodeI(35,29,9,4));a.Emit(0);
                a.Emit(UniversalMips.EncodeI(9,0,2,2));a.Emit(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.JumpLabel,Label:PCLabel(footer)));a.Emit(0);
            }
        }
        return a;
    }
    private static void EmitGuard(UniversalAsmPart a,uint entry)
    {
        uint I(int op,int rs,int rt,int imm)=>UniversalMips.EncodeI(op,rs,rt,imm);
        // Native movement owns 64B frames; O32 SP0/4 are unused by the complete
        // original packets. Save/restore T0/T1 without another call or frame.
        a.Emit(I(43,29,8,0));a.Emit(I(43,29,9,4));
        foreach(int off in new[]{24,28})
        {
            a.Emit(I(35,29,8,off));a.Emit(0);a.Emit(I(9,8,8,-Minimum));
            a.Emit(I(15,0,9,3));a.Emit(I(13,9,9,0xC000));a.Emit(0x0109402B); // SLTU T0,T0,T1 width245760
            a.Emit(I(4,8,0,0),new(UniversalFixKind.BranchLabel,Label:AbortLabel(entry)));a.Emit(0);
        }
        a.Emit(I(35,29,8,0));a.Emit(I(35,29,9,4));a.Emit(0);
    }
    internal static UniversalMagicMovementBoundaryReadback RequireWritten(byte[] code,IReadOnlyDictionary<string,uint> labels,uint codeBase,IReadOnlyDictionary<uint,uint> allowedIncoming)
    {
        Require(labels.TryGetValue(EntryLabel(0x80039398),out uint at),"whole native movement copy label missing");
        var (expected,expectedLabels)=UniversalBehaviorBundleBuilder.Assemble([Build()],at);
        Require(at>=codeBase && (ulong)at+(uint)expected.Length <= (ulong)codeBase+(uint)code.Length,"native copies leave allocated behavior owner");
        Require(expectedLabels.All(p=>labels.TryGetValue(p.Key,out uint v)&&v==p.Value),"copied native entry/branch/guard/abort labels changed");
        Require(code.AsSpan(checked((int)(at-codeBase)),expected.Length).SequenceEqual(expected),"complete copied native argument/branch/query/store/guard/return preimage changed");
        uint end=at+(uint)expected.Length;
        for(int off=0;off<code.Length;off+=4)
        {
            uint pc=codeBase+(uint)off;if(pc>=at&&pc<end)continue;
            uint w=BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(off));
            if(UniversalMips.IsPcRelative(w)||UniversalMips.Op(w) is 2 or 3)
            {
                uint target=UniversalMips.BranchTarget(w,pc);
                if(target>=at&&target<end) Require(UniversalMips.IsJal(w)&&allowedIncoming.TryGetValue(pc,out uint own)&&own==target,"outside copied transfer enters native guard/argument/store interior");
            }
            Require(!(w>=at&&w<end),"outside copied raw address/table pointer enters native copy");
            if(UniversalMips.Op(w)==15)
                for(int j=1;j<=4 && off+j*4<code.Length;j++)
                {
                    uint low=BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(off+j*4));
                    if(UniversalMips.Rs(low)!=UniversalMips.Rt(w)||UniversalMips.Op(low) is not (9 or 13))continue;
                    uint address=UniversalMips.Op(low)==9?unchecked((w&65535)<<16)+(uint)UniversalMips.SImm(low):((w&65535)<<16)|(low&65535);
                    Require(!(address>=at&&address<end),"outside copied materialized address enters native copy");
                }
        }
        return new(at,expected.Length,Hash(expected),Minimum,MaximumExclusive,Guards.Values.SelectMany(v=>v).ToArray());
    }
    internal static void RequireReadback(byte[] overlay,UniversalMagicMovementBoundaryReadback proof)
    {
        var (expected,_)=UniversalBehaviorBundleBuilder.Assemble([Build()],proof.Base);
        Require(proof.MinimumXY==Minimum && proof.MaximumXYExclusive==MaximumExclusive && proof.Bytes==expected.Length && proof.SHA256==Hash(expected),"readback boundary contract changed");
        Require(proof.GuardSourcePCs.SequenceEqual(Guards.Values.SelectMany(v=>v)),"readback query/commit guard set changed");
        Require(proof.Base>=UniversalOverlay.Base && (ulong)proof.Base+(uint)proof.Bytes <= (ulong)UniversalOverlay.Base+(uint)overlay.Length && overlay.AsSpan(checked((int)(proof.Base-UniversalOverlay.Base)),proof.Bytes).SequenceEqual(expected),"written native boundary copies changed");
    }
}
