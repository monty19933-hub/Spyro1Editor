using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Closed Artisans/slot-zero live-consumer gate. No stack, calls or stores.
/// Bound rows alone require the source-projected scene, current native selected
/// save tuple, owned A00 reserve and Ready ledger. Epoch is nonzero opaque
/// admission provenance, not a comparison with the discarded card overlay.
/// Native adapter/caller frames already exist: rejection adds no writes but
/// cannot repair an invalid incoming native frame. IRQ/reentrancy is unproved.
/// </summary>
internal sealed class NativeJapaneseEggLiveGuardKernel
{
    internal const int CapacityBytes=0x800,NestedBytes=0x60,CallerBytes=0x4B0;
    internal const uint Live=0x801FF600,StackFloor=0x801FF800,StackEnd=0x80200000;
    private readonly byte[] bytes;
    private NativeJapaneseEggLiveGuardKernel(NativeJapaneseEggAuthoredClassesPlan authored,
        NativeJapaneseEggCollectibleBinding binding,NativeJapaneseEggOverlayProjection projection,
        NativeJapaneseEggLifecycleCallables lifecycle,NativeByteExtent capacity,uint codeAddress,uint preBridge,uint postBridge)
    {
        // Its game code compares the scene address (0x800785E4) with the 2 MB layout's.
        Spyro.Editor.Core.Universal.GameRam.RequirePlayStationSceneAddress("The Japanese egg guard");
        binding.RequireCurrent(authored,NativeCollectibleCardLoadResearchComposition.FixedManifest());projection.RequireCurrent(authored);
        if(lifecycle.Attempt.OrdinarySlot!=0||lifecycle.Attempt.CallerLedgerAddress!=Live||
            lifecycle.MaximumStackByteLength!=NestedBytes||capacity.ByteLength!=CapacityBytes||
            !ReferenceEquals(capacity.Space,projection.Layout.Domain)||codeAddress!=0x8007AA38+capacity.Offset||
            capacity.EndExclusive>projection.Layout.FinalByteLength||projection.SceneEnd>Live||projection.ModelAddress<projection.Layout.CopyAnchor)
            throw new InvalidDataException("Guard requires its final fixed-manifest slot-zero lifecycle and disjoint source geometry.");
        Owner=capacity;SourceBound=authored.SourceCertified;
        if(SourceBound)
        {
            byte[] original=authored.Integration.CopyOriginalExecutable();NativeJapaneseEggMemoryReport.RequireSourceExecutable(original);
            foreach(var(pc,length,hash) in new(uint,int,string)[]{
                (0x80033884,0x14,"d00d8c734a9eb8d4480f4fd5402139047f666c1ca36dfa7e9520d81f222e5149"),
                (0x80033A98,0x14,"fce1660c0e8ecc6eb36e45039fff8a2d8b5c6693faf064b870b155bc22138070"),
                (0x80013680,0x14,"7fb88653f48941877441fc528d3454a62484bd0d74a398bcec9991c4e227ee91"),
                (0x800158E0,0x24,"7fe4751ede7f3c6d49218f98e3a6365ff7485f784cf56d778544870ff98b694c"),
                (0x80015B34,0x3C,"d772757b766eb43222e32ee0e95d47e1f96d80337f42bcdc783a8870b122958c"),
                (0x80013F0C,0x70,"85d97245f95d942bb47091640da6e7efbcc4110db7b260902ac97bf549946f46"),
                (0x8005A710,0x78,"4d9e3ec2a7363919417bc9b49363fafc40d3682eae3ae7293d4f4ecadaa8a0f4"),
                (0x80012204,0x20,"6427a811295fc7d2cab53e9d76dc5b6f7405d78040ec5907a27dcce18142c846"),
                (0x8003385C,0x10,"ee2a02ca4beae773dd35f2196fc8d024078d45e16700094ffe67dcebe884407b")})
                if(Convert.ToHexStringLower(SHA256.HashData(original.AsSpan(checked((int)(pc-0x8000F800)),length)))!=hash)
                    throw new InvalidDataException("Guard current-scene/native-caller source window changed.");
            if(Convert.ToHexStringLower(SHA256.HashData(original.AsSpan(checked((int)(0x80015ACC-0x8000F800)),0x68)))!=
                    "e3333647e8548d11fd19653ac0d706b78571d6dbe1fa439b6db10b5b59235a9b"||
                Convert.ToHexStringLower(SHA256.HashData(original.AsSpan(checked((int)(0x80032B98-0x8000F800)),0x38)))!=
                    "4b251c8925f94486099909eb1f9fd93cb4a8a49beaa6b6b45c2225e24d8e2297"||
                Convert.ToHexStringLower(SHA256.HashData(authored.Integration.CopyOriginalOverlay().AsSpan(checked((int)(0x8007D9C8-0x8007AA38)),0x20)))!=
                    "3c109680073fb34137f9140e3f3f9356cdedcffea797b4277ffa63a725801466")
                throw new InvalidDataException("Guard model-registration/classifier/callback-frame source evidence changed.");
        }
        ExpectedSceneGlobals=Array.AsReadOnly(new(uint Address,uint Value)[]{
            (0x8007596C,10),(0x800757D8,0),(0x80075690,0),(0x80075734,0x8007D9C8),
            (0x800785D8,projection.CopyAnchor),(0x800785E0,projection.ModelAddress),(0x800785E4,projection.SceneAddress),
            (0x80075828,checked(projection.SceneAddress+(uint)authored.Integration.OriginalSceneSource.RowTableOffset)),
            (0x80075890,lifecycle.DynamicBase)}.Concat(projection.Models.Select(m=>(0x80076378+m.AuthoredClass*4u,m.RootAddress))).ToArray());
        var e=new Emitter(codeAddress);
        const int v0=2,t0=8,t1=9,t3=11,t7=15,s3=19,sp=29;
        void Route(uint target,uint unbound)
        {
            e.Const(t7,target);
            foreach(var row in binding.Rows){e.Const(t0,row.RowAddress);e.Branch(4,s3,t0,"validate");}
            e.Jump(unbound);
        }
        BeforeEntryPc=e.Pc;Route(preBridge,0x80088A38);
        AfterEntryPc=e.Pc;Route(postBridge,0x80088B14);
        ValidatePc=e.Pc;e.Label("validate");
        // Exact source frames: adapter2B0 + dispatcher1C0 + update20 + main20.
        // Their upper extent and max(visual28,lifecycle60) below SP must fit
        // the native top-stack reservation, disjoint from every live/data owner.
        e.I(12,sp,t0,7);e.Branch(5,t0,0,"refuse");
        e.Const(t0,StackFloor+NestedBytes);e.R(0x2B,sp,t0,t1);e.Branch(5,t1,0,"refuse");
        e.Const(t0,StackEnd-CallerBytes);e.R(0x2B,t0,sp,t1);e.Branch(5,t1,0,"refuse");
        void Read(int r,uint at,int op=0x23){e.Const(t3,at);e.Load(r,t3,0,op);}
        void Expect(uint at,uint value,int op=0x23){Read(t0,at,op);e.Const(t1,value);e.Branch(5,t0,t1,"refuse");}
        Expect(0x800755A4,0xA00); // No ledger access before ownership check.
        foreach(var pair in ExpectedSceneGlobals)Expect(pair.Address,pair.Value);
        Expect(0x80078D14,1);Expect(0x80078D1C,0);
        Expect(Live+0x16,1,0x24);Expect(Live+0x17,0,0x24);
        Read(t0,Live+0x18,0x24);e.I(11,t0,t1,2);e.Branch(4,t1,0,"refuse");e.R(0,0,t0,t0,4);
        Read(t1,0x80078D18);e.Branch(5,t0,t1,"refuse");
        Read(t0,Live+0x1A,0x25);Read(t1,0x80078D20);e.Branch(5,t0,t1,"refuse");
        Read(t0,Live+0x8C);e.Branch(4,t0,0,"refuse");
        e.R(8,t7,0,0);e.Word(0);
        e.Label("refuse");e.Const(v0,(uint)NativeJapaneseEggLifecycleResult.InvalidLedger);e.Jump(0x80088B14);
        byte[] code=e.Finish();if(code.Length>capacity.ByteLength)throw new InvalidDataException("Guard exceeds fixed owned code capacity.");
        CodeByteLength=code.Length;bytes=new byte[CapacityBytes];code.CopyTo(bytes,0);
    }
    internal NativeByteExtent Owner{get;}
    internal bool SourceBound{get;}
    internal uint BeforeEntryPc{get;}
    internal uint AfterEntryPc{get;}
    internal uint ValidatePc{get;}
    internal int CodeByteLength{get;}
    internal IReadOnlyList<(uint Address,uint Value)> ExpectedSceneGlobals{get;}
    internal byte[] CopyBytes()=>bytes.ToArray();
    internal static NativeJapaneseEggLiveGuardKernel Compile(NativeJapaneseEggAuthoredClassesPlan authored,
        NativeJapaneseEggCollectibleBinding binding,NativeJapaneseEggOverlayProjection projection,
        NativeJapaneseEggLifecycleCallables lifecycle,NativeByteExtent capacity,uint codeAddress,uint preBridge,uint postBridge)=>
        new(authored,binding,projection,lifecycle,capacity,codeAddress,preBridge,postBridge);
    private sealed class Emitter(uint start)
    {
        private readonly List<uint> words=[];private readonly Dictionary<string,int> labels=[];private readonly List<(int At,string Label)> branches=[];
        internal uint Pc=>start+(uint)words.Count*4;
        internal void Word(uint w)=>words.Add(w);
        internal void I(int op,int rs,int rt,int imm)=>Word((uint)(op<<26|rs<<21|rt<<16)|(ushort)imm);
        internal void R(int fn,int rs,int rt,int rd,int sh=0)=>Word((uint)(rs<<21|rt<<16|rd<<11|sh<<6|fn));
        internal void Const(int r,uint v){I(15,0,r,(int)(v>>16));I(13,r,r,(int)v);}
        internal void Load(int r,int at,int off,int op){I(op,at,r,off);Word(0);}
        internal void Branch(int op,int rs,int rt,string label){branches.Add((words.Count,label));I(op,rs,rt,0);Word(0);}
        internal void Label(string label)=>labels.Add(label,words.Count);
        internal void Jump(uint pc){Word(0x08000000|((pc>>2)&0x3FFFFFF));Word(0);}
        internal byte[] Finish(){foreach(var(at,label)in branches){int delta=labels[label]-at-1;if(delta<short.MinValue||delta>short.MaxValue)throw new InvalidDataException("Guard branch overflow.");words[at]|=(ushort)(short)delta;}
            byte[] b=new byte[words.Count*4];for(int i=0;i<words.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i*4),words[i]);return b;}
    }
}
