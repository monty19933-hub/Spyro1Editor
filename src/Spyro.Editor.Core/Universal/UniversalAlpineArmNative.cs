using System.Security.Cryptography;

namespace Spyro.Editor.Core.Universal;

/// <summary>Exact copied-source ownership prerequisite for the actual Alpine13B
/// updater and its initialized constructor. Does not authorize its callers,
/// destination collision namespace, resource capacity, or player-response paths.</summary>
internal static class UniversalAlpineArmNative
{
    internal static void RequireNative(byte[] exe)
    {
        if(Convert.ToHexStringLower(SHA256.HashData(exe))!="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998")
            throw new InvalidDataException("Alpine13B native lifecycle requires the complete original USA executable.");
    }
    internal static (UniversalAsmPart Update,UniversalAsmPart Init) Build(UniversalLevelData data,UniversalOverlay overlay,byte[] exe,int physicalClass)
    {
        UniversalAlpineArmResources.ProveSource(data,overlay);RequireNative(exe);
        if(physicalClass<0||physicalClass>=512)throw new InvalidDataException("Alpine13B class alias exceeds the certified native g_Models namespace.");
        var update=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(overlay,[0x13B],"alpine_13B_update",new Dictionary<int,int>{{0x13B,physicalClass}}).Part;
        var init=UniversalBehaviorBundleBuilder.BuildSpecializedInit(overlay,[0x13B],"alpine_13B_init").Part;
        UniversalClassRemapping.PatchInitClassStore(init,overlay,0x13B,physicalClass);
        return(update,init);
    }
    internal static void RequireCopied(UniversalLevelData data,UniversalOverlay overlay,byte[] exe,int physicalClass,UniversalAsmPart update,UniversalAsmPart init)
    {
        var expected=Build(data,overlay,exe,physicalClass);Compare(update,expected.Update);Compare(init,expected.Init);
        static void Compare(UniversalAsmPart supplied,UniversalAsmPart expected)
        {
            if(supplied.LenientTables!=expected.LenientTables||!supplied.Items.SequenceEqual(expected.Items)||supplied.Tables.Count!=expected.Tables.Count||
                expected.Tables.Any(t=>!supplied.Tables.TryGetValue(t.Key,out var targets)||!t.Value.SequenceEqual(targets)))
                throw new InvalidDataException("Alpine13B copied lifecycle bytes/source PCs/replay/arguments/tables/labels/incoming entries changed.");
        }
    }
    internal static (UniversalAsmPart Update,IReadOnlyList<UniversalAsmPart> Constructors) BuildCohort(
        UniversalLevelData data,UniversalOverlay source,byte[] exe,IReadOnlyDictionary<int,int> map)
    {
        UniversalAlpineArmResources.ProveSource(data,source);RequireNative(exe);
        if(!map.Keys.Order().SequenceEqual(new[]{0xFD,0x10F,0x13B})||map.Values.Distinct().Count()!=3||map.Values.Any(c=>c<0||c>=512))
            throw new InvalidDataException("Alpine cohort needs three unique bounded physical model bindings.");
        var update=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,[0xFD,0x10F,0x13B],"alpine_cohort_owned",map).Part;
        UniversalAlpineArmModelReads.MarkOwned(update,data,source,exe,map);
        var sw=UniversalDispatchAnalysis.FindUpdateSwitch(source);var all=UniversalDispatchAnalysis.Closure(source,sw.Function,[sw.Function],new HashSet<uint>()).Instructions;
        var actual=UniversalClassRemapping.ConstantSpawns(source,all).Where(c=>c.ClassId==0x13B).ToArray();
        if(!actual.Select(c=>c.CallPc).Order().SequenceEqual(new uint[]{0x80084DB0,0x80085550}))
            throw new InvalidDataException("Alpine actual13B constructor caller ownership changed.");
        // Immediate source literal/global/jalr/actor-delay packets are separately
        // authenticated in ProveSource; general recovery does not issue authority.
        UniversalClassRemapping.PatchConstantSpawns(update,source,map);
        UniversalClassRemapping.RequireNoUnownedClassMemory(update,false,source);
        List<UniversalAsmPart> constructors=[];
        foreach(int c in new[]{0xFD,0x10F,0x13B})
        {
            var init=UniversalBehaviorBundleBuilder.BuildSpecializedInit(source,[c],$"alpine_cohort_ctor_{c:X3}").Part;
            UniversalClassRemapping.PatchInitClassStore(init,source,c,map[c]);
            if(c==map[c])
            {
                // Identity class stores still need explicit ownership. The exact
                // proved allocator/class/parent prefix keeps the input class S0
                // and writes it into its own newly allocated row in this delay.
                var roles=UniversalClassRemapping.ProveInitializerRoles(init,source);
                int at=init.Items.FindIndex(i=>i.Orig==roles.ClassStorePc);
                if(at<0||init.Items[at].Word!=source.Word(roles.ClassStorePc))throw new InvalidDataException("Alpine identity native class store changed.");
                init.Items[at]=init.Items[at] with{ClassReadOwned=true};
            }
            UniversalClassRemapping.PatchInitAllocatedClassReads(init,source,map,c);
            UniversalClassRemapping.PatchInitParentClassReads(init,source,map);
            UniversalClassRemapping.PatchConstantSpawns(init,source,map);
            UniversalClassRemapping.RequireNoUnownedClassMemory(init,true);constructors.Add(init);
        }
        return(update,constructors);
    }
    internal static void RequireCohort(UniversalLevelData data,UniversalOverlay source,byte[] exe,IReadOnlyDictionary<int,int> map,
        UniversalAsmPart update,IReadOnlyList<UniversalAsmPart> constructors)
    {
        var expected=BuildCohort(data,source,exe,map);
        bool Same(UniversalAsmPart a,UniversalAsmPart b)=>a.LenientTables==b.LenientTables&&a.Items.SequenceEqual(b.Items)&&a.Tables.Count==b.Tables.Count&&b.Tables.All(t=>a.Tables.TryGetValue(t.Key,out var targets)&&t.Value.SequenceEqual(targets));
        if(!Same(update,expected.Update)||constructors.Count!=expected.Constructors.Count||constructors.Where((p,i)=>!Same(p,expected.Constructors[i])).Any())
            throw new InvalidDataException("Alpine complete copied cohort source/replay/constructor/model/spawn/label/incoming ownership changed.");
    }
}
