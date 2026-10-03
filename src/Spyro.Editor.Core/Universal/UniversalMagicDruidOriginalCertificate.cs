using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;

public sealed record UniversalMagicDruidOriginalReadback(uint Base,int Bytes,string SHA256,IReadOnlyList<int> SourceRows)
{
    public string Scope {get;init;}="PRIVATE original Magic1/11/16/25/26/27 selector0/3 and second selector2 pair32/33, retained original Dark Hollow, common translation/native yaw, one source-owned singleton or second pair with rebased home/path; native evidence pending";
}

/// <summary>Private aggregate issuer for an original selector0/3 singleton or selector2 pair32/33. Admission
/// follows structural owners and authored inputs, not a fixed final image/pose.
/// This authority never admits source selectors4/10 or a genuine13B spawn.</summary>
public sealed class UniversalMagicDruidOriginalCertificate
{
    private readonly UniversalLevelData _source,_host;
    private readonly UniversalOverlay _overlay,_hostOverlay;
    private readonly byte[] _exe;
    private readonly UniversalMobyPlacement[] _placements;
    private readonly UniversalRowAllocation _allocation;private readonly int _selector;private readonly int _count;
    private byte[]? _code;private Dictionary<string,uint>? _labels;private uint _base;
    private UniversalMagicDruidOriginalCertificate(UniversalLevelData source,UniversalOverlay overlay,byte[] host,byte[] hostOverlay,byte[] exe,IReadOnlyList<UniversalMobyPlacement> placements)
    { _source=new(source.Bytes.ToArray());_overlay=new(overlay.Bytes.ToArray(),overlay.Bindings.ToDictionary());_host=new(host.ToArray());_exe=exe.ToArray();_hostOverlay=new(hostOverlay.ToArray(),UniversalExeBindings.Read(exe,12));_placements=placements.Where(p=>!p.Retired).OrderBy(p=>p.PersistentSlot??placements.ToList().IndexOf(p)).ToArray();_count=_placements.Length;_allocation=UniversalRowRemoval.Allocate(_host,[],_count);_selector=I(source.CaptureRow("magiccrafters",_placements[0].SourceRowIndex).Properties,20); }
    private static int I(byte[] b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static string H(byte[] b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Magic original druid: "+why);}
    internal static UniversalMagicDruidOriginalCertificate Capture(string destination,byte[] host,byte[] hostOverlay,byte[] exe,UniversalLevelData source,UniversalOverlay overlay,IReadOnlyList<UniversalMobyPlacement> placements,IReadOnlyCollection<int>? removed)
    {
        Require(destination=="darkhollow"&&(removed==null||removed.Count==0)&&H(host)=="a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97"&&H(hostOverlay)=="9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32","use original Dark Hollow without native/terrain/asset edits for this druid");
        UniversalMagicChainPathFollowing.RequireSource(source,overlay,exe);
        var bindings=UniversalExeBindings.Read(exe,30);Require(bindings.Count==overlay.Bindings.Count&&bindings.All(p=>overlay.Bindings.TryGetValue(p.Key,out var actual)&&actual==p.Value),"complete native source callback/copy/particle bindings changed");
        if(placements.Any(p=>p.PersistentSlot!=null))Require(placements.All(p=>p.PersistentSlot!=null&&!string.IsNullOrWhiteSpace(p.EditorId))&&placements.Select(p=>p.EditorId).Distinct().Count()==placements.Count&&placements.Select(p=>p.PersistentSlot!.Value).Order().SequenceEqual(Enumerable.Range(0,placements.Count)),"preserve unique editor IDs and contiguous lifetime ledger including retirements");
        else Require(placements.All(p=>!p.Retired),"retired identities require persistent lifetime slots");
        var active=placements.Where(p=>!p.Retired).ToArray();int prefix=new UniversalLevelData(host).Scene().RowCount;
        Require((new UniversalLevelData(host).Scene().MobyComponentSize-8)/88+active.Length+1<=256,"retained actor arena and admitted source rows exceed the owned256-word active cache including terminator");
        bool pair=active.Length==2&&active.All(p=>p.SourceLevelKey=="magiccrafters")&&active.Select(p=>p.SourceRowIndex).Order().SequenceEqual(new[]{32,33});
        Require(pair||active.Length==1&&active.All(p=>p.SourceLevelKey=="magiccrafters"&&p.SourceRowIndex is 1 or 11 or 16 or 25 or 26 or 27),"import one original selector0/3 singleton or complete second pair32/33");
        Require(active.All(p=>!string.IsNullOrWhiteSpace(p.EditorId)&&p.PersistentSlot is null or >=0)&&active.Select(p=>p.EditorId).Distinct().Count()==active.Length&&active.All(p=>(p.PersistentSlot??0)<256-prefix),"unique source/editor/lifetime identities required");
        if(pair)Require(active.All(p=>!string.IsNullOrWhiteSpace(p.LinkGroupId))&&active.Select(p=>p.LinkGroupId).Distinct().Count()==1,"one original second pair group required");
        long[]? translation=null;
        foreach(var p in active)
        {
            var row=source.Row(p.SourceRowIndex);long[] delta=[(long)p.X-I(row,12),(long)p.Y-I(row,16),(long)p.Z-I(row,20)];
            Require(delta.All(d=>d>=int.MinValue&&d<=int.MaxValue)&&(translation==null||delta.SequenceEqual(translation)),"preserve original relative XYZ using one common translation");translation=delta;
            Require(p.X>=UniversalMagicPairMovementBoundary.Minimum&&p.X<UniversalMagicPairMovementBoundary.MaximumExclusive&&p.Y>=UniversalMagicPairMovementBoundary.Minimum&&p.Y<UniversalMagicPairMovementBoundary.MaximumExclusive,"initial actor leaves retained native collision grid");
            UniversalMagicDruidOriginalRows.Rebase(source,overlay,source.CaptureRow("magiccrafters",p.SourceRowIndex),p);
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
        // Exact original placed selectors0/2/3 have no retained14 store. Typed retained native
        // callees preserve14/24/28; the original pod setter contributes only2.
        // Source states are0/2/3/4; native killFD exits active cache. Source state4
        // branches to100 only for selector2; the table owner retains it only for that pair.
        return table with{Targets=table.Targets.Select((target,i)=>table.TableAddress==0x8007B544?(i==_selector?target:0x800889F4u):table.TableAddress==0x8007B574?(i==_selector-1?target:0x800889F4u):(i is 0 or 2 or 3 or 4||_selector==2&&i==100?target:0x800889F4u)).ToArray()};
    }
    private sealed record AssemblyOwner(IReadOnlyList<UniversalAsmItem> Items,IReadOnlyDictionary<string,IReadOnlyList<uint>> Tables,bool LenientTables);
    internal IReadOnlyList<UniversalAsmPart> ExpectedParts()
    {
        using var resource=typeof(UniversalMagicDruidOriginalCertificate).Assembly.GetManifestResourceStream("Spyro.Editor.Core.Universal.MagicPairAssemblyOwner.json")??throw new InvalidDataException("Missing original common Magic native replay/init schema");
        using var memory=new MemoryStream();resource.CopyTo(memory);var bytes=memory.ToArray();Require(H(bytes)=="591667503a0274040c285f9def7314b7e504549f46d90ba1083255e117ddfc08","whole common replay/init source schema changed");
        var fixtures=JsonSerializer.Deserialize<AssemblyOwner[]>(bytes)!;var parts=fixtures.Select(f=>{var p=new UniversalAsmPart{LenientTables=f.LenientTables};p.Items.AddRange(f.Items);foreach(var t in f.Tables)p.Tables[t.Key]=t.Value;return p;}).ToArray();
        Require(parts.Length==5,"common behavior packet cardinality changed");
        // This source part is independently rebuilt from authenticated original
        // bytes, exact trace/table/state certificate and literal class271 owner.
        // Native replay, constructor and destination callback schemas stay fixed.
        parts[2]=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(_overlay,[271],"u_magiccrafters",magicOriginal:this).Part;
        BindCalls(parts[2]);parts[4]=BuildNativeParts();return parts;
    }
    private static UniversalAsmPart BuildNativeParts()
    =>UniversalMagicPairCollisionShadow.BuildOwnedWrappers(1024);
    private void BindCalls(UniversalAsmPart part)
    {
        if(_selector==2)UniversalMagicPairCollisionShadow.RequireCopiedOwner(part);
        Require(!part.Items.Any(i=>i.Orig is 0x80086BB8 or 0x80086E18 or 0x80086E94 or 0x80087278 or 0x80087D60 or 0x80087F40 or 0x800880BC or 0x80087298 or 0x80086C78 or 0x800873E8),"unissued indexed/13B consumer survived the scoped source table/state owner");
        foreach(var (pc,native) in UniversalMagicPairCollisionShadow.Calls)
        {
            int at=part.Items.FindIndex(i=>i.Orig==pc);if(at<0)continue;
            Require(part.Items[at].Word==UniversalMips.EncodeJ(3,native)&&part.Items[at].Fix.Kind==UniversalFixKind.None,"retained direct movement call changed before boundary binding");
            part.Items[at]=part.Items[at] with{Fix=new(UniversalFixKind.JalLabel,Label:$"private_magic_pair_wrap_{pc:X8}")};
            if(_selector==2)part.Items.Insert(at,new(null,default,null,$"private_magic_pair_call_{pc:X8}"));
        }
        // Exact source packet is checked by the path owner before redirecting.
    }
    internal UniversalAsmPart Apply(UniversalAsmPart part,UniversalOverlay source,byte[] exe)
    {RequireOwner(source);RequireTreasuredExecutable(exe);BindCalls(part);return BuildNativeParts();}
    internal UniversalRowTemplate Rebase(UniversalRowTemplate t,UniversalMobyPlacement p)
    {Require(_placements.Contains(p),"placement is outside admitted group");return UniversalMagicDruidOriginalRows.Rebase(_source,_overlay,t,p);}
    internal UniversalMagicDruidOriginalReadback RequireCompiled(byte[] code,uint baseAddress,IReadOnlyList<UniversalAsmPart>? actual=null)
    {
        Require(baseAddress%8==0&&baseAddress>=_hostOverlay.CopyBuffer&&(ulong)baseAddress+(uint)code.Length<=UniversalMobyComposer.NormalBufferFloor,"bundle overlaps native host owners or retained buffer domain");
        var parts=ExpectedParts();
        if(actual!=null)Require(actual.Count==parts.Count&&actual.Select((p,i)=>p.LenientTables==parts[i].LenientTables&&p.Items.SequenceEqual(parts[i].Items)&&p.Tables.Count==parts[i].Tables.Count&&parts[i].Tables.All(t=>p.Tables.TryGetValue(t.Key,out var values)&&values.SequenceEqual(t.Value))).All(x=>x),"full source/replay/constructor/native/label/table producer schema changed");
        var (expected,labels)=UniversalBehaviorBundleBuilder.Assemble(parts,baseAddress);Require(code.SequenceEqual(expected),"compiled behavior differs from independently relocated structural owners");
        _code=expected;_labels=labels;_base=baseAddress;
        return new(baseAddress,code.Length,H(code),_placements.Select(p=>p.SourceRowIndex).ToArray());
    }
    private int[] SaveRows(){int[] rows=Enumerable.Range(0,_allocation.FinalRowCount).ToArray();for(int i=0;i<_count;i++)rows[_allocation.NewRows[i]]=checked(_host.Scene().RowCount+(_placements[i].PersistentSlot??i));return rows;}
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
        for(int i=0;i<_count;i++){var p=_placements[i];var t=resolved[i];var row=t.Row.ToArray();BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(12),p.X);BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(16),p.Y);BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(20),p.Z);row[67]=255;row[74]=255;rows.Add((row,t));}
        var (_,_,physical)=expected.AppendRows(rows);AppendPairPod(expected,physical);var sf=expected.Subfiles()[3];return expected.Bytes.AsSpan(sf.Offset,sf.Length).ToArray();
    }
    internal void AppendPairPod(UniversalLevelData data,IReadOnlyList<int> rows)
    {
        if(_count!=2)return;
        Require(rows.SequenceEqual(_allocation.NewRows) && data.Scene().RowCount==_allocation.FinalRowCount,"exact append-only final physical pair required; no reuse, duplicate or reordered row binding");
        var sc=data.Scene();var sf=data.Subfiles()[3];byte[] scene=data.Bytes.AsSpan(sf.Offset,sf.Length).ToArray();
        int oldSize=I(scene,sc.PodComponent),oldCount=I(scene,sc.PodComponent+4);Require(oldCount==2,"native Dark Hollow pod namespace changed");
        int newSize=checked(oldSize+8);byte[] pods=new byte[newSize];BinaryPrimitives.WriteInt32LittleEndian(pods,newSize);BinaryPrimitives.WriteInt32LittleEndian(pods.AsSpan(4),oldCount+1);
        for(int i=0;i<oldCount;i++)BinaryPrimitives.WriteInt32LittleEndian(pods.AsSpan(8+4*i),checked(I(scene,sc.PodComponent+8+4*i)+4));
        scene.AsSpan(sc.PodComponent+8+4*oldCount,oldSize-8-4*oldCount).CopyTo(pods.AsSpan(12+4*oldCount));BinaryPrimitives.WriteInt32LittleEndian(pods.AsSpan(8+4*oldCount),oldSize+4);
        for(int i=0;i<2;i++){BinaryPrimitives.WriteUInt16LittleEndian(pods.AsSpan(oldSize+4+i*2),(ushort)(rows[i]|(i==1?0x8000:0)));scene[sc.RowsOffset+rows[i]*88+0x43]=checked((byte)oldCount);}
        int oldEnd=sc.PodComponent+oldSize;int Move(int value)=>value>=oldEnd?checked(value+8):value;
        byte[] rewritten=new byte[Math.Max(scene.Length,checked(sc.UsedEnd+8))];scene.AsSpan(0,sc.PodComponent).CopyTo(rewritten);pods.CopyTo(rewritten,sc.PodComponent);scene.AsSpan(oldEnd,sc.UsedEnd-oldEnd).CopyTo(rewritten.AsSpan(oldEnd+8));
        foreach(int fixup in sc.Fixups)BinaryPrimitives.WriteInt32LittleEndian(rewritten.AsSpan(Move(fixup)),Move(I(scene,fixup)));
        int fixupsAt=Move(sc.FixupsOffset);for(int i=0;i<sc.Fixups.Count;i++)BinaryPrimitives.WriteInt32LittleEndian(rewritten.AsSpan(fixupsAt+4+4*i),Move(sc.Fixups[i]));
        if(rewritten.Length>sf.Length)data.GrowSubfile(3,UniversalLevelData.Align(rewritten.Length-sf.Length,2048));sf=data.Subfiles()[3];Array.Clear(data.Bytes,sf.Offset,sf.Length);rewritten.CopyTo(data.Bytes,sf.Offset);
        sc=data.Scene();int leader=rows[Array.FindIndex(_placements,p=>p.SourceRowIndex==32)];int leaderProps=I(data.Row(leader),0);int flag=checked(leaderProps+76);
        Require(I(data.Bytes,sf.Offset+flag)==0,"second pair shared source flag allocation changed");
        foreach(int row in rows){int properties=I(data.Row(row),0);Require(sc.Fixups.Contains(properties+60),"second pair shared flag fixup owner missing");BinaryPrimitives.WriteInt32LittleEndian(data.Bytes.AsSpan(sf.Offset+properties+60),flag);}

    }

    private sealed record Region(int Offset,int Length,string Role,string SHA256);
    private sealed record Assets(int Length,int SceneOffset,int SceneLength,IReadOnlyList<Region> Owners);
    internal void RequireFinalRows(UniversalLevelData data,IReadOnlyList<int> physical)
    {
        Require(physical.SequenceEqual(_allocation.NewRows),"final allocated physical/native prefix identity changed");
        using var stream=typeof(UniversalMagicDruidOriginalCertificate).Assembly.GetManifestResourceStream("Spyro.Editor.Core.Universal.MagicPairAssetOwners.json")!;using var memory=new MemoryStream();stream.CopyTo(memory);var b=memory.ToArray();Require(H(b)=="69db09bd5f88d71a5c6b0656c804aea7c9fc979168400c91aa90159384039f3e","original model/host/resource schema changed");var assets=JsonSerializer.Deserialize<Assets>(b)!;
        Require(data.Bytes.Length==assets.Length&&data.Subfiles()[3]==(assets.SceneOffset,assets.SceneLength),"original druid model/native resource allocation differs from independently source-owned layout");
        foreach(var region in assets.Owners)Require(H(data.Bytes.AsSpan(region.Offset,region.Length).ToArray())==region.SHA256,"immutable native/imported model/texture/sound/descriptor owner changed: "+region.Role);
        Require(data.Bytes.AsSpan(assets.SceneOffset,assets.SceneLength).SequenceEqual(ExpectedScene())&&data.Scene().RowCount==_allocation.FinalRowCount,"final scene differs from actual source-derived common translation/rows/indices/shared flag/pod/path/fixups and retained native host");
        var st=UniversalParticleDependencies.Records(_source);var dt=UniversalParticleDependencies.Records(data);Require(st.TryGetValue(0,out var src)&&dt.TryGetValue(0,out var dst)&&UniversalParticleDependencies.EquivalentTexture(src,_source.Vram(),dst,data.Vram()),"native death particle texture0 lost its source pixels/palette owner");
    }
}
