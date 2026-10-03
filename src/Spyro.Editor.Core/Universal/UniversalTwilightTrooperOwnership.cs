using System.Runtime.CompilerServices;

[assembly:InternalsVisibleTo("Spyro.Editor.UniversalTwilightTrooperSmoke")]
namespace Spyro.Editor.Core.Universal;

/// <summary>Exact Twilight edge producers and runtime B0 pointer ownership.</summary>
internal static class UniversalTwilightTrooperOwnership
{
    internal static bool SourceFamily(UniversalOverlay o)=>(o.Bindings.TryGetValue(UniversalExeBindings.CopyBufferGlobal,out var copy)&&copy.LuiAddress==0x8005B5A0&&o.Bindings.TryGetValue(UniversalExeBindings.UpdateGlobal,out var update)&&update.LuiAddress==0x8005B5C0)||Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(o.Bytes))==UniversalTwilightTrooperPlacement.OverlaySHA256;
    internal const uint FlashCall=0x8007FA40,LinkedRead=0x8007F568;
    private static readonly (uint Branch,uint Word,uint Constant)[] FlashEdges=[(0x8007F400,0x1440018C,0x8007F404),(0x8007FA14,0x14400007,0x8007FA18),(0x8007FA2C,0x10400015,0x8007FA30)];
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Twilight trooper ownership: "+why);}
    internal static IReadOnlyList<UniversalClassRemapping.SpawnArgument>? SpawnArguments(UniversalOverlay source,IReadOnlySet<uint> instructions,uint call)
    {
        if(call!=FlashCall||!instructions.Contains(call))return null;
        UniversalTwilightTrooperPlacement.RequireOverlay(source);
        uint[] packet=[0x3C028007,0x8C4258CC,0,0x0040F809,0x02A02821];
        for(int n=0;n<packet.Length;n++)Require(instructions.Contains(0x8007FA34+(uint)n*4)&&source.Word(0x8007FA34+(uint)n*4)==packet[n],"callback/class/parent argument packet changed");
        List<UniversalClassRemapping.SpawnArgument> arguments=[];
        foreach(var edge in FlashEdges)
        {
            if(!instructions.Contains(edge.Branch)&&!instructions.Contains(edge.Constant))continue;
            Require(instructions.Contains(edge.Branch)&&instructions.Contains(edge.Constant)&&source.Word(edge.Branch)==edge.Word&&source.Word(edge.Constant)==0x240401FB,"edge and delay-slot class producer must survive together");
            arguments.Add(new(FlashCall,edge.Constant,0x1FB));
        }
        Require(arguments.Count>0,"shared callback lost every authenticated class producer");
        return arguments;
    }
    internal static void AddDependencies(UniversalOverlay source,SortedSet<uint> instructions,SortedSet<int> spawned)
    {if(SpawnArguments(source,instructions,FlashCall)!=null)spawned.Add(0x1FB);}
    internal static void AddParticles(UniversalOverlay source,SortedSet<uint> instructions,SortedSet<int> particles)
    {
        if(!instructions.Contains(0x80081A88))return;
        UniversalTwilightTrooperPlacement.RequireOverlay(source);
        foreach(var owner in new[]{(0x80080630u,0x2404000Au),(0x80080634u,0x0802069Eu),(0x80080638u,0x24050046u),(0x80081A88u,0x0040F809u),(0x80081A8Cu,0x24070010u)})Require(instructions.Contains(owner.Item1)&&source.Word(owner.Item1)==owner.Item2,"B0 shared explosion70 request lost exact branch/delay/count/argument owner");
        particles.Add(70);
    }
    // Run before any class/particle/gem adapter changes the actor body. Expected
    // source closure binds producers, all props38 writes, paths and rectangles.
    internal static void RequireCopiedInputs(UniversalAsmPart part,UniversalOverlay source,IReadOnlyList<int> classes,IReadOnlyDictionary<int,int> map,byte[] exe)
    {
        if(!classes.Any(c=>c is 0x93 or 0x94))return;
        UniversalTwilightTrooperPlacement.RequireOverlay(source);UniversalTwilightTrooperPlacement.RequireNative(exe);
        string tag=part.Items.First(i=>i.Label!=null).Label!;
        var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,classes,tag,map).Part;
        Require(part.LenientTables==expected.LenientTables&&part.Items.SequenceEqual(expected.Items)&&part.Tables.Count==expected.Tables.Count&&expected.Tables.All(t=>part.Tables.TryGetValue(t.Key,out var got)&&got.SequenceEqual(t.Value)),"complete copied native actor/state/argument/path/pointer/incoming schema changed");
        var instructions=part.Items.Where(i=>i.Word!=null&&(i.Orig??i.SourcePc)!=null).Select(i=>(i.Orig??i.SourcePc)!.Value).ToHashSet();
        _=SpawnArguments(source,instructions,FlashCall);
    }
    internal static void RequireInitializer(UniversalAsmPart part,UniversalOverlay source,int sourceClass,byte[] exe,IReadOnlyList<int>? selectedClasses=null)
    {
        UniversalTwilightTrooperPlacement.RequireOverlay(source);UniversalTwilightTrooperPlacement.RequireNative(exe);
        var selected=selectedClasses??[sourceClass];Require(selected.Contains(sourceClass)&&selected.Distinct().Count()==selected.Count,"constructor class membership changed");
        string tag=part.Items.First(i=>i.Label!=null).Label!;var expected=UniversalBehaviorBundleBuilder.BuildSpecializedInit(source,selected,tag).Part;
        Require(part.Items.SequenceEqual(expected.Items)&&part.LenientTables==expected.LenientTables&&part.Tables.Count==expected.Tables.Count&&expected.Tables.All(t=>part.Tables.TryGetValue(t.Key,out var got)&&got.SequenceEqual(t.Value)),"complete copied allocator/class/parent/default init/model activation schema changed");
        var roles=UniversalClassRemapping.ProveInitializerRoles(part,source);
        Require(roles.ClassRegister==16&&roles.ActorRegister==19&&roles.ParentRegister==21,"native constructor lost original class/allocated actor/parent register ownership");
    }
    internal static IReadOnlyList<uint> TranslateLinkedRead(UniversalAsmPart part,UniversalOverlay source,IReadOnlyList<int> classes,IReadOnlyDictionary<int,int> map)
    {
        if(!classes.Contains(0x94)||!map.Any(m=>m.Key!=m.Value))return [];
        UniversalTwilightTrooperPlacement.RequireOverlay(source);
        // Constructor callback returns a native allocated/classed B0 actor. Source
        //94 stores that v0 in props38; the only other stores clear0 or sentinel-1.
        UniversalNativeLinkedActorOwnership.RequirePacket(part,source,0x8007FABC,[0x1440000C,0x240400B0,0x3C028007,0x8C4258CC,0,0x0040F809,0x02A02821,0x24030028,0xAE220038]);
        UniversalNativeLinkedActorOwnership.RequirePacket(part,source,0x8007F550,[0x8E240038,0,0x10800014,0x2402FFFF,0x10820012,0x240200B0,0x84830036,0,0x1462000E,0,0x90820049,0,0x1440000A,0,0x90820048,0,0x2C420080,0x10400005,0x24020001,0xA0820049,0x8E220038,0x24090002,0xA0490048]);
        var insns=part.Items.Where(i=>i.Word!=null&&(i.Orig??i.SourcePc)!=null).Select(i=>(i.Orig??i.SourcePc)!.Value).ToHashSet();
        Require(UniversalClassRemapping.ComparisonOnly(source,insns,LinkedRead+4,3,map.Keys.Append(0xB0).Append(0).Distinct().ToArray()),"linked B0 class escaped its native comparison");
        foreach(int at in Enumerable.Range(0,part.Items.Count).Where(n=>(part.Items[n].Orig??part.Items[n].SourcePc)==LinkedRead).OrderDescending())UniversalClassRemapping.TranslateOwnedClassification(part,at,map,"twilight_b0");
        return [LinkedRead];
    }
}
