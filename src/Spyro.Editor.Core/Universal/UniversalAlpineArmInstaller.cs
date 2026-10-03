using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
namespace Spyro.Editor.Core.Universal;

/// <summary>One complete original Alpine0/1 or24/25/26 cohort over retained original Blowhard.
/// Authenticates source, retained native owners and actual allocations; no final
/// image hash or fixed authored pose grants admission.</summary>
internal static class UniversalAlpineArmInstaller
{
    internal static bool Applies(UniversalRowTemplate t)=>string.Equals(t.SourceLevelKey,"alpineridge",StringComparison.OrdinalIgnoreCase)&&t.SourceRowIndex is 0 or 1 or 24 or 25 or 26;
    internal static bool Requested(IReadOnlyList<UniversalMobyPlacement> p)=>p.Any(x=>!x.Retired&&string.Equals(x.SourceLevelKey,"alpineridge",StringComparison.OrdinalIgnoreCase)&&x.SourceRowIndex is 0 or 1 or 24 or 25 or 26);
    private static int I(byte[] b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static void Require(bool ok,string reason){if(!ok)throw new InvalidDataException("Alpine armored druid group: "+reason);}
    internal static UniversalComposeResult Compose(LevelDefinition level,byte[] hostBytes,byte[] hostOverlayBytes,
        byte[] originalExe,Func<string,(LevelDefinition Level,UniversalLevelData Data,UniversalOverlay Overlay)> get,
        IReadOnlySet<int> shared,IEnumerable<(long Offset,int Length)> protectedVram,
        IReadOnlyList<UniversalMobyPlacement> ledger,IReadOnlyCollection<int>? removed,
        Func<NativePlayerAnimationSource>? animations,byte[]? sharedEntry,int? nativePrefix,int padding)
    {
        Require(level.Key=="blowhard","this group currently needs the original Blowhard level.");
        Require(removed is not {Count:>0}&&padding==0&&nativePrefix is null or 87,
            "keep Blowhard's original objects and terrain; edited or cleared hosts are not supported for this group yet.");
        Require(Convert.ToHexStringLower(SHA256.HashData(originalExe))=="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998","this group currently needs the unchanged game executable; export other level changes separately.");
        UniversalAlpineArmNative.RequireNative(originalExe);
        Require(sharedEntry!=null,"the export must supply the original shared reward resources (WAD entry8).");
        Require(Convert.ToHexStringLower(SHA256.HashData(sharedEntry!))=="b73da4852629d2035f9a1f6541f0ebda45ce61762f8b09ec62d8fe293f5e1f86","shared reward/digit resources changed.");
        HashSet<int> originalShared=[];
        for(int f=4;f+8<=0x800;f+=8)if(BinaryPrimitives.ReadUInt32LittleEndian(sharedEntry!.AsSpan(f))!=0)originalShared.Add(I(sharedEntry!,f+4));
        Require(originalShared.SetEquals(shared),"shared model namespace differs from its original resource owner.");
        var donor=get("alpineridge");var host=get("blowhard");
        UniversalAlpineArmResources.ProveSource(donor.Data,donor.Overlay);
        Require(hostBytes.AsSpan().SequenceEqual(host.Data.Bytes)&&hostOverlayBytes.AsSpan().SequenceEqual(host.Overlay.Bytes),
            "keep Blowhard's original objects and terrain; retained edits need a separate native owner.");
        Require(Convert.ToHexStringLower(SHA256.HashData(hostBytes))==UniversalAlpineArmArenaStorage.HostHash&&
            Convert.ToHexStringLower(SHA256.HashData(hostOverlayBytes))=="1ee0030607139491b6d076c389837d5b6c0a38e87d324fec2e14b58b47f95aef","original Blowhard data/callback owner changed.");
        Require(ledger.Count>0,"no group was supplied.");
        if(ledger.Any(x=>x.PersistentSlot!=null))Require(ledger.All(x=>x.PersistentSlot!=null&&!string.IsNullOrWhiteSpace(x.EditorId))&&
            ledger.Select(x=>x.EditorId).Distinct(StringComparer.Ordinal).Count()==ledger.Count&&
            ledger.Select(x=>x.PersistentSlot!.Value).Order().SequenceEqual(Enumerable.Range(0,ledger.Count)),"saved placements require unique IDs and contiguous lifetime slots.");
        var order=Enumerable.Range(0,ledger.Count).Where(i=>!ledger[i].Retired).OrderBy(i=>ledger[i].PersistentSlot??i).ToArray();
        var active=order.Select(i=>ledger[i]).ToArray();
        Require(active.Length is 2 or 3&&active.All(x=>x.SourceLevelKey=="alpineridge"),"place one complete original Alpine arm group on its own.");
        var members=UniversalAlpineArmResources.Members(active[0].SourceRowIndex);int placed=members.Length,nativeEnd=87+placed;
        Require(active.Select(x=>x.SourceRowIndex).Order().SequenceEqual(members),"place the complete original Alpine group on its own.");
        Require(!string.IsNullOrWhiteSpace(active[0].LinkGroupId)&&active.All(x=>x.LinkGroupId==active[0].LinkGroupId),"all actors must retain the same placement group.");
        Require(ledger.All(x=>87+(x.PersistentSlot??Array.IndexOf(ledger.ToArray(),x))<256),"saved placement lifetimes have exhausted the native 256-row namespace.");
        var templates=active.Select(x=>UniversalAlpineArmResources.Rebase(donor.Data,donor.Overlay,donor.Data.CaptureRow("alpineridge",x.SourceRowIndex),x)).ToArray();
        foreach(var t in templates)UniversalAlpineArmResources.RequireGroup(t,templates);
        long[] delta=[(long)active[0].X-I(templates[0].Row,12),(long)active[0].Y-I(templates[0].Row,16),(long)active[0].Z-I(templates[0].Row,20)];
        Require(active.Select((x,i)=>Enumerable.Range(0,3).All(a=>(long)new[]{x.X,x.Y,x.Z}[a]-I(templates[i].Row,12+4*a)==delta[a])).All(x=>x),"move all actors together, preserving their original relative pose.");
        Require(active.All(x=>x.X>=8192&&x.X<253952&&x.Y>=8192&&x.Y<253952),"placement leaves the retained native collision grid.");
        templates=templates.Select(t=>UniversalIndexedRows.Resolve(t,templates)).ToArray();
        // Native reserve90 is preserved. One genuine druid emits at most16 arms.
        // Per-source retained reward/fallback/replacement and decimal digits
        // give peak23 for0/1, peak26 for24/25/26, below32 extra lifetimes.
        int peak=16+2*placed+templates.Sum(t=>UniversalTreasureAccounting.KnownValue(t.Row)>=10?2:1);
        Require(peak<=32,"complete source reward/digit/dynamic arm lifetime peak exceeds owned reserve.");
        int reserve=32+(placed*88+111)/112;
        var dest=UniversalAlpineArmArenaStorage.Reserve(host.Data,donor.Data,donor.Overlay,originalExe,reserve,placed);
        UniversalAlpineArmArenaStorage.RequireWritten(host.Data,donor.Data,donor.Overlay,originalExe,reserve,placed,dest);
        var occupied=host.Data.Registrations().Select(r=>r.ClassId).Concat(shared).ToHashSet();
        for(int r=0;r<87;r++)occupied.Add(BinaryPrimitives.ReadUInt16LittleEndian(host.Data.Row(r).AsSpan(54)));
        occupied.UnionWith(UniversalDragonflyEggs.Variants.Select(v=>v.Class));occupied.Add(UniversalDragonflyEggs.SignClass);occupied.Add(UniversalMobyComposer.InertClass);
        var sw=UniversalDispatchAnalysis.FindUpdateSwitch(host.Overlay);
        for(int c=1;c<512;c++)if(UniversalDispatchAnalysis.Handles(host.Overlay,sw,c))occupied.Add(c);
        foreach(byte[] bytes in new[]{originalExe,hostOverlayBytes})for(int f=0;f+4<=bytes.Length;f+=4){uint w=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(f));if(UniversalMips.Op(w) is 9 or 10 or 11 or 12 or 13 or 14&&UniversalMips.Imm(w) is >0 and <512)occupied.Add(UniversalMips.Imm(w));}
        var map=new Dictionary<int,int>();foreach(int c in new[]{0xFD,0x10F,0x13B}){int physical=Enumerable.Range(1,0x1FD).Reverse().FirstOrDefault(c=>!occupied.Contains(c));Require(physical>0,"no independently unused physical model class remains.");map.Add(c,physical);occupied.Add(physical);}
        var exe=originalExe.ToArray();var packages=map.Select(x=>(Source:x.Key,Physical:x.Value,Package:donor.Data.Package(x.Key))).ToArray();
        var vram=new UniversalVramAllocator(UniversalAlpineArmTextureOwners.Protected(host.Data).Concat(protectedVram));int freeBefore=vram.FreeBytes;
        var textures=UniversalAssetImport.ImportTexturesBatch(packages.Select(x=>(x.Package,donor.Data.Vram().ToArray(),$"Alpine class{x.Source:X3}")).ToArray(),dest.Vram(),vram);
        List<UniversalClassImport> imports=[];
        for(int n=0;n<packages.Length;n++){var p=packages[n];var sounds=UniversalAssetImport.ImportSounds(p.Package,donor.Data,dest,exe);Require(sounds.SlotsSilenced==0,"source sound could not fit; no silent replacement is admitted.");imports.Add(new(p.Source,"alpineridge",true,true,p.Package.Length,textures[n],sounds,p.Source==0x13B){DestinationClassId=p.Physical});}
        dest.AppendModels(packages.Select(x=>(x.Physical,x.Package)).ToArray());
        var cohort=UniversalAlpineArmWorldCalls.Build(donor.Data,donor.Overlay,originalExe,map);
        var damage=UniversalNativePlayerDamageCalls.AnalyzeSource("alpineridge",donor.Overlay,cohort.Update,originalExe);
        var writes=UniversalPlayerDamageWrites.AnalyzeOverlay(donor.Overlay,new SortedSet<uint>(cohort.Update.Items.Where(i=>i.Word!=null&&(i.SourcePc??i.Orig)!=null).Select(i=>(i.SourcePc??i.Orig)!.Value))).Writes;
        uint mask=writes.Aggregate(damage.DamageMask,(m,w)=>m|w.DamageMask);
        var ids=packages.SelectMany(p=>UniversalPlayerDamageDependencies.Analyze(donor.Data.Package(p.Source),originalExe).AnimationIds).Concat(UniversalPlayerDamageDependencies.MapDamageMask(mask,originalExe)).Distinct().Order().ToArray();
        Require(animations!=null,"the original Spyro reaction resources must be supplied by the export.");var animationSource=animations!();
        var local=animationSource.Levels.Single(l=>l.LevelId==donor.Level.LevelId).Stream.Records.Select(r=>r.AnimationId).ToHashSet();List<UniversalPlayerAnimationImport> reactions=[];
        foreach(var g in ids.GroupBy(id=>local.Contains(id)?donor.Level.LevelId:10)){var merged=UniversalPlayerAnimationImporter.Merge(dest,g.Key==10?"artisans":"alpineridge",g.Key,g.ToArray(),animationSource,exe);dest=merged.Data;reactions.Add(merged.Report);}
        UniversalAlpineArmDeathSound.Require(donor.Data,donor.Overlay,originalExe,host.Data,dest);
        var rows=templates.Select((t,n)=>{var row=t.Row.ToArray();for(int a=0;a<3;a++)BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(12+4*a),new[]{active[n].X,active[n].Y,active[n].Z}[a]);BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(54),(ushort)map[t.ClassId]);row[74]=255;return(Row:row,Template:t);}).ToArray();
        int treasure=templates.Sum(t=>UniversalTreasureAccounting.KnownValue(t.Row));UniversalTreasureAccounting.ApplyDelta(exe,level.LevelId,treasure);
        var appended=dest.AppendRows(rows);UniversalAlpineArmResources.AppendPods(dest,donor.Data,donor.Overlay,rows,appended.NewRowIndices);
        Require(appended.NewRowIndices.SequenceEqual(Enumerable.Range(87,placed)),"intact native physical prefix changed.");
        foreach(var t in templates)foreach(var link in t.SourceRowIndices){int owner=Array.IndexOf(templates,t);int target=Array.FindIndex(templates,x=>x.SourceRowIndex==link.SourceRow);Require(I(dest.CaptureRow("written",appended.NewRowIndices[owner]).Properties,link.Offset)==appended.NewRowIndices[target],"written reciprocal link differs from actual source identity allocation.");}
        UniversalAlpineArmTextureOwners.RequireWritten(host.Data,dest);
        UniversalAlpineArmPoofOwners.Require(donor.Data,donor.Overlay,originalExe,host.Data,host.Overlay,dest);
        UniversalAlpineArmPoofOwners.RequireCalls(cohort.Update,donor.Data,donor.Overlay,originalExe,map);
        uint address=(host.Overlay.CopyBuffer+7)&~7u;var joined=UniversalAlpineArmQuarantine.Build(donor.Data,donor.Overlay,host.Overlay,originalExe,map,address);
        uint copy=(address+(uint)joined.Code.Length+15)&~15u;byte[] overlay=new byte[UniversalLevelData.Align(checked((int)(copy-UniversalOverlay.Base)),2048)];
        host.Overlay.Bytes[..checked((int)(host.Overlay.CopyBuffer-UniversalOverlay.Base))].CopyTo(overlay);joined.Code.CopyTo(overlay,checked((int)(address-UniversalOverlay.Base)));
        UniversalExeBindings.Patch(exe,host.Overlay.Bindings[UniversalExeBindings.UpdateGlobal],joined.Labels["quarantine_wrap_update"]);UniversalExeBindings.Patch(exe,host.Overlay.Bindings[UniversalExeBindings.InitGlobal],joined.Labels["quarantine_wrap_init"]);UniversalExeBindings.Patch(exe,host.Overlay.Bindings[UniversalExeBindings.CopyBufferGlobal],copy);
        UniversalAlpineArmRetainedRewardOwners.Require(donor.Data,donor.Overlay,host.Data,host.Overlay,dest,originalExe,exe,sharedEntry!,sharedEntry!,overlay,map,address,joined.Code,joined.Labels,templates.Select(t=>t.SourceRowIndex).ToArray());
        int[] save=Enumerable.Range(0,nativeEnd).ToArray();for(int n=0;n<placed;n++)save[87+n]=87+(active[n].PersistentSlot??order[n]);
        if(save.Where((logical,physical)=>logical!=physical).Any())
        {var mapped=NativeMobySaveMap.Append(host.Overlay,overlay,joined.Labels["quarantine_wrap_update"],copy,save);overlay=mapped.Overlay;copy=mapped.CopyBuffer;NativeMobySaveMap.PatchExe(exe);var currentBindings=UniversalExeBindings.Read(exe,level.LevelId);UniversalExeBindings.Patch(exe,currentBindings[UniversalExeBindings.UpdateGlobal],mapped.UpdatePointer);UniversalExeBindings.Patch(exe,currentBindings[UniversalExeBindings.CopyBufferGlobal],copy);}
        uint before=UniversalMobyComposer.RuntimeEnd(host.Data,host.Overlay.CopyBuffer),end=UniversalMobyComposer.RuntimeEnd(dest,copy),limit=UniversalMobyComposer.NormalBufferFloor-UniversalMobyComposer.RamSafetyMargin;
        int modelGrowth=packages.Sum(x=>x.Package.Length)+reactions.Sum(x=>x.ModelGrowthBytes),sceneGrowth=dest.Subfiles()[3].Length-host.Data.Subfiles()[3].Length,codeGrowth=checked((int)(copy-host.Overlay.CopyBuffer));
        var budget=new UniversalRamBudget(before,end,UniversalMobyComposer.NormalBufferFloor,(int)(UniversalMobyComposer.NormalBufferFloor-before),(int)((long)UniversalMobyComposer.NormalBufferFloor-end),codeGrowth,modelGrowth,sceneGrowth,checked((int)((long)end-before))-codeGrowth-modelGrowth-sceneGrowth);
        if(end>limit)throw new UniversalBudgetException("Blowhard does not have enough game memory for the complete Alpine group and its dynamic arms.",budget);
        int arenaBytes=dest.Scene().ArenaEnd-dest.Scene().RowsOffset-nativeEnd*88;int usable=(arenaBytes-73)/112;
        Require(usable>=90+32&&nativeEnd+usable<256,"complete cohort/native dynamic reserve does not fit the row/cache namespace.");
        int[] physicalRows=Enumerable.Repeat(-1,ledger.Count).ToArray(),saveRows=physicalRows.ToArray();for(int n=0;n<placed;n++){physicalRows[order[n]]=87+n;saveRows[order[n]]=save[87+n];}
        return new(level.Key,imports,physicalRows,budget,usable,freeBefore-vram.FreeBytes,[new("alpineridge",new[]{0xFD,0x10F,0x13B},cohort.Update.Items.Count(i=>i.Word!=null)*4,cohort.Constructors.Sum(p=>p.Items.Count(i=>i.Word!=null)*4),cohort.Update.Tables.Count+cohort.Constructors.Sum(p=>p.Tables.Count)+cohort.World.Tables.Count){PlayerDamageMask=mask,PlayerDamageWrites=writes,NativePlayerDamageCalls=damage.Calls}],[],treasure,overlay,dest.Bytes,exe)
        {ParticleDependencies=new[]{11,72}.Select(id=>new UniversalParticleDependency("alpineridge",id,"native-equivalent","Exact particle byte0 class and retained Blowhard updater/resource ownership; byte1 is render mode, not particle class. Native poof call second arguments24/70 are preserved.")).ToArray(),ImportedVramRanges=vram.ImportedReservations,RestoredIndexedObjectLinks=templates.Sum(t=>t.ImportedRowIndices.Count),PlacementSaveRows=saveRows,PhysicalToLogicalSaveRows=save,NativeRowsBefore=87,NativeRowsAfter=nativeEnd,PlayerAnimationImports=reactions};
    }
}
