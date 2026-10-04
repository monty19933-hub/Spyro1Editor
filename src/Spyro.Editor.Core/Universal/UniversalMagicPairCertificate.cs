using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;

/// <summary>Original Magic12/13 selector2 group within unchanged Dark Hollow,
/// or original Artisans with its six selected native Gnorc removals.
/// This authority never applies to other rows, selectors, groups, destinations or mixed imports.
/// Native ordinary scene/sound/particle arenas are retained; imported movement owns its private
/// cache and bounds every future XY query/commit. The native engine is not certified for corrupted RAM.</summary>
public sealed class UniversalMagicPairCertificate
{
    private readonly UniversalLevelData _source;
    private readonly UniversalOverlay _overlay;
    private readonly IReadOnlyList<UniversalMobyPlacement> _placements;
    private readonly UniversalLevelData _host;
    private readonly UniversalOverlay _hostOverlay;
    private readonly byte[] _originalExe;
    private readonly string _hostKey;
    private readonly int _hostId;
    private readonly int[] _removed;
    private readonly UniversalRowAllocation _allocation;
    internal int PreservedCacheBytes => _hostKey=="artisans" ? UniversalMagicArtisansHost.CacheBytes(_host,_allocation.FinalRowCount) : UniversalMagicPairCollisionShadow.ShadowBytes;
    private uint _bundleBase;
    private byte[]? _expectedCode;
    private Dictionary<string,uint>? _expectedLabels;
    private int NativeCount=>_host.Scene().RowCount;
    private int PodId=>I(_host.Bytes,_host.Subfiles()[3].Offset+_host.Scene().PodComponent+4);
    private UniversalMagicPairCertificate(UniversalLevelData source, UniversalOverlay overlay, IReadOnlyList<UniversalMobyPlacement> placements, byte[] hostData,byte[] hostOverlay,byte[] exe,string hostKey,IReadOnlyCollection<int>? removals)
    { _source = new(source.Bytes.ToArray()); _overlay = new(overlay.Bytes.ToArray(),overlay.Bindings.ToDictionary()); _placements = placements.Where(p=>!p.Retired).All(p=>p.PersistentSlot!=null)?placements.Where(p=>!p.Retired).OrderBy(p=>p.PersistentSlot).ToArray():placements.Where(p=>!p.Retired).ToArray(); _host=new(hostData.ToArray());_originalExe=exe.ToArray();_hostKey=hostKey;_hostId=hostKey=="artisans"?10:12;_removed=(removals??[]).Order().ToArray();_allocation=UniversalRowRemoval.Allocate(_host,_removed,2);_hostOverlay=new(hostOverlay.ToArray(),UniversalExeBindings.Read(exe,_hostId)); }
    private static string H(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static int I(byte[] b,int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static void Require(bool ok,string why) { if(!ok)throw new InvalidDataException("Magic selector2 pair: "+why); }
    internal static bool IsPair(UniversalRowTemplate t) => t.SourceLevelKey=="magiccrafters" && t.SourceRowIndex is 12 or 13;
    internal static UniversalMagicPairCertificate Capture(string destination, byte[] data, byte[] overlay, byte[] exe,
        UniversalLevelData source, UniversalOverlay sourceOverlay, IReadOnlyList<UniversalMobyPlacement> placements,IReadOnlyCollection<int>? removals,bool removedTreasureAlreadyApplied=false)
    {
        if(destination=="artisans"&&removedTreasureAlreadyApplied)(data,exe)=UniversalMagicArtisansHost.RestoreSavedDeletions(data,overlay,exe,removals);
        UniversalMagicPairCollisionShadow.RequireScenario(destination,data,overlay,exe,source,sourceOverlay,placements,removals);
        var bindings=UniversalExeBindings.Read(exe,30);Require(sourceOverlay.Bindings.Count==bindings.Count&&bindings.All(b=>sourceOverlay.Bindings.TryGetValue(b.Key,out var current)&&current==b.Value),"complete source callback/copy/particle bindings differ from original native issuer");
        foreach(var p in placements.Where(p=>!p.Retired)) RequireSourceTemplate(source,sourceOverlay,source.CaptureRow(p.SourceLevelKey,p.SourceRowIndex));
        return new(source,sourceOverlay,placements,data,overlay,exe,destination,removals);
    }
    // Source-only enrollment exposes the authentic companion identity for Expand. The export
    // still requires Capture above before dependency pruning/negative-index admission/rebasing.
    internal static UniversalRowTemplate CaptureSource(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate template)
    {
        RequireSourceTemplate(data,overlay,template);
        return template with { SourcePrerequisiteRows = new[]{new UniversalSourcePrerequisite(template.SourceRowIndex==12?13:12,271)} };
    }
    internal static void RequirePrerequisites(UniversalRowTemplate owner,IReadOnlyList<UniversalRowTemplate> active)
    {
        Require(IsPair(owner)&&owner.ClassId==271,"pair prerequisite has no exact source actor owner");int target=owner.SourceRowIndex==12?13:12;
        Require(owner.SourcePrerequisiteRows.SequenceEqual(new[]{new UniversalSourcePrerequisite(target,271)}),"pair companion prerequisite metadata changed");
        Require(active.Count(t=>t.SourceLevelKey==owner.SourceLevelKey&&t.SourceRowIndex==target&&(t.LinkGroupId??"")== (owner.LinkGroupId??"")&&t.ClassId==271)==1,"exact native pod companion must be imported once in the same group");
    }
    private static void RequireSourceTemplate(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate template)
    {
        Require(IsPair(template) && H(data.Bytes)=="ad34af2e3401514e7db740691c80fd2bbf59057a55bdfedbaaa39b6939141fcb" && H(overlay.Bytes.ToArray())=="98e135e18876ea094ca9484b33cfb38438e2b99b72ee4d6d66e907722c744a1d","complete original pair data/overlay required");
        Require(overlay.Update==0x8007E398&&overlay.Init==0x8008B2C0&&overlay.CopyBuffer==0x8008E608,"source callback entry bindings changed");
        var original=data.CaptureRow("magiccrafters",template.SourceRowIndex);
        Require(template.ClassId==271 && template.Row.SequenceEqual(original.Row) && template.Properties.SequenceEqual(original.Properties) && template.InternalPointers.SequenceEqual(original.InternalPointers) && template.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers) && template.DroppedExternalPointers==original.DroppedExternalPointers,"template must retain exact source identity, byte fields and pointer owners");
        Require(template.Row[0x43]==1 && template.Row[0x48]==0 && template.Row[0x49]==0 && I(template.Properties,0x14)==2 && I(template.Properties,0x24)==-1 && I(template.Properties,0x28)==-1,"only original selector2, initialstate0, pod1 and both inactive negative indices are issued");
        Require(template.InternalPointers.Count==2 && template.InternalPointers.Any(p=>p.Offset==0&&p.Target==0x50) && template.InternalPointers.Any(p=>p.Offset==0x3C&&p.Target==0x4C) && template.Properties[0x50]==1,"exact one-node path and read-only shared flag resource owner required");
    }
    internal void RequireOwner(UniversalOverlay overlay)
    { Require(H(overlay.Bytes.ToArray())=="98e135e18876ea094ca9484b33cfb38438e2b99b72ee4d6d66e907722c744a1d"&&overlay.Bindings.Count==_overlay.Bindings.Count&&_overlay.Bindings.All(b=>overlay.Bindings.TryGetValue(b.Key,out var current)&&current==b.Value),"closure request changed its complete source bytes or callback owner"); }
    internal UniversalJumpTable Filter(UniversalOverlay owner,UniversalJumpTable table)
    {
        RequireOwner(owner);
        if(table.TableAddress is not(0x8007B544 or 0x8007B574 or 0x8007B3AC))return table;
        int expected=table.TableAddress==0x8007B544?11:table.TableAddress==0x8007B574?9:101;
        Require(table.Targets.Count==expected,"source state/selector table cardinality changed");
        for(int i=0;i<expected;i++)Require(table.Targets[i]==owner.Word(table.TableAddress+(uint)i*4),"table targets differ from original source producer");
        // Induction: native placed init retains selector2/indices; all direct source
        // stores exclude Props14/24/28. Enrolled native callees retain these fields.
        // Only live states0/2/3/4/100 arise from own stores or exact pod state2;
        // placed kill FD leaves active cache. Original DH pods0/1 never target pod2.
        // State2 uses exact index1 from immutable selector2 minus1.
        return table with { Targets = table.Targets.Select((target,i)=>table.TableAddress==0x8007B544?(i==2?target:0x800889F4u):table.TableAddress==0x8007B574?(i==1?target:0x800889F4u):(i is 0 or 2 or 3 or 4 or 100?target:0x800889F4u)).ToArray() };
    }
    private sealed record AssemblyOwner(IReadOnlyList<UniversalAsmItem> Items,IReadOnlyDictionary<string,IReadOnlyList<uint>> Tables,bool LenientTables);
    private sealed record AssetRegion(int Offset,int Length,string Role,string SHA256);
    private sealed record AssetOwners(int Length,int SceneOffset,int SceneLength,IReadOnlyList<AssetRegion> Owners);
    private static T Resource<T>(string name,string sha)
    {
        using var input=typeof(UniversalMagicPairCertificate).Assembly.GetManifestResourceStream("Spyro.Editor.Core.Universal."+name)??throw new InvalidDataException("Missing authenticated Magic owner schema");
        using var buffer=new MemoryStream();input.CopyTo(buffer);var bytes=buffer.ToArray();Require(H(bytes)==sha,"complete immutable owner schema changed");return JsonSerializer.Deserialize<T>(bytes)??throw new InvalidDataException("Invalid Magic owner schema");
    }
    private IReadOnlyList<UniversalAsmPart> ExpectedParts()
    {
        var fixture=Resource<AssemblyOwner[]>("MagicPairAssemblyOwner.json","591667503a0274040c285f9def7314b7e504549f46d90ba1083255e117ddfc08");
        var parts=fixture.Select(f=>{UniversalAsmPart p=new(){LenientTables=f.LenientTables};p.Items.AddRange(f.Items);foreach(var t in f.Tables)p.Tables.Add(t.Key,t.Value);return p;}).ToArray();
        if(_hostKey=="artisans")
        {
            var wrappers=parts[1];int update=wrappers.Items.FindIndex(i=>i.Word==UniversalMips.EncodeJ(3,0x8007AE40));int init=wrappers.Items.FindIndex(i=>i.Word==UniversalMips.EncodeJ(2,0x80082960));
            Require(update>=0&&init>=0,"exact native replay owner disappeared");
            wrappers.Items[update]=wrappers.Items[update] with{Word=UniversalMips.EncodeJ(3,_hostOverlay.Update)};wrappers.Items[init]=wrappers.Items[init] with{Word=UniversalMips.EncodeJ(2,_hostOverlay.Init)};
            parts[4]=UniversalMagicPairCollisionShadow.BuildOwnedWrappers(PreservedCacheBytes);
        }
        return parts;
    }
    internal void RequireCompiled(byte[] code,uint baseAddress,IReadOnlyList<UniversalAsmPart>? actualParts=null)
    {
        Require(baseAddress%8==0&&baseAddress>=_hostOverlay.CopyBuffer&&(ulong)baseAddress+(uint)code.Length<=UniversalMobyComposer.NormalBufferFloor,"allocated bundle overlaps native owner or exceeds retained buffer domain");
        var expectedParts=ExpectedParts();
        if(actualParts!=null)
        {
            Require(actualParts.Count==expectedParts.Count,"compiled packet owner count changed");
            for(int i=0;i<expectedParts.Count;i++)Require(actualParts[i].Items.SequenceEqual(expectedParts[i].Items)&&actualParts[i].LenientTables==expectedParts[i].LenientTables&&actualParts[i].Tables.Count==expectedParts[i].Tables.Count&&expectedParts[i].Tables.All(t=>actualParts[i].Tables.TryGetValue(t.Key,out var v)&&v.SequenceEqual(t.Value)),"complete source/init/replay/argument/label/incoming/table schema changed");
        }
        var (expected,labels)=UniversalBehaviorBundleBuilder.Assemble(expectedParts,baseAddress);
        Require(code.SequenceEqual(expected),"emission differs from independently relocated complete authenticated schema");
        _bundleBase=baseAddress;_expectedCode=expected;_expectedLabels=labels;
    }
    // Physical rows derive from the append-only scene; lifetime identities derive
    // from the validated editor ledger, including retired slots. Retired entries
    // own no actors/properties/code. They cannot donate an old card bit to a new row.
    private int[] SaveRows()
    {
        int[] map=Enumerable.Range(0,_allocation.FinalRowCount).Select(i=>_removed.Contains(i)?-1:i).ToArray();
        for(int i=0;i<_placements.Count;i++)map[_allocation.NewRows[i]]=checked(NativeCount+(_placements[i].PersistentSlot??i));return map;
    }
    private bool RemapsSaveRows=>SaveRows().Where((n,i)=>n!=i).Any();
    private NativeMobySaveMapOverlay ExpectedOverlay()
    {
        Require(_expectedCode!=null&&_expectedLabels!=null,"final overlay has no admitted compiled owner");
        uint copy=checked((_bundleBase+(uint)_expectedCode!.Length+15)&~15u);
        byte[] expected=new byte[UniversalLevelData.Align(checked((int)(copy-UniversalOverlay.Base)),0x800)];
        _hostOverlay.Bytes.Slice(0,checked((int)(_hostOverlay.CopyBuffer-UniversalOverlay.Base))).CopyTo(expected);
        _expectedCode.CopyTo(expected,checked((int)(_bundleBase-UniversalOverlay.Base)));
        var result=NativeMobySaveMap.Append(_hostOverlay,expected,_expectedLabels!["wrap_upd"],copy,SaveRows());
        Require(result.CopyBuffer<=UniversalMobyComposer.NormalBufferFloor,"lifetime save-map allocation exceeds retained buffer domain");
        return result;
    }
    internal void RequireFinalOverlay(byte[] overlay)
    {
        var expected=ExpectedOverlay();
        Require(overlay.SequenceEqual(expected.Overlay),"final retained native prefix/padding/relocated bundle/table/lifetime map owner changed");
    }
    internal void RequireFinalExecutable(byte[] exe)
    {
        Require(_expectedCode!=null&&_expectedLabels!=null,"final executable has no admitted callbacks");
        byte[] expected=_originalExe.ToArray();UniversalTreasureAccounting.ApplyDelta(expected,_hostId,_placements.Sum(p=>UniversalMobyComposer.TreasureValue(_source.Row(p.SourceRowIndex)))-_removed.Sum(r=>UniversalMobyComposer.TreasureValue(_host.Row(r))));
        var bindings=UniversalExeBindings.Read(_originalExe,_hostId);var overlay=ExpectedOverlay();
        UniversalExeBindings.Patch(expected,bindings[UniversalExeBindings.UpdateGlobal],overlay.UpdatePointer);
        UniversalExeBindings.Patch(expected,bindings[UniversalExeBindings.InitGlobal],_expectedLabels!["wrap_init"]);
        UniversalExeBindings.Patch(expected,bindings[UniversalExeBindings.CopyBufferGlobal],overlay.CopyBuffer);
        // PatchExe authenticates all six original source-owner windows atomically.
        // Complete original EXE authentication and final expected equality also
        // bind every incoming caller and all unchanged native source instructions.
        if(RemapsSaveRows)NativeMobySaveMap.PatchExe(expected);
        Require(exe.SequenceEqual(expected),"final executable differs from original plus derived treasure/callback/lifetime-map owners");
    }
    internal void RequireTreasuredExecutable(byte[] exe) { var expected=_originalExe.ToArray();UniversalTreasureAccounting.ApplyDelta(expected,_hostId,7-_removed.Sum(r=>UniversalMobyComposer.TreasureValue(_host.Row(r))));Require(exe.SequenceEqual(expected),"native source before bundle differs from original plus exact pair treasure delta"); }
    private byte[] ExpectedScene()
    {
        UniversalLevelData expected=new(_host.Bytes.ToArray());List<(byte[] Row,UniversalRowTemplate Template)> rows=[];
        foreach(var p in _placements)
        {
            var t=Rebase(CaptureSource(_source,_overlay,_source.CaptureRow("magiccrafters",p.SourceRowIndex)),p) with{LinkGroupId=p.LinkGroupId};byte[] row=t.Row.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(12),p.X);BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(16),p.Y);BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(20),p.Z);row[0x43]=255;row[0x4A]=255;rows.Add((row,t));
        }
        if(_removed.Length>0) { foreach(int r in _removed)expected.SetRowClass(r,UniversalMobyComposer.InertClass);expected.RemoveModels([114,339]);UniversalRowRemoval.RemoveOwnedPropertiesAndClearRows(expected,_removed); }
        var (_,_,physical)=expected.AppendRows(rows,_removed);WritePod(expected,physical);var sf=expected.Subfiles()[3];return expected.Bytes.AsSpan(sf.Offset,sf.Length).ToArray();
    }
    internal UniversalRowTemplate Rebase(UniversalRowTemplate template,UniversalMobyPlacement placement)
    {
        RequireSourceTemplate(_source,_overlay,template);
        Require(_placements.Any(p=>p==placement),"property rebasing has no admitted authored placement");
        byte[] props=template.Properties.ToArray();int[] delta={checked(placement.X-I(template.Row,12)),checked(placement.Y-I(template.Row,16)),checked(placement.Z-I(template.Row,20))};
        void Shift(int at) { for(int axis=0;axis<3;axis++) BinaryPrimitives.WriteInt32LittleEndian(props.AsSpan(at+4*axis),checked(I(props,at+4*axis)+delta[axis])); }
        Shift(4);Shift(0x58);
        return template with { Properties=props };
    }
    internal void AppendPod(UniversalLevelData data,IReadOnlyList<int> rows)
    { WritePod(data,rows);RequireFinalRows(data,rows,finalized:false); }
    private void WritePod(UniversalLevelData data,IReadOnlyList<int> rows)
    {
        Require(rows.SequenceEqual(_allocation.NewRows) && data.Scene().RowCount==_allocation.FinalRowCount,"exact append-only final physical pair required; no reuse, duplicate or reordered row binding");
        var sc=data.Scene();var sf=data.Subfiles()[3];byte[] scene=data.Bytes.AsSpan(sf.Offset,sf.Length).ToArray();
        int oldSize=I(scene,sc.PodComponent),oldCount=I(scene,sc.PodComponent+4);Require(oldCount==PodId,"native Dark Hollow pod namespace changed");
        int newSize=checked(oldSize+8);byte[] pods=new byte[newSize];BinaryPrimitives.WriteInt32LittleEndian(pods,newSize);BinaryPrimitives.WriteInt32LittleEndian(pods.AsSpan(4),oldCount+1);
        for(int i=0;i<oldCount;i++)BinaryPrimitives.WriteInt32LittleEndian(pods.AsSpan(8+4*i),checked(I(scene,sc.PodComponent+8+4*i)+4));
        scene.AsSpan(sc.PodComponent+8+4*oldCount,oldSize-8-4*oldCount).CopyTo(pods.AsSpan(12+4*oldCount));BinaryPrimitives.WriteInt32LittleEndian(pods.AsSpan(8+4*oldCount),oldSize+4);
        for(int i=0;i<2;i++){BinaryPrimitives.WriteUInt16LittleEndian(pods.AsSpan(oldSize+4+i*2),(ushort)(rows[i]|(i==1?0x8000:0)));scene[sc.RowsOffset+rows[i]*88+0x43]=checked((byte)oldCount);}
        int oldEnd=sc.PodComponent+oldSize;int Move(int value)=>value>=oldEnd?checked(value+8):value;
        byte[] rewritten=new byte[Math.Max(scene.Length,checked(sc.UsedEnd+8))];scene.AsSpan(0,sc.PodComponent).CopyTo(rewritten);pods.CopyTo(rewritten,sc.PodComponent);scene.AsSpan(oldEnd,sc.UsedEnd-oldEnd).CopyTo(rewritten.AsSpan(oldEnd+8));
        foreach(int fixup in sc.Fixups)BinaryPrimitives.WriteInt32LittleEndian(rewritten.AsSpan(Move(fixup)),Move(I(scene,fixup)));
        int fixupsAt=Move(sc.FixupsOffset);for(int i=0;i<sc.Fixups.Count;i++)BinaryPrimitives.WriteInt32LittleEndian(rewritten.AsSpan(fixupsAt+4+4*i),Move(sc.Fixups[i]));
        if(rewritten.Length>sf.Length)data.GrowSubfile(3,UniversalLevelData.Align(rewritten.Length-sf.Length,2048));sf=data.Subfiles()[3];Array.Clear(data.Bytes,sf.Offset,sf.Length);rewritten.CopyTo(data.Bytes,sf.Offset);
    }
    internal void PrepareArtisansRescueModels(UniversalLevelData data)
    { if(_hostKey=="artisans")UniversalMagicArtisansRescueModels.Prepare(data); }
    internal void RequireFinalRows(UniversalLevelData data,IReadOnlyList<int> rows,bool finalized=true)
    {
        // Source-converted immutable asset regions retain their reviewed owners.
        // Scene bytes derive from the actual authenticated authored inputs.
        // Immutable admitted asset regions bind every original host/model/geometry
        // descriptor plus exact relocated imported model, native sound map/bank,
        // new property owners, native pod/fixups and visibility. This intentionally
        // refuses different assets, allocation layouts or edited terrain in this
        // first scoped certificate, rather than admitting generic mutable buffers.
        if(finalized && _hostKey!="artisans")
        {
            var owners=Resource<AssetOwners>("MagicPairAssetOwners.json","95bca7569c62303b711c04a7a33900ead30f5e3c560742e20bea3069101854c5");
            Require(data.Bytes.Length==owners.Length&&data.Subfiles()[3]==(owners.SceneOffset,owners.SceneLength),"asset/scene region allocation differs from source-bound import owner");
            foreach(var region in owners.Owners)Require(H(data.Bytes.AsSpan(region.Offset,region.Length).ToArray())==region.SHA256,"immutable asset owner changed: "+region.Role);
            Require(data.Bytes.AsSpan(owners.SceneOffset,owners.SceneLength).SequenceEqual(ExpectedScene()),"scene differs from authenticated native host plus actual source-derived translated rows/properties/pod/fixups");
        }
        if(finalized && _hostKey=="artisans")
        {
            var owners=Resource<AssetOwners>("MagicPairArtisansAssetOwners.json","3bf51743a5a0522e56e947f8fadab3f80da5f442c78c714b6cf9307ac051b81a");
            Require(data.Bytes.Length==owners.Length&&data.Subfiles()[3]==(owners.SceneOffset,owners.SceneLength),"Artisans asset/scene allocation differs from independently audited structural owners");
            foreach(var region in owners.Owners)Require(H(data.Bytes.AsSpan(region.Offset,region.Length).ToArray())==region.SHA256,"immutable Artisans asset owner changed: "+region.Role);
            Require(data.Bytes.AsSpan(owners.SceneOffset,owners.SceneLength).SequenceEqual(ExpectedScene()),"Artisans scene differs from original plus authenticated deletions and actual authored source group");
        }
        if(finalized)
        {
            var srcTextures=UniversalParticleDependencies.Records(_source);var dstTextures=UniversalParticleDependencies.Records(data);
            Require(srcTextures.TryGetValue(0,out var sourceTile)&&dstTextures.TryGetValue(0,out var targetTile)&&UniversalParticleDependencies.EquivalentTexture(sourceTile,_source.Vram(),targetTile,data.Vram()),"native death texture0 pixels/palette lost their source owner");
        }
        Require(rows.SequenceEqual(_allocation.NewRows)&&data.Scene().RowCount==_allocation.FinalRowCount,"final physical identity changed");var sc=data.Scene();int so=data.Subfiles()[3].Offset;
        Require(I(data.Bytes,so+sc.PodComponent+4)==PodId+1,"final pod namespace changed");int memberAt=I(data.Bytes,so+sc.PodComponent+8+4*PodId);
        Require(BinaryPrimitives.ReadUInt16LittleEndian(data.Bytes.AsSpan(so+sc.PodComponent+memberAt))==rows[0] && BinaryPrimitives.ReadUInt16LittleEndian(data.Bytes.AsSpan(so+sc.PodComponent+memberAt+2))==(rows[1]|0x8000),"final pod must contain exact pair once with native terminal bit");
        Require(I(data.Row(rows[0]),0)!=I(data.Row(rows[1]),0),"final properties cannot alias between native pod members");
        for(int i=0;i<2;i++)
        {
            var p=_placements[i];var actual=data.CaptureRow("darkhollow",rows[i]);var source=_source.CaptureRow("magiccrafters",p.SourceRowIndex);var expected=Rebase(source,p);
            byte[] expectedRow=source.Row.ToArray();BinaryPrimitives.WriteInt32LittleEndian(expectedRow.AsSpan(12),p.X);BinaryPrimitives.WriteInt32LittleEndian(expectedRow.AsSpan(16),p.Y);BinaryPrimitives.WriteInt32LittleEndian(expectedRow.AsSpan(20),p.Z);expectedRow[0x43]=checked((byte)PodId);expectedRow[0x4A]=255;
            Require(actual.Row.AsSpan(4).SequenceEqual(expectedRow.AsSpan(4)),"complete final actor descriptor differs from authenticated source and admitted pose/pod/visibility transformations");
            int propertyStart=I(actual.Row,0);Require(propertyStart>=sc.PropertiesComponent+4&&propertyStart+expected.Properties.Length<=sc.PropertiesEnd,"final complete properties allocation owner is missing");
            Require(actual.Row[0x43]==PodId&&actual.Row[0x48]==0&&actual.Row[0x49]==0&&actual.Row[0x4A]==255&&actual.ClassId==271&&I(actual.Row,12)==p.X&&I(actual.Row,16)==p.Y&&I(actual.Row,20)==p.Z,"postoverride final class/state/pod/visibility/pose contract changed");
            byte[] actualBytes=actual.Properties.ToArray(),expectedBytes=expected.Properties.ToArray();foreach(var pointer in expected.InternalPointers){actualBytes.AsSpan(pointer.Offset,4).Clear();expectedBytes.AsSpan(pointer.Offset,4).Clear();}
            Require(actualBytes.SequenceEqual(expectedBytes)&&actual.InternalPointers.SequenceEqual(expected.InternalPointers),"postallocation selector/index/path/flag resource bytes or local pointer owners changed");
            Require(actual.Row[0x46]==source.Row[0x46]&&actual.Row[0x53]==source.Row[0x53],"native yaw/reward fields changed");
        }
    }
}
