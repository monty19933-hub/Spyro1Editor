using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Spyro.Editor.Core.Universal;

/// <summary>Private native path ownership prerequisite for the original selector1 chain.
/// This helper does not issue selector pruning or admit the missing13B resource.</summary>
internal static class UniversalMagicChainPathFollowing
{
    internal const uint SourceCall = 0x800875F8, Entry = 0x80039E94, End = 0x8003A16C;
    internal const string EntryLabel = "magic_chain_native_path", WrapperLabel = "magic_chain_path_shadow",
        ShadowLabel = "magic_chain_shadow_data", CallLabel = "magic_chain_source_path_call";
    private const int Frame = 80, CacheBytes = 1024;
    private sealed record Function(uint Entry, uint End, string SHA256, IReadOnlyList<uint> Words);
    private static string H(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool ok, string why)
    { if (!ok) throw new InvalidDataException("Magic native path owner: " + why); }
    private static string PC(uint pc) => $"magic_chain_path_pc_{pc:X8}";
    private static Function Load()
    {
        using var s = typeof(UniversalMagicChainPathFollowing).Assembly.GetManifestResourceStream("Spyro.Editor.Core.Universal.PrivateMagicPathFollowingSource.json")
            ?? throw new InvalidDataException("Missing original native path schema");
        using var b = new MemoryStream(); s.CopyTo(b); var bytes = b.ToArray();
        Require(H(bytes) == "59e4c4b98b5e32e281d785e5e15a9e706ea5aa6256244fc3c06bcfceecb2f2d4", "complete immutable schema changed");
        var f = JsonSerializer.Deserialize<Function>(bytes) ?? throw new InvalidDataException("Invalid native path schema");
        Require(f.Entry == Entry && f.End == End && f.Words.Count * 4 == End - Entry, "whole native function extent changed");
        byte[] packet = new byte[f.Words.Count * 4];
        for (int i = 0; i < f.Words.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(i * 4), f.Words[i]);
        Require(H(packet) == "87bf3554f489b3b84623027b279df666cc11c7d0b022f5dfc9fe3fde4c6c1f43" && f.SHA256 == H(packet), "whole native path packet changed");
        return f;
    }
    internal static void RequireSource(UniversalLevelData data, UniversalOverlay source, byte[] executable)
    {
        UniversalMagicCraftersDruidIndexedRows.ProveSource("magiccrafters", data, source);
        Require(H(executable) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998", "original native executable/incoming owner changed");
        var f = Load();
        for (int i = 0; i < f.Words.Count; i++)
            Require(BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan(checked((int)(Entry - 0x8000F800)) + i * 4)) == f.Words[i], "native argument/branch/store/return producer changed");
        // Full overlay authentication includes callers, descriptors, jump tables and
        // cross-class producers. This packet additionally binds the actual O32 ABI.
        uint[] packet = [0x8E220038,0,0x1440050C,0x02602021,0x24060100,0x24090010,0x24020080,
            0xAFA20018,0x240200FF,0xAFA2001C,0x24020005,0xAFA00010,0xAFA90014,0xAFA20020,
            0x8E250000,0x0C00E7A5,0x2407008C,0x24030100,0x144304FC,0x02602021,
            0x8E22003C,0xAE3E0038,0xAC5E0000,0xAE200014];
        for (int i = 0; i < packet.Length; i++) Require(source.Word(0x800875BC + (uint)i * 4) == packet[i], "placed selector1 path/flag/selector packet changed");
    }
    internal static UniversalAsmPart Build()
    {
        var f = Load(); UniversalAsmPart part = new();
        part.Label(WrapperLabel); part.Items.AddRange(Wrapper());
        part.Label(EntryLabel);
        for (int i = 0; i < f.Words.Count; i++)
        {
            uint pc = Entry + (uint)i * 4, word = f.Words[i]; part.Label(PC(pc)); UniversalFix fix = default;
            if (UniversalMips.IsPcRelative(word) || UniversalMips.Op(word) == 2)
            {
                uint target = UniversalMips.BranchTarget(word, pc);
                Require(target >= Entry && target < End, "direct path transfer leaves whole function");
                fix = new(UniversalMips.Op(word) == 2 ? UniversalFixKind.JumpLabel : UniversalFixKind.BranchLabel, Label: PC(target));
            }
            Require(!UniversalMips.IsJalr(word) && (!UniversalMips.IsJr(word) || word == UniversalMips.JrRa), "native path acquires an unowned indirect transfer");
            if (pc == 0x8003A068)
            {
                Require(word == UniversalMips.EncodeJ(3, 0x80039398), "exact inner movement call changed");
                fix = new(UniversalFixKind.JalLabel, Label: UniversalMagicPairMovementBoundary.EntryLabel(0x80039398));
            }
            part.Emit(word, fix);
        }
        part.Label(ShadowLabel); for (int i = 0; i < CacheBytes / 4; i++) part.Emit(0);
        return part;
    }
    private static IReadOnlyList<UniversalAsmItem> Wrapper()
    {
        List<UniversalAsmItem> a = [];
        void Emit(uint word, UniversalFix fix = default) => a.Add(new(word,fix,null,null));
        uint I(int op,int rs,int rt,int imm) => UniversalMips.EncodeI(op,rs,rt,imm);
        Emit(I(9,29,29,-Frame)); Emit(I(43,29,31,76));
        void Temps(bool save) { for (int r=8;r<12;r++) Emit(I(save?43:35,29,r,40+4*(r-8))); if(!save) Emit(0); }
        Temps(true);
        // Native path has FIVE stacked arguments; the pair's two-argument movement
        // wrapper is not interchangeable. O32 argument home area remains0..15.
        for(int off=16;off<=32;off+=4) { Emit(I(35,29,8,Frame+off));Emit(0);Emit(I(43,29,8,off)); }
        void Copy(bool save)
        {
            Emit(I(15,0,8,UniversalMips.Hi(UniversalMagicPairCollisionShadow.Cache)));
            Emit(I(9,8,8,UniversalMips.Lo(UniversalMagicPairCollisionShadow.Cache)));
            Emit(I(15,0,9,0),new(UniversalFixKind.LabelAddrHi,Label:ShadowLabel));
            Emit(I(9,9,9,0),new(UniversalFixKind.LabelAddrLo,Label:ShadowLabel));
            Emit(I(9,0,10,CacheBytes/4));int start=a.Count;
            Emit(I(35,save?8:9,11,0)); Emit(I(9,save?8:9,save?8:9,4));
            Emit(I(43,save?9:8,11,0)); Emit(I(9,save?9:8,save?9:8,4));
            Emit(I(9,10,10,-1)); Emit(I(5,10,0,start-a.Count-1)); Emit(0);
        }
        Copy(true);Temps(false);Emit(UniversalMips.EncodeJ(3,Entry),new(UniversalFixKind.JalLabel,Label:EntryLabel));
        Emit(0);Emit(0);Temps(true);Copy(false);Temps(false);Emit(I(35,29,31,76));Emit(0);
        Emit(I(9,29,29,Frame));Emit(UniversalMips.JrRa);Emit(0);
        Require(a.Count==67,"complete five-argument path wrapper extent changed");return a;
    }
    internal static void BindSource(UniversalAsmPart part, UniversalOverlay source)
    {
        Require(H(source.Bytes.ToArray())==UniversalMagicCraftersDruidIndexedRows.OverlaySha256,"source overlay changed before copied path binding");
        for(uint pc=0x800875BC;pc<=0x80087618;pc+=4)
        {
            var items=part.Items.Where(i=>i.Orig==pc).ToArray();
            Require(items.Length==1&&items[0].Word==source.Word(pc),"copied original path/argument/flag/selector instruction disappeared or changed");
            if(pc==SourceCall)Require(items[0].Fix.Kind==UniversalFixKind.None,"owned path call already redirected by another owner");
        }
        int at=part.Items.FindIndex(i=>i.Orig==SourceCall);
        part.Items[at]=part.Items[at] with{Fix=new(UniversalFixKind.JalLabel,Label:WrapperLabel)};
        part.Items.Insert(at,new(null,default,null,CallLabel));
    }
    internal static void RequireWritten(byte[] code,IReadOnlyDictionary<string,uint> labels,uint codeBase)
    {
        bool hasWrapper=labels.TryGetValue(WrapperLabel,out uint at),hasCall=labels.TryGetValue(CallLabel,out uint call);
        Require(hasWrapper&&hasCall,"copied path caller/owner labels absent");
        var expectedPart=Build();
        Require(labels.TryGetValue(UniversalMagicPairMovementBoundary.EntryLabel(0x80039398),out uint movement),"copied guarded inner movement label absent");
        for(int i=0;i<expectedPart.Items.Count;i++)
            if(expectedPart.Items[i].Fix.Label==UniversalMagicPairMovementBoundary.EntryLabel(0x80039398))
                expectedPart.Items[i]=expectedPart.Items[i] with{Word=UniversalMips.EncodeJ(3,movement),Fix=default};
        var (expected,expectedLabels)=UniversalBehaviorBundleBuilder.Assemble([expectedPart],at);
        Require(at>=codeBase&&(ulong)at+(uint)expected.Length<=(ulong)codeBase+(uint)code.Length,"whole path/shadow allocation is outside the bundle");
        Require(expectedLabels.All(p=>labels.TryGetValue(p.Key,out uint actual)&&actual==p.Value),"native path/branch/argument/shadow label changed");
        Require(code.AsSpan(checked((int)(at-codeBase)),expected.Length).SequenceEqual(expected),"complete copied path wrapper/native/schema changed");
        uint Word(uint pc) { Require(pc>=codeBase&&(ulong)pc+4<=(ulong)codeBase+(uint)code.Length,"caller outside bundle");return BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(checked((int)(pc-codeBase)))); }
        Require(Word(call)==UniversalMips.EncodeJ(3,at),"source call does not enter its complete owned wrapper");
        uint end=at+(uint)expected.Length;
        for(int off=0;off<code.Length;off+=4)
        {
            uint pc=codeBase+(uint)off;if(pc>=at&&pc<end)continue;uint word=Word(pc);
            if(UniversalMips.IsPcRelative(word)||UniversalMips.Op(word) is 2 or 3)
            {
                uint target=UniversalMips.BranchTarget(word,pc);
                if(target>=at&&target<end)Require(pc==call&&UniversalMips.IsJal(word)&&target==at,"outside copied transfer enters path/argument/shadow interior");
            }
            Require(!(word>=at&&word<end),"outside copied pointer/table names native path/shadow interior");
            if(UniversalMips.Op(word)==15)
                for(int j=1;j<=4&&off+j*4<code.Length;j++)
                {
                    uint low=Word(pc+(uint)j*4);
                    if(UniversalMips.Rs(low)!=UniversalMips.Rt(word)||UniversalMips.Op(low) is not(9 or 13))continue;
                    uint address=UniversalMips.Op(low)==9?unchecked((word&65535)<<16)+(uint)UniversalMips.SImm(low):((word&65535)<<16)|(low&65535);
                    Require(!(address>=at&&address<end),"outside materialized address names native path/shadow interior");
                }
        }
    }
}
