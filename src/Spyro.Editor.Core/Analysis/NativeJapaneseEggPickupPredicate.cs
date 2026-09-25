using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeJapaneseEggPickupPredicateSmoke")]

namespace Spyro.Editor.Core.Analysis;

internal sealed record NativeJapaneseEggPickupEvidence(string Role, string Region, uint Pc, int ByteLength, string Sha256);
internal sealed record NativeJapaneseEggPickupOperand(uint SourcePc, uint FinalPc, uint Before, uint After, uint UsaAddress);

/// <summary>
/// Detached CPU-only callable: a0 is a readable, aligned native row; v0 returns
/// 0/1. Reads row+0C/+10/+14 and signed halfword+38 plus pinned USA player fields.
/// Preserves s0-s7/fp/gp/sp/ra, HI/LO, and t registers; clobbers at/v0/v1/a0-a3.
/// Needs an owned 0x20-byte stack frame, disjoint from code, row and globals.
/// This is NOT collection, runtime allocation, a hook or a save transition.
/// </summary>
internal sealed class NativeJapaneseEggPickupPredicateKernel
{
    private readonly byte[] _bytes;
    internal NativeJapaneseEggPickupPredicateKernel(byte[] bytes, NativeByteExtent owner, uint runtimeBase,
        IReadOnlyDictionary<uint,uint> mapping, NativeJapaneseEggPickupOperand[] patches, bool sourceBound)
    {
        _bytes=bytes.ToArray();Owner=owner;RuntimeBase=runtimeBase;SourceBound=sourceBound;
        SourceToFinalPc=new System.Collections.ObjectModel.ReadOnlyDictionary<uint,uint>(new Dictionary<uint,uint>(mapping));
        OperandPatches=Array.AsReadOnly(patches.ToArray());
    }
    internal NativeByteExtent Owner { get; }
    internal uint RuntimeBase { get; }
    internal uint EntryPc=>RuntimeBase+0x100;
    internal int StackByteLength=>0x20;
    internal bool SourceBound { get; }
    internal bool DispatchActivated=>false;
    internal bool PublicationApproved=>false;
    internal bool RuntimeStackFitProven=>false;
    internal IReadOnlyDictionary<uint,uint> SourceToFinalPc { get; }
    internal IReadOnlyList<NativeJapaneseEggPickupOperand> OperandPatches { get; }
    internal byte[] CopyBytes()=>_bytes.ToArray();
}

/// <summary>
/// Exact-source F5 proximity/player-state fragment, ending BEFORE any sound,
/// effect, per-level alias lookup, collection-byte store or retirement. Its only
/// helper is the complete JP/USA byte-identical XY approximation leaf. Native
/// signed ADD/SUB overflow exceptions remain native exceptions, not false/true.
/// </summary>
internal sealed class NativeJapaneseEggPickupPredicateSource
{
    private const uint ExeBase=0x8000F800, PredicatePc=0x8008DF64, DistancePc=0x80016F74;
    private const string PredicateHash="a787f4aab4b11fe4ebaf6694ef543ded92f36e47ea28873cf5b7028d7890b9bb";
    private const string DistanceHash="277c87f0a049389cf5c3e0510eac8b878a516d9f773d44545184f67910815d90";
    private readonly byte[] _predicate,_distance;
    private readonly bool _sourceBound;
    private static readonly (uint Pc,uint Before,uint After,uint Address)[] Operands=
    [
        (0x8008DF88,0x8C8428A0,0x8C848A60,0x80078A60),
        // The USA flags word is NOT JP minus the player-coordinate base delta.
        (0x8008DFC0,0x8C422A90,0x8C428C4C,0x80078C4C),
        (0x8008DFD8,0x8C632910,0x8C638AD0,0x80078AD0),
        (0x8008DFEC,0x8C422914,0x8C428AD4,0x80078AD4)
    ];
    private NativeJapaneseEggPickupPredicateSource(byte[] predicate,byte[] distance,bool sourceBound,
        IEnumerable<NativeJapaneseEggPickupEvidence> evidence)
    {
        if(predicate.Length!=0x98||Hash(predicate)!=PredicateHash)throw new InvalidDataException("F5 predicate preimage changed.");
        if(distance.Length!=0x60||Hash(distance)!=DistanceHash)throw new InvalidDataException("F5 distance preimage changed.");
        _predicate=predicate.ToArray();_distance=distance.ToArray();_sourceBound=sourceBound;
        Evidence=Array.AsReadOnly(evidence.ToArray());
        var pd=new NativeAddressSpace(NativeAddressKind.LevelData,sourceBound?"jp-f5-predicate-original":"fixture-f5-predicate",NativeAddressRevision.Original,0x98);
        var dd=new NativeAddressSpace(NativeAddressKind.ExecutableFile,sourceBound?"regional-f5-distance-original":"fixture-f5-distance",NativeAddressRevision.Original,0x60);
        byte[] Read(NativeByteExtent e)=>ReferenceEquals(e.Space,pd)?_predicate.AsSpan((int)e.Offset,(int)e.ByteLength).ToArray():
            ReferenceEquals(e.Space,dd)?_distance.AsSpan((int)e.Offset,(int)e.ByteLength).ToArray():
            throw new InvalidDataException("Unbound F5 predicate source owner.");
        Package=NativeMipsBehaviorFragmentCodec.Capture(
            [new("native-xy-distance",DistancePc,new("xy-distance",new(dd,0,0x60),DistanceHash)),
             new("pickup-predicate-only",PredicatePc,new("predicate",new(pd,0,0x98),PredicateHash))],
            [PredicatePc],[0x8008DFFC,0x80090FA4],Read);
        if(Package.InstructionCount!=62||Package.StackAccesses.Count!=0||
            Package.Transfers.Count(t=>t.Kind==NativeMipsBehaviorTransferKind.Call26)!=1||
            !Package.Transfers.Any(t=>t.SourcePc==0x8008DF68&&t.TargetPc==DistancePc&&t.Kind==NativeMipsBehaviorTransferKind.Call26)||
            Package.Transfers.Any(t=>t.Kind==NativeMipsBehaviorTransferKind.CallbackRegister)||
            Package.Fragments.SelectMany(f=>Enumerable.Range(0,f.ByteLength/4).Select(i=>Word(f.CopyBytes(),i*4)>>26)).Any(op=>op is 0x28 or 0x29 or 0x2A or 0x2B or 0x2E))
            throw new InvalidDataException("F5 predicate/helper closure or store-free ABI changed.");
    }
    internal NativeMipsBehaviorFragmentPackage Package { get; }
    internal IReadOnlyList<NativeJapaneseEggPickupEvidence> Evidence { get; }
    internal byte[] CopyPredicate()=>_predicate.ToArray();
    internal byte[] CopyDistance()=>_distance.ToArray();
    internal static NativeJapaneseEggPickupPredicateSource Fixture(byte[] predicate,byte[] distance)=>new(predicate,distance,false,[]);

    internal static NativeJapaneseEggPickupPredicateSource Capture(NativeRegionalSourceSession japan,NativeRegionalSourceSession usa)
    {
        ArgumentNullException.ThrowIfNull(japan);ArgumentNullException.ThrowIfNull(usa);
        if(japan.Revision!=NativeDiscRevision.JapanScps10083||usa.Revision!=NativeDiscRevision.UsaScus94228)
            throw new InvalidDataException("F5 pickup requires exact separate regional sources.");
        try
        {
            byte[] jo=japan.ReadWadEntry(22),uo=usa.ReadWadEntry(9);
            if(Hash(jo)!="bcfe62569db265135d5627de1902a9d31da82c72f51acdc461d2872e08f7cdf2"||
                Hash(uo)!="7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55")
                throw new InvalidDataException("Complete regional F5 pickup overlay preimage changed.");
            List<NativeJapaneseEggPickupEvidence> evidence=[];
            byte[] Read(string role,bool jp,uint pc,int length,string hash,bool overlay=false)
            {
                byte[] b=overlay?(jp?jo:uo).AsSpan((int)(pc-(jp?0x80084A28u:0x8007AA38u)),length).ToArray():
                    (jp?japan:usa).ReadExecutableRange(pc-ExeBase,length);
                if(Hash(b)!=hash)throw new InvalidDataException("F5 pickup source consumer changed: "+role);
                evidence.Add(new(role,jp?"JP":"USA",pc,length,hash));return b;
            }
            byte[] predicate=Read("predicate",true,PredicatePc,0x98,PredicateHash,true);
            byte[] distance=Read("distance-jp",true,DistancePc,0x60,DistanceHash);
            byte[] usaDistance=Read("distance-usa",false,0x80017990,0x60,DistanceHash);
            if(!distance.SequenceEqual(usaDistance))throw new InvalidDataException("Regional native distance identity differs.");
            _=Read("jp-player-frame-pointer",true,0x80087EB4,8,"aab6febd7d2c72c15660cb9ffe6221142cb6b1ded7975b0f0e83d176f9621f21",true);
            _=Read("usa-row-prologue-fp-is-one",false,0x8007DA38,0x64,"7d531ec1116f4d20f5dfd2e769f5da3cd9facf7c1288c4a61729e9e0acf1745e",true);
            _=Read("usa-player-xyz-constructor-input",false,0x800860D4,0x34,"d0fdb4f986a005106f54b18de70c41f481b7c538916d99ea0f1da766f56f92ed",true);
            _=Read("usa-three-word-position-copy",false,0x80017700,0x1C,"c8e9d6ff8966aaa5fbc6e7ecbab1f9d4b379d15f22e3091881361a770fd433b7");
            _=Read("usa-player-xy-retail-consumer",false,0x8007EE34,0x28,"85b7c4cf7c36778c994477ba078dc40669c19becdd02dd7020ff7178754d60d7",true);
            _=Read("jp-state15-substate11",true,0x8003610C,0x28,"c92e2c3343babb4edce8667e91f339a2b56390e8b99577e7dc6046a02e28f868");
            _=Read("usa-state15-substate11",false,0x80034EE0,0x28,"bae4485be67da45550fa2c6d68cff727fcd7bdaa35d45e93d876d0a4e690aceb");
            byte[] jf=Read("jp-player-bit2000",true,0x8004E62C,0x20,"bda0c8e8ae03d046f058bce662c35759ab0da8b3fe06f7734abbb12a2d94e616");
            byte[] uf=Read("usa-player-bit2000",false,0x8004A200,0x20,"307a0e7bcdde9e06182f77cde19af04195266f85ce53a1ea426235185b85d770");
            if(!Enumerable.Range(0,8).Where(i=>Word(jf,i*4)!=Word(uf,i*4)).SequenceEqual([1]))
                throw new InvalidDataException("Player flags consumers differ beyond the exact regional operand.");
            return new(predicate,usaDistance,true,evidence);
        }
        finally { japan.VerifyUnchanged();usa.VerifyUnchanged(); }
    }

    internal NativeJapaneseEggPickupPredicateKernel Compile(NativeByteExtent owner,uint runtimeBase)
    {
        ArgumentNullException.ThrowIfNull(owner);
        const int length=0x154;
        if(owner.Space.Kind!=NativeAddressKind.LevelData||owner.Space.Revision!=NativeAddressRevision.Final||
            owner.ByteLength!=length||owner.Offset%4!=0||runtimeBase%4!=0||runtimeBase<0x80000000||(ulong)runtimeBase+length>0x80200000)
            throw new InvalidDataException("F5 predicate requires its exact aligned detached final code owner.");
        foreach(var (address,count) in new (uint,int)[]{(0x80078A58,12),(0x80078AD0,8),(0x80078C4C,4)})
            if((ulong)runtimeBase<(ulong)address+(uint)count&&(ulong)runtimeBase+length>address)
                throw new InvalidDataException("F5 detached code overlaps a bound player input owner.");
        const int entry=0x100,reject=0x124,accept=0x130,restore=0x13C;
        List<NativeMipsBehaviorExternalBinding> bindings=[];
        foreach(var t in Package.Transfers)
        {
            if(t.Kind==NativeMipsBehaviorTransferKind.ReturnRegister||t.TargetPc is uint target&&Package.OwnsPc(target))continue;
            int offset=t.TargetPc switch {0x80090FA4=>reject,0x8008DFFC=>accept,_=>throw new InvalidDataException("Unclosed F5 predicate exit.")};
            bindings.Add(new(t.SourcePc,t.TargetPc,runtimeBase+(uint)offset,new(owner.Space,owner.Offset+offset,4)));
        }
        var linked=Package.Link(owner,runtimeBase,bindings);byte[] linkedBytes=linked.CopyBytes(),bytes=new byte[length];
        if(linkedBytes.Length!=entry||linked.UnmaterializedExternalTargets.Count!=2)
            throw new InvalidDataException("F5 predicate link changed its exact emitted/reserved owner partition.");
        linkedBytes.CopyTo(bytes,0);
        List<NativeJapaneseEggPickupOperand> patches=[];
        foreach(var p in Operands)
        {
            uint pc=linked.SourceToFinalPc[p.Pc];int at=(int)(pc-runtimeBase);
            if(Word(bytes,at)!=p.Before)throw new InvalidDataException("F5 regional operand preimage changed.");
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at),p.After);
            patches.Add(new(p.Pc,pc,p.Before,p.After,p.Address));
        }
        // This is a callable 0x20 frame, not the existing loop adapter whose
        // full-register restoration deliberately restores v0 and sets fp=1.
        Write(entry,[0x27BDFFE0,0xAFB30010,0xAFBE0014,0xAFBF0018,0x00809821,
            0x3C1E8008,0x27DE8A58,Jump(linked.SourceToFinalPc[PredicatePc]),0]);
        Write(reject,[0x24020000,Jump(runtimeBase+restore),0]);
        Write(accept,[0x24020001,Jump(runtimeBase+restore),0]);
        Write(restore,[0x8FB30010,0x8FBE0014,0x8FBF0018,0,0x03E00008,0x27BD0020]);
        foreach(var reserved in linked.UnmaterializedExternalTargets)
            if(reserved.Space!=owner.Space||reserved.ByteLength!=4||
                reserved.Offset!=owner.Offset+reject&&reserved.Offset!=owner.Offset+accept)
                throw new InvalidDataException("Unmaterialized F5 predicate external target.");
        return new(bytes,owner,runtimeBase,linked.SourceToFinalPc,patches.ToArray(),_sourceBound);
        void Write(int at,uint[] words)
        {foreach(uint word in words){BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at),word);at+=4;}}
    }
    private static uint Jump(uint pc)=>0x08000000|((pc>>2)&0x3FFFFFF);
    private static uint Word(byte[] b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
