using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;

public sealed record UniversalMagicDruidChainReadback(uint Base,int Bytes,string SHA256,IReadOnlyList<int> SourceRows)
{
    public string Scope {get;init;}="PRIVATE original Magic2/3/4/5 selector1→0, retained original Dark Hollow, common translation/native yaw, one complete source pod/flag/path group; native evidence pending";
}

/// <summary>Private aggregate issuer for one original four-druid chain. Admission
/// follows structural owners and authored inputs, not a fixed final image/pose.
/// This authority never admits source selectors4/10 or a genuine13B spawn.</summary>
public sealed class UniversalMagicDruidChainCertificate
{
    private readonly UniversalLevelData _source,_host;
    private readonly UniversalOverlay _overlay,_hostOverlay;
    private readonly byte[] _exe;
    private readonly UniversalMobyPlacement[] _placements;
    private readonly UniversalRowAllocation _allocation;
    private byte[]? _code;private Dictionary<string,uint>? _labels;private uint _base;
    private UniversalMagicDruidChainCertificate(UniversalLevelData source,UniversalOverlay overlay,byte[] host,byte[] hostOverlay,byte[] exe,IReadOnlyList<UniversalMobyPlacement> placements)
    { _source=new(source.Bytes.ToArray());_overlay=new(overlay.Bytes.ToArray(),overlay.Bindings.ToDictionary());_host=new(host.ToArray());_exe=exe.ToArray();_hostOverlay=new(hostOverlay.ToArray(),UniversalExeBindings.Read(exe,12));_placements=placements.Where(p=>!p.Retired).OrderBy(p=>p.PersistentSlot??placements.ToList().IndexOf(p)).ToArray();_allocation=UniversalRowRemoval.Allocate(_host,[],4); }
    private static int I(byte[] b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static string H(byte[] b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Magic four-druid chain: "+why);}
    internal static UniversalMagicDruidChainCertificate Capture(string destination,byte[] host,byte[] hostOverlay,byte[] exe,UniversalLevelData source,UniversalOverlay overlay,IReadOnlyList<UniversalMobyPlacement> placements,IReadOnlyCollection<int>? removed)
    {
        Require(destination=="darkhollow"&&(removed==null||removed.Count==0)&&H(host)=="a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97"&&H(hostOverlay)=="9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32","use original Dark Hollow without native/terrain/asset edits for this chain");
        UniversalMagicChainPathFollowing.RequireSource(source,overlay,exe);
        var bindings=UniversalExeBindings.Read(exe,30);Require(bindings.Count==overlay.Bindings.Count&&bindings.All(p=>overlay.Bindings.TryGetValue(p.Key,out var actual)&&actual==p.Value),"complete native source callback/copy/particle bindings changed");
        if(placements.Any(p=>p.PersistentSlot!=null))Require(placements.All(p=>p.PersistentSlot!=null&&!string.IsNullOrWhiteSpace(p.EditorId))&&placements.Select(p=>p.EditorId).Distinct().Count()==placements.Count&&placements.Select(p=>p.PersistentSlot!.Value).Order().SequenceEqual(Enumerable.Range(0,placements.Count)),"preserve unique editor IDs and contiguous lifetime ledger including retirements");
        else Require(placements.All(p=>!p.Retired),"retired identities require persistent lifetime slots");
        var active=placements.Where(p=>!p.Retired).ToArray();int prefix=new UniversalLevelData(host).Scene().RowCount;
        Require((new UniversalLevelData(host).Scene().MobyComponentSize-8)/88+5<=256,"retained actor arena and four new rows exceed the owned256-word active cache including terminator");
        Require(active.Length==4&&active.All(p=>p.SourceLevelKey=="magiccrafters")&&active.Select(p=>p.SourceRowIndex).Order().SequenceEqual(new[]{2,3,4,5}),"import all four original chain members exactly once");
        Require(active.All(p=>!string.IsNullOrWhiteSpace(p.EditorId)&&!string.IsNullOrWhiteSpace(p.LinkGroupId)&&p.PersistentSlot is null or >=0)&&active.Select(p=>p.EditorId).Distinct().Count()==4&&active.Select(p=>p.LinkGroupId).Distinct().Count()==1&&active.All(p=>(p.PersistentSlot??0)<256-prefix),"one complete group with distinct editor/lifetime identities required");
        long[]? translation=null;
        foreach(var p in active)
        {
            var row=source.Row(p.SourceRowIndex);long[] delta=[(long)p.X-I(row,12),(long)p.Y-I(row,16),(long)p.Z-I(row,20)];
            Require(delta.All(d=>d>=int.MinValue&&d<=int.MaxValue)&&(translation==null||delta.SequenceEqual(translation)),"preserve original relative XYZ using one common translation");translation=delta;
            Require(p.X>=UniversalMagicPairMovementBoundary.Minimum&&p.X<UniversalMagicPairMovementBoundary.MaximumExclusive&&p.Y>=UniversalMagicPairMovementBoundary.Minimum&&p.Y<UniversalMagicPairMovementBoundary.MaximumExclusive,"initial actor leaves retained native collision grid");
            UniversalMagicDruidChainResources.Rebase(source,overlay,source.CaptureRow("magiccrafters",p.SourceRowIndex),p);
        }
        return new(source,overlay,host,hostOverlay,exe,placements);
    }
    internal void RequireOwner(UniversalOverlay source)=>Require(H(source.Bytes.ToArray())==UniversalMagicCraftersDruidIndexedRows.OverlaySha256&&source.Bindings.Count==_overlay.Bindings.Count&&_overlay.Bindings.All(p=>source.Bindings.TryGetValue(p.Key,out var actual)&&actual==p.Value),"source bytes/callback owners changed before state-domain closure");
    internal UniversalJumpTable Filter(UniversalOverlay source,UniversalJumpTable table)
    {
        RequireOwner(source);
        if(table.TableAddress is not(0x8007B544 or 0x8007B574 or 0x8007B3AC))return table;
        int count=table.TableAddress==0x8007B544?11:table.TableAddress==0x8007B574?9:101;
        Require(table.Targets.Count==count&&table.Targets.Select((target,i)=>target==source.Word(table.TableAddress+(uint)i*4)).All(x=>x),"complete source state/selector table namespace changed");
        // Original placed selector1 can write only0 at87618. Typed retained native
        // callees preserve14/24/28; the original pod setter contributes only2.
        // Source states are0/3/4; native killFD exits active cache. Source state4
        // branches to100 only for selector2, excluded by the exact selector owner.
        return table with{Targets=table.Targets.Select((target,i)=>table.TableAddress==0x8007B544?(i is 0 or 1?target:0x800889F4u):table.TableAddress==0x8007B574?(i==0?target:0x800889F4u):(i is 0 or 2 or 3 or 4?target:0x800889F4u)).ToArray()};
    }
    private sealed record AssemblyOwner(IReadOnlyList<UniversalAsmItem> Items,IReadOnlyDictionary<string,IReadOnlyList<uint>> Tables,bool LenientTables);
    internal IReadOnlyList<UniversalAsmPart> ExpectedParts()
    {
        using var resource=typeof(UniversalMagicDruidChainCertificate).Assembly.GetManifestResourceStream("Spyro.Editor.Core.Universal.MagicPairAssemblyOwner.json")??throw new InvalidDataException("Missing original common Magic native replay/init schema");
        using var memory=new MemoryStream();resource.CopyTo(memory);var bytes=memory.ToArray();Require(H(bytes)=="591667503a0274040c285f9def7314b7e504549f46d90ba1083255e117ddfc08","whole common replay/init source schema changed");
        var fixtures=JsonSerializer.Deserialize<AssemblyOwner[]>(bytes)!;var parts=fixtures.Select(f=>{var p=new UniversalAsmPart{LenientTables=f.LenientTables};p.Items.AddRange(f.Items);foreach(var t in f.Tables)p.Tables[t.Key]=t.Value;return p;}).ToArray();
        Require(parts.Length==5,"common behavior packet cardinality changed");
        // This source part is independently rebuilt from authenticated original
        // bytes, exact trace/table/state certificate and literal class271 owner.
        // Native replay, constructor and destination callback schemas stay fixed.
        parts[2]=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(_overlay,[271],"u_magiccrafters",magicChain:this).Part;
        BindCalls(parts[2]);UniversalMagicChainPathFollowing.BindSource(parts[2],_overlay);parts[4]=BuildNativeParts();return parts;
    }
    private static UniversalAsmPart BuildNativeParts()
    {var part=UniversalMagicPairCollisionShadow.BuildOwnedWrappers(1024);part.Items.AddRange(UniversalMagicChainPathFollowing.Build().Items);return part;}
    private static void BindCalls(UniversalAsmPart part)
    {
        Require(!part.Items.Any(i=>i.Orig is 0x80086BB8 or 0x80086E18 or 0x80086E94 or 0x80087278 or 0x80087D60 or 0x80087F40 or 0x800880BC or 0x80087298 or 0x80086C78 or 0x800873E8),"unissued indexed/13B consumer survived the scoped source table/state owner");
        foreach(var (pc,native) in UniversalMagicPairCollisionShadow.Calls)
        {
            int at=part.Items.FindIndex(i=>i.Orig==pc);if(at<0)continue;
            Require(part.Items[at].Word==UniversalMips.EncodeJ(3,native)&&part.Items[at].Fix.Kind==UniversalFixKind.None,"retained direct movement call changed before boundary binding");
            part.Items[at]=part.Items[at] with{Fix=new(UniversalFixKind.JalLabel,Label:$"private_magic_pair_wrap_{pc:X8}")};
        }
        // Exact source packet is checked by the path owner before redirecting.
    }
    internal UniversalAsmPart Apply(UniversalAsmPart part,UniversalOverlay source,byte[] exe)
    {RequireOwner(source);RequireTreasuredExecutable(exe);BindCalls(part);UniversalMagicChainPathFollowing.BindSource(part,source);return BuildNativeParts();}
    internal UniversalRowTemplate Rebase(UniversalRowTemplate t,UniversalMobyPlacement p)
    {Require(_placements.Contains(p),"placement is outside admitted group");return UniversalMagicDruidChainResources.Rebase(_source,_overlay,t,p);}
    internal void AppendPod(UniversalLevelData data,IReadOnlyList<(byte[] Row,UniversalRowTemplate Template)> rows,IReadOnlyList<int> physical)
    {Require(physical.SequenceEqual(_allocation.NewRows),"final physical group allocation changed");UniversalMagicDruidChainResources.AppendOwnedPods(data,rows,physical);}
    internal UniversalMagicDruidChainReadback RequireCompiled(byte[] code,uint baseAddress,IReadOnlyList<UniversalAsmPart>? actual=null)
    {
        Require(baseAddress%8==0&&baseAddress>=_hostOverlay.CopyBuffer&&(ulong)baseAddress+(uint)code.Length<=UniversalMobyComposer.NormalBufferFloor,"bundle overlaps native host owners or retained buffer domain");
        var parts=ExpectedParts();
        if(actual!=null)Require(actual.Count==parts.Count&&actual.Select((p,i)=>p.LenientTables==parts[i].LenientTables&&p.Items.SequenceEqual(parts[i].Items)&&p.Tables.Count==parts[i].Tables.Count&&parts[i].Tables.All(t=>p.Tables.TryGetValue(t.Key,out var values)&&values.SequenceEqual(t.Value))).All(x=>x),"full source/replay/constructor/native/label/table producer schema changed");
        var (expected,labels)=UniversalBehaviorBundleBuilder.Assemble(parts,baseAddress);Require(code.SequenceEqual(expected),"compiled behavior differs from independently relocated structural owners");
        UniversalMagicChainPathFollowing.RequireWritten(code,labels,baseAddress);_code=expected;_labels=labels;_base=baseAddress;
        return new(baseAddress,code.Length,H(code),_placements.Select(p=>p.SourceRowIndex).ToArray());
    }
    private int[] SaveRows(){int[] rows=Enumerable.Range(0,_allocation.FinalRowCount).ToArray();for(int i=0;i<4;i++)rows[_allocation.NewRows[i]]=checked(_host.Scene().RowCount+(_placements[i].PersistentSlot??i));return rows;}
    private NativeMobySaveMapOverlay ExpectedOverlay()
    {
        Require(_code!=null&&_labels!=null,"compiled source/native issuer absent");uint copy=checked((_base+(uint)_code!.Length+15)&~15u);
        byte[] raw=new byte[UniversalLevelData.Align((int)(copy-UniversalOverlay.Base),2048)];_hostOverlay.Bytes.Slice(0,(int)(_hostOverlay.CopyBuffer-UniversalOverlay.Base)).CopyTo(raw);_code.CopyTo(raw,(int)(_base-UniversalOverlay.Base));
        return NativeMobySaveMap.Append(_hostOverlay,raw,_labels!["wrap_upd"],copy,SaveRows());
    }
    internal void RequireFinalOverlay(byte[] overlay)=>Require(overlay.SequenceEqual(ExpectedOverlay().Overlay),"retained native overlay/callback/cache/source/label/table/save-map owner changed");
    private int Treasure=>_placements.Sum(p=>UniversalMobyComposer.TreasureValue(_source.Row(p.SourceRowIndex)));
    internal void RequireTreasuredExecutable(byte[] exe){var expected=_exe.ToArray();UniversalTreasureAccounting.ApplyDelta(expected,12,Treasure);Require(exe.SequenceEqual(expected),"native source differs from original plus exact authenticated source rewards");}
    internal void RequireFinalExecutable(byte[] exe)
    {
        Require(_labels!=null,"compiled callback owner absent");var expected=_exe.ToArray();UniversalTreasureAccounting.ApplyDelta(expected,12,Treasure);var bindings=UniversalExeBindings.Read(_exe,12);var overlay=ExpectedOverlay();
        UniversalExeBindings.Patch(expected,bindings[UniversalExeBindings.UpdateGlobal],overlay.UpdatePointer);UniversalExeBindings.Patch(expected,bindings[UniversalExeBindings.InitGlobal],_labels!["wrap_init"]);UniversalExeBindings.Patch(expected,bindings[UniversalExeBindings.CopyBufferGlobal],overlay.CopyBuffer);
        if(SaveRows().Where((value,i)=>value!=i).Any())NativeMobySaveMap.PatchExe(expected);
        Require(exe.SequenceEqual(expected),"final native executable differs from original plus derived callback/treasure/lifetime owners");
    }
    private byte[] ExpectedScene()
    {
        var expected=new UniversalLevelData(_host.Bytes.ToArray());var templates=_placements.Select(p=>Rebase(_source.CaptureRow("magiccrafters",p.SourceRowIndex),p)).ToArray();
        var resolved=templates.Select(t=>UniversalIndexedRows.Resolve(t,templates)).ToArray();var rows=new List<(byte[] Row,UniversalRowTemplate Template)>();
        for(int i=0;i<4;i++){var p=_placements[i];var t=resolved[i];var row=t.Row.ToArray();BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(12),p.X);BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(16),p.Y);BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(20),p.Z);row[67]=255;row[74]=255;rows.Add((row,t));}
        var (_,_,physical)=expected.AppendRows(rows);UniversalMagicDruidChainResources.AppendOwnedPods(expected,rows,physical);var sf=expected.Subfiles()[3];return expected.Bytes.AsSpan(sf.Offset,sf.Length).ToArray();
    }
    private sealed record Region(int Offset,int Length,string Role,string SHA256);
    private sealed record Assets(int Length,int SceneOffset,int SceneLength,IReadOnlyList<Region> Owners);
    internal void RequireFinalRows(UniversalLevelData data,IReadOnlyList<int> physical)
    {
        Require(physical.SequenceEqual(_allocation.NewRows),"final allocated physical/native prefix identity changed");
        using var stream=typeof(UniversalMagicDruidChainCertificate).Assembly.GetManifestResourceStream("Spyro.Editor.Core.Universal.MagicPairAssetOwners.json")!;using var memory=new MemoryStream();stream.CopyTo(memory);var b=memory.ToArray();Require(H(b)=="69db09bd5f88d71a5c6b0656c804aea7c9fc979168400c91aa90159384039f3e","original model/host/resource schema changed");var assets=JsonSerializer.Deserialize<Assets>(b)!;
        Require(data.Bytes.Length==assets.Length&&data.Subfiles()[3]==(assets.SceneOffset,assets.SceneLength),"chain model/native resource allocation differs from independently source-owned layout");
        foreach(var region in assets.Owners)Require(H(data.Bytes.AsSpan(region.Offset,region.Length).ToArray())==region.SHA256,"immutable native/imported model/texture/sound/descriptor owner changed: "+region.Role);
        Require(data.Bytes.AsSpan(assets.SceneOffset,assets.SceneLength).SequenceEqual(ExpectedScene())&&data.Scene().RowCount==_allocation.FinalRowCount,"final scene differs from actual source-derived common translation/rows/indices/shared flag/pod/path/fixups and retained native host");
        var st=UniversalParticleDependencies.Records(_source);var dt=UniversalParticleDependencies.Records(data);Require(st.TryGetValue(0,out var src)&&dt.TryGetValue(0,out var dst)&&UniversalParticleDependencies.EquivalentTexture(src,_source.Vram(),dst,data.Vram()),"native death particle texture0 lost its source pixels/palette owner");
    }
}
