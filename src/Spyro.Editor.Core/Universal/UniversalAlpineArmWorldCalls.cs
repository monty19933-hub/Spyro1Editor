using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Private complete copied world-call storage prerequisite. This owns
/// the actual source caller/control graph and native O32/cache/grid/path copies;
/// it does not issue the contact, parent trajectory, player response or capacity
/// premises required for ordinary gameplay admission.</summary>
internal static class UniversalAlpineArmWorldCalls
{
    internal const uint Cache = 0x800700F4;
    internal const int CacheBytes = 1024;
    internal static readonly (uint Pc,uint Target)[] Calls = [
        (0x80082818,0x80039398),(0x800828FC,0x80039398),(0x80082A28,0x80039E94),
        (0x80082AE0,0x80039688),(0x800833C4,0x80039398),(0x80083548,0x80039398),
        (0x80085130,0x80039E94),(0x80085324,0x80039E94),(0x80085758,0x80039E94),
        (0x8008593C,0x80039398),(0x80085B84,0x80039398),(0x80085CE0,0x80039398),
        (0x80085DCC,0x80039910),(0x800861CC,0x80039688),(0x8008639C,0x80039688),
        (0x800864A8,0x80039398),(0x800879A4,0x80039688)];
    private static void Require(bool ok,string why)
    { if(!ok)throw new InvalidDataException("Alpine world-call storage: "+why); }
    internal static string Caller(uint pc)=>$"alpine_cohort_world_call_{pc:X8}";
    internal static string Wrapper(uint target)=>target==0x80039E94 ? UniversalAlpineArmPathFollowing.WrapperLabel : $"alpine_cohort_world_wrap_{target:X8}";
    internal static void RequireHost(string key,UniversalLevelData data,UniversalOverlay overlay,byte[] exe)
    {
        UniversalAlpineArmNative.RequireNative(exe);
        string H(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
        Require(key=="darkhollow"&&H(data.Bytes)=="a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97"&&H(overlay.Bytes.ToArray())=="9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32", "this private cache profile requires intact original Dark Hollow geometry, objects and assets");
    }
    internal static (UniversalAsmPart Update,IReadOnlyList<UniversalAsmPart> Constructors,UniversalAsmPart World) Build(
        UniversalLevelData data,UniversalOverlay source,byte[] exe,IReadOnlyDictionary<int,int> map)
    {
        var cohort=UniversalAlpineArmNative.BuildCohort(data,source,exe,map);
        // Full native/source hashes and complete class closures bind every actual
        // predecessor, argument, delay slot, cross-class producer and table. The
        // actual copied call inventory is then independently derived and checked.
        var actual=cohort.Update.Items.Where(i=>i.Word is uint w&&UniversalMips.IsJal(w)&&
            Calls.Any(c=>UniversalMips.BranchTarget(w,i.SourcePc??i.Orig??0)==c.Target))
            .Select(i=>(Pc:i.SourcePc??i.Orig??0,Target:UniversalMips.BranchTarget(i.Word!.Value,i.SourcePc??i.Orig??0))).OrderBy(c=>c.Pc).ToArray();
        Require(actual.SequenceEqual(Calls.OrderBy(c=>c.Pc)),"source movement/path caller inventory changed");
        foreach(var (pc,target) in Calls)
        {
            int at=cohort.Update.Items.FindIndex(i=>(i.SourcePc??i.Orig)==pc);
            Require(at>=0&&cohort.Update.Items[at].Word==UniversalMips.EncodeJ(3,target)&&cohort.Update.Items[at].Fix.Kind==UniversalFixKind.None,"actual retained call has conflicting ownership");
            cohort.Update.Items[at]=cohort.Update.Items[at] with{Fix=new(UniversalFixKind.JalLabel,Label:Wrapper(target))};
            cohort.Update.Items.Insert(at,new(null,default,null,Caller(pc)));
        }
        UniversalAlpineArmBoneBoundary.Bind(cohort.Update,source);
        UniversalAsmPart world=new();
        foreach(uint target in new uint[]{0x80039398,0x80039688,0x80039910})
        { world.Label(Wrapper(target));world.Items.AddRange(MovementWrapper(target)); }
        var path=UniversalAlpineArmPathFollowing.Build();
        world.Items.AddRange(path.Items);world.Items.AddRange(UniversalAlpineArmBoneBoundary.Build().Items);world.Items.AddRange(UniversalAlpineArmMovementBoundary.Build().Items);
        return(cohort.Update,cohort.Constructors,world);
    }
    private static IReadOnlyList<UniversalAsmItem> MovementWrapper(uint target)
    {
        List<UniversalAsmItem> a=[]; const int frame=64;
        void E(uint w,UniversalFix fix=default)=>a.Add(new(w,fix,null,null));
        uint I(int op,int rs,int rt,int imm)=>UniversalMips.EncodeI(op,rs,rt,imm);
        E(I(9,29,29,-frame));E(I(43,29,31,60));
        void Temps(bool save){for(int r=8;r<12;r++)E(I(save?43:35,29,r,32+4*(r-8)));if(!save)E(0);}
        Temps(true);foreach(int off in new[]{16,20}){E(I(35,29,8,frame+off));E(0);E(I(43,29,8,off));}
        void Copy(bool save)
        {
            E(I(15,0,8,UniversalMips.Hi(Cache)));E(I(9,8,8,UniversalMips.Lo(Cache)));
            E(I(15,0,9,0),new(UniversalFixKind.LabelAddrHi,Label:UniversalAlpineArmPathFollowing.ShadowLabel));
            E(I(9,9,9,0),new(UniversalFixKind.LabelAddrLo,Label:UniversalAlpineArmPathFollowing.ShadowLabel));
            E(I(9,0,10,CacheBytes/4));int start=a.Count;
            E(I(35,save?8:9,11,0));E(I(9,save?8:9,save?8:9,4));E(I(43,save?9:8,11,0));E(I(9,save?9:8,save?9:8,4));
            E(I(9,10,10,-1));E(I(5,10,0,start-a.Count-1));E(0);
        }
        Copy(true);Temps(false);E(UniversalMips.EncodeJ(3,target),new(UniversalFixKind.JalLabel,Label:UniversalAlpineArmMovementBoundary.EntryLabel(target)));
        E(0);E(0);Temps(true);Copy(false);Temps(false);E(I(35,29,31,60));E(0);E(I(9,29,29,frame));E(UniversalMips.JrRa);E(0);
        Require(a.Count==58,"complete two-stack-argument wrapper extent changed");return a;
    }
    internal static void RequireCopied(UniversalLevelData data,UniversalOverlay source,byte[] exe,IReadOnlyDictionary<int,int> map,
        UniversalAsmPart update,IReadOnlyList<UniversalAsmPart> constructors,UniversalAsmPart world)
    {
        var expected=Build(data,source,exe,map);
        bool Same(UniversalAsmPart a,UniversalAsmPart b)=>a.LenientTables==b.LenientTables&&a.Items.SequenceEqual(b.Items)&&a.Tables.Count==b.Tables.Count&&b.Tables.All(t=>a.Tables.TryGetValue(t.Key,out var targets)&&t.Value.SequenceEqual(targets));
        Require(Same(update,expected.Update)&&Same(world,expected.World)&&constructors.Count==expected.Constructors.Count&&constructors.Where((p,i)=>!Same(p,expected.Constructors[i])).Any()==false,"complete copied source/argument/native/grid/cache/path/table/label/incoming ownership changed");
    }
    internal static void RequireWritten(UniversalLevelData data,UniversalOverlay source,byte[] exe,IReadOnlyDictionary<int,int> map,
        byte[] code,IReadOnlyDictionary<string,uint> labels,uint codeBase)
    {
        var expected=Build(data,source,exe,map);
        var (bytes,derived)=UniversalBehaviorBundleBuilder.Assemble(new[]{expected.Update}.Concat(expected.Constructors).Append(expected.World).ToList(),codeBase);
        Require(codeBase>=UniversalOverlay.Base&&(ulong)codeBase+(uint)bytes.Length<=0x80200000,"whole private owned fragment allocation leaves PS1 RAM");
        Require(code.AsSpan().SequenceEqual(bytes)&&labels.Count==derived.Count&&derived.All(p=>labels.TryGetValue(p.Key,out uint at)&&at==p.Value),"independently derived final native/source/cache/path/branch/constructor regions changed");
        // No guessed final-image hash: caller/class inputs and independently
        // rebuilt full source graph derive all allowed entry points and labels.
        foreach(var (pc,target) in Calls)
            Require(BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(checked((int)(labels[Caller(pc)]-codeBase))))==UniversalMips.EncodeJ(3,labels[Wrapper(target)]),"written actual source call misses complete wrapper");
    }
}
