using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Scene;

internal sealed record NativeMobyScenePrivateReference(string Role,int FieldOffset,int TargetOffset);
internal sealed record NativeMobySceneAppendRequest(NativeMobyDependencyInstantiationRequest Instances,
    IReadOnlyList<NativeMobyScenePrivateReference> InternalReferences);

internal sealed class NativeMobySceneAppendSource
{
    private readonly byte[] _bytes;
    private NativeMobySceneAppendSource(NativeAddressSpace original,byte[] bytes)
    {
        Original=original;_bytes=bytes;Sha256=Hash(bytes);
        int at=0x88;
        for(int i=0;i<7;i++)at=ComponentEnd(at,8);
        MobyOffset=at;PropertiesOffset=ComponentEnd(at,8);RowTableOffset=at+8;
        PlacedCount=Count(at+4,256,"placed row");
        if((long)RowTableOffset+(long)PlacedCount*0x58>PropertiesOffset)
            throw new InvalidDataException("Native placed rows exceed their Moby component capacity.");
        PropertiesEnd=ComponentEnd(PropertiesOffset,4);
        at=ComponentEnd(PropertiesEnd,4);FixupOffset=ComponentEnd(at,4);
        FixupCount=Count(FixupOffset,bytes.Length/4,"fixup");
        if((long)FixupOffset+4+(long)FixupCount*4>bytes.Length)throw new InvalidDataException("Native scene fixup list is out of bounds.");
        FixupEnd=FixupOffset+4+FixupCount*4;
        if(bytes.AsSpan(FixupEnd).ContainsAnyExcept((byte)0))throw new InvalidDataException("Native scene tail is not certified zero padding.");
        HashSet<int> fields=[];HashSet<int> rowFields=Enumerable.Range(0,PlacedCount).Select(i=>RowTableOffset+i*0x58).ToHashSet();
        for(int i=0;i<FixupCount;i++)
        {
            uint raw=Word(bytes,FixupOffset+4+i*4);
            if(raw>int.MaxValue)throw new InvalidDataException("Native fixup field is out of bounds.");
            int field=(int)raw;
            if(!fields.Add(field))throw new InvalidDataException("Native scene contains a duplicate fixup field.");
            if(field%4!=0||(!rowFields.Contains(field)&&(field<PropertiesOffset+4||field>PropertiesEnd-4)))
                throw new InvalidDataException("Native fixup field is not an owned row pointer or properties field.");
            uint target=Word(bytes,field);
            if(target%4!=0||target<PropertiesOffset+4||target>=PropertiesEnd)
                throw new InvalidDataException("Native fixup target is not an owned original properties address; unknown movement is refused.");
        }
        if(!rowFields.IsSubsetOf(fields))throw new InvalidDataException("A native row pointer field is missing from the fixup list.");
        return;

        int ComponentEnd(int offset,int minimum)
        {
            if(offset<0||offset>bytes.Length-4)throw new InvalidDataException("Native scene component header is out of bounds.");
            uint length=Word(bytes,offset);
            if(length<minimum||length%4!=0||length>bytes.Length-offset)
                throw new InvalidDataException("Native scene component length is invalid.");
            return offset+(int)length;
        }
        int Count(int offset,int maximum,string name)
        {
            if(offset>bytes.Length-4||Word(bytes,offset)>maximum)throw new InvalidDataException("Native scene "+name+" count exceeds its bounded schema.");
            return (int)Word(bytes,offset);
        }
    }
    public NativeAddressSpace Original { get; }
    public string Sha256 { get; }
    public int MobyOffset { get; }
    public int RowTableOffset { get; }
    public int PlacedCount { get; }
    public int PropertiesOffset { get; }
    public int PropertiesEnd { get; }
    public int FixupOffset { get; }
    public int FixupEnd { get; }
    public int FixupCount { get; }
    public int DynamicCapacity=>(PropertiesOffset-RowTableOffset-PlacedCount*0x58)/0x70;
    public byte[] CopyScene()=>_bytes.ToArray();
    public void VerifySource(ReadOnlySpan<byte> bytes)
    {if(bytes.Length!=_bytes.Length||Hash(bytes)!=Sha256)throw new InvalidDataException("Native scene source full preimage changed.");}
    public static NativeMobySceneAppendSource Capture(NativeAddressSpace original,string expectedSha,Func<NativeByteExtent,byte[]> reader)
    {
        ArgumentNullException.ThrowIfNull(original);ArgumentNullException.ThrowIfNull(reader);
        if(original.Kind!=NativeAddressKind.Scene||original.Revision!=NativeAddressRevision.Original||
            original.ByteLength<0x800||original.ByteLength>0x400000||original.ByteLength%0x800!=0)
            throw new InvalidDataException("Native scene capture requires a bounded sector-aligned original Scene domain.");
        byte[] bytes=reader(new(original,0,original.ByteLength))?.ToArray()??throw new InvalidDataException("Native scene reader returned no preimage.");
        if(bytes.LongLength!=original.ByteLength||Hash(bytes)!=expectedSha)throw new InvalidDataException("Native scene full preimage hash/length changed.");
        return new(original,bytes);
    }
    internal static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static uint Word(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}

/// <summary>
/// A bounded inner-scene append, not an actor behavior linker. Only a validated
/// source factory may supply schemas. Old pointers must remain below the genuine
/// properties insertion; unknown moving pointer classes are refused. The native
/// Moby component shares its remaining bytes with dynamic rows plus 0x18-byte
/// properties, so every placed row explicitly debits floor(remaining/0x70).
/// </summary>
internal sealed class NativeMobySceneAppendPlan
{
    private readonly NativeMobySceneAppendSource _source;
    private readonly byte[] _scene;
    private NativeMobySceneAppendPlan(NativeMobySceneAppendSource source,NativeMobyDependencyInstantiationPlan allocation,
        NativeMobyDependencyClosurePlan graph,byte[] scene,int fixupCount)
    {_source=source;Allocation=allocation;Graph=graph;_scene=scene.ToArray();FixupCountAfter=fixupCount;FinalSha256=NativeMobySceneAppendSource.Hash(scene);}
    public int PlacedCountBefore=>_source.PlacedCount;
    public int PlacedCountAfter=>Allocation.FinalRowCount;
    public int FixupCountAfter { get; }
    public int DynamicCapacityBefore=>_source.DynamicCapacity;
    public int DynamicCapacityAfter=>(_source.PropertiesOffset-_source.RowTableOffset-PlacedCountAfter*0x58)/0x70;
    public bool BehaviorClosureProven=>false;
    public bool RuntimeFitProven=>false;
    public bool PublicationApproved=>false;
    public NativeMobyDependencyClosurePlan Graph { get; }
    public NativeMobyDependencyInstantiationPlan Allocation { get; }
    public string FinalSha256 { get; }
    public byte[] CopyScene()=>_scene.ToArray();
    public void Verify(ReadOnlySpan<byte> original,ReadOnlySpan<byte> final)
    {
        _source.VerifySource(original);
        if(final.Length!=_scene.Length||!final.SequenceEqual(_scene))throw new InvalidDataException("Native scene exact final readback changed.");
    }
    public static NativeMobySceneAppendPlan Compile(NativeMobySceneAppendSource source,NativeAddressSpace finalScene,
        IReadOnlyList<NativeAddressSpace> finalDomains,NativeAddressSpace runtimeExe,
        IEnumerable<NativeMobySceneAppendRequest> requests,Func<NativeByteExtent,byte[]> reader)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(finalScene);
        ArgumentNullException.ThrowIfNull(finalDomains);ArgumentNullException.ThrowIfNull(requests);ArgumentNullException.ThrowIfNull(reader);
        if(finalScene.Kind!=NativeAddressKind.Scene||finalScene.Revision!=NativeAddressRevision.Final||finalScene.ByteLength!=source.Original.ByteLength)
            throw new InvalidDataException("Native scene append needs the exact-sized final Scene domain.");
        if(!finalDomains.Contains(finalScene))throw new InvalidDataException("Native scene append has no canonical final domain binding.");
        List<NativeMobySceneAppendRequest> batch=[];
        foreach(var request in requests)
        {
            if(batch.Count==256)throw new InvalidDataException("Native scene append raw request budget exceeded.");
            if(request==null||request.Instances==null||request.Instances.Template==null||request.InternalReferences==null)
                throw new InvalidDataException("Native scene append request schema is missing.");
            if(request.InternalReferences.Count>4096)throw new InvalidDataException("Native scene internal pointer request budget exceeded.");
            batch.Add(request with { InternalReferences=Array.AsReadOnly(request.InternalReferences.ToArray()) });
        }
        if(batch.Count==0)throw new InvalidDataException("Native scene append requires a nonempty request batch.");
        if(batch.SelectMany(r=>r.Instances.Template.Rows).Any(r=>r.Row.Extent.Space.Kind!=NativeAddressKind.Scene||r.Properties.Extent.Space.Kind!=NativeAddressKind.Scene))
            throw new InvalidDataException("Native scene private extraction requires actual original Scene address domains.");
        byte[] original=reader(new(source.Original,0,source.Original.ByteLength))??throw new InvalidDataException("Native scene source reader returned no preimage.");
        source.VerifySource(original);original=original.ToArray();
        long rowCount=batch.Sum(r=>(long)r.Instances.Template.Rows.Count);
        long privateBytes=batch.Sum(r=>r.Instances.Template.Rows.Sum(s=>s.Properties.Extent.ByteLength));
        if(rowCount>256||privateBytes>source.Original.ByteLength||privateBytes<=0)
            throw new InvalidDataException("Native scene private capacity exceeds the bounded append schema.");
        int firstRow=source.RowTableOffset+source.PlacedCount*0x58;
        if(rowCount*0x58>source.PropertiesOffset-firstRow)
            throw new InvalidDataException("Native scene dynamic row capacity is exhausted.");
        if(original.AsSpan(firstRow,checked((int)rowCount*0x58)).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Native scene dynamic row reservation has nonzero source bytes.");
        int addedProperties=(int)privateBytes;
        long refCount=batch.Sum(r=>(long)r.InternalReferences.Count);
        long finalFixupEnd=(long)source.FixupEnd+privateBytes+4*(rowCount+refCount);
        if(finalFixupEnd>original.Length)throw new InvalidDataException("Native scene zero-tail capacity is exhausted.");
        var allocation=NativeMobyDependencyInstantiationPlan.Create(new(finalScene,firstRow,rowCount*0x58),
            new(finalScene,source.PropertiesEnd,privateBytes),source.RowTableOffset,source.PlacedCount,finalDomains,runtimeExe)
            .Allocate(batch.Select(r=>r.Instances));
        List<NativeMobyDependencyNode> nodes=[];List<NativeByteExtent> writes=[];
        HashSet<string> sharedIds=[];
        foreach(var family in allocation.Families)
        {
            // A resolved reference receipt omits its original unit/base encoding.
            // Do not guess that schema while importing a precompiled shared graph.
            if(family.Template.Shared.References.Count!=0)
                throw new InvalidDataException("Shared typed reference schemas require explicit graph composition before this bounded scene stage.");
            foreach(var n in family.Template.Shared.Nodes)
            {
                if(!sharedIds.Add(n.Id))continue;
                var installs=family.Template.Shared.Installs.Where(i=>i.NodeId==n.Id).Select(i=>new NativeMobyDependencyInstall(i.InstallId,i.Destination)).ToArray();
                if(installs.Any(i=>i.Destination.Space==finalScene))
                    throw new InvalidDataException("Shared resources cannot install into preserved native scene ownership during this private append stage.");
                writes.AddRange(installs.Select(i=>i.Destination));
                byte[]? payload=n.PayloadSha256==null?null:family.Template.Shared.CopyPayload(n.Id);
                nodes.Add(new(n.Id,n.Kind,n.Resolution,n.Sharing,n.EvidenceSha256,n.Dependencies,n.Reads,
                    payload==null?null:_=>new(payload,installs,[]),n.OwnerInstanceId,Preserved:n.Preserved));
            }
        }
        List<int> addedFixups=[];
        foreach(var family in allocation.Families)
        {
            var request=batch.Single(r=>r.Instances.Id==family.Id);
            if(request.InternalReferences.Any(r=>r==null||!family.Rows.Any(a=>a.Source.Role==r.Role)))
                throw new InvalidDataException("Native internal pointer names an unknown private role.");
            foreach(var row in family.Rows)
            {
                var refs=request.InternalReferences.Where(r=>r.Role==row.Source.Role).OrderBy(r=>r.FieldOffset).ToArray();
                HashSet<int> fields=[];List<NativeMobyDependencyReference> pointers=[];
                foreach(var r in refs)
                {
                    if(r.FieldOffset<0||r.FieldOffset%4!=0||r.FieldOffset>row.Properties.ByteLength-4||!fields.Add(r.FieldOffset)||
                        r.TargetOffset<0||r.TargetOffset%4!=0||r.TargetOffset>=row.Properties.ByteLength||
                        row.Source.OwnerFields.Any(f=>f.PropertyByteOffset==r.FieldOffset))
                        throw new InvalidDataException("Native private internal pointer field/target is unsupported or duplicated.");
                    uint before=checked((uint)(row.Source.Properties.Extent.Offset-row.Source.PropertiesPointerBaseOffset+r.TargetOffset));
                    pointers.Add(new("internal."+r.FieldOffset,r.FieldOffset,row.OwnerId+".props",r.TargetOffset,4,before));
                }
                Add(row.OwnerId+".props",NativeMobyDependencyKind.InstanceProperties,pointers.Count==0?[]:[row.OwnerId+".props"],
                    [row.Source.Properties],c=>row.RemapPrivateOwnerIndices(c.Read(row.Source.Properties.Id)),row.Properties,row.OwnerId,pointers);
                Add(row.OwnerId+".row",NativeMobyDependencyKind.ObjectIndex,[row.OwnerId+".props"],
                    [row.Source.Row],c=>c.Read(row.Source.Row.Id),row.Row,row.OwnerId,[row.PropertiesReference]);
                nodes.Add(new(row.OwnerId,NativeMobyDependencyKind.Instance,NativeMobyDependencyResolution.Aggregate,
                    NativeMobyDependencySharing.Immutable,row.Source.Row.ExpectedSha256,[row.OwnerId+".row",row.OwnerId+".props",..sharedIds.Order(StringComparer.Ordinal)],[]));
                addedFixups.Add(checked((int)row.Row.Offset));
                addedFixups.AddRange(refs.Select(r=>checked((int)row.Properties.Offset+r.FieldOffset)));
            }
        }
        Add("scene.placed-count",NativeMobyDependencyKind.ObjectIndex,[],[Read(source.MobyOffset+4,4)],
            _=>WordBytes((uint)allocation.FinalRowCount),new(finalScene,source.MobyOffset+4,4));
        Add("scene.properties-length",NativeMobyDependencyKind.InstanceProperties,[],[Read(source.PropertiesOffset,4)],
            c=>WordBytes(checked(NativeMobySceneAppendSource.Word(c.Read("native"),0)+(uint)addedProperties)),new(finalScene,source.PropertiesOffset,4));
        Add("scene.pod-collision",NativeMobyDependencyKind.SceneFixup,[],[Read(source.PropertiesEnd,source.FixupOffset-source.PropertiesEnd)],
            c=>c.Read("native"),new(finalScene,source.PropertiesEnd+addedProperties,source.FixupOffset-source.PropertiesEnd));
        int finalFixups=source.FixupCount+addedFixups.Count;
        Add("scene.fixups",NativeMobyDependencyKind.SceneFixup,allocation.Rows.Select(r=>r.OwnerId).ToArray(),
            [Read(source.FixupOffset,source.FixupEnd-source.FixupOffset)],c=>
            {
                byte[] bytes=new byte[4+finalFixups*4];c.Read("native").CopyTo(bytes,0);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes,(uint)finalFixups);
                for(int i=0;i<addedFixups.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4+(source.FixupCount+i)*4),(uint)addedFixups[i]);
                return bytes;
            },new(finalScene,source.FixupOffset+addedProperties,4+finalFixups*4));
        List<NativeMobyDependencyPreservation> preserved=[];
        Preserve(0,0,source.PropertiesEnd);
        Preserve(source.FixupEnd,checked((int)finalFixupEnd),original.Length-checked((int)finalFixupEnd));
        nodes.Add(new("scene.native-preserved",NativeMobyDependencyKind.SceneFixup,NativeMobyDependencyResolution.DestinationResident,
            NativeMobyDependencySharing.Immutable,source.Sha256,[],[Read(0,original.Length)],Preserved:preserved));
        var graph=NativeMobyDependencyClosureCompiler.Compile(nodes,nodes.Select(n=>n.Id),reader);
        allocation.RequireSharedBinding(graph);
        byte[] final=new byte[original.Length];bool[] covered=new bool[final.Length];
        foreach(var node in graph.Nodes)foreach(var p in node.Preserved.Where(p=>p.Final.Space==finalScene))
        {
            var read=node.Reads.Single(r=>r.Id==p.ReadId);
            byte[] bytes=reader(read.Extent)??throw new InvalidDataException("Native scene preservation source is missing.");
            if(bytes.LongLength!=read.Extent.ByteLength||NativeMobySceneAppendSource.Hash(bytes)!=read.ExpectedSha256)
                throw new InvalidDataException("Native scene preservation preimage changed.");
            bytes.AsSpan(p.ReadOffset,(int)p.Final.ByteLength).CopyTo(final.AsSpan((int)p.Final.Offset));
            Array.Fill(covered,true,(int)p.Final.Offset,(int)p.Final.ByteLength);
        }
        foreach(var install in graph.Installs.Where(i=>i.Destination.Space==finalScene))
        {
            graph.CopyPayload(install.NodeId).CopyTo(final,(int)install.Destination.Offset);
            Array.Fill(covered,true,(int)install.Destination.Offset,(int)install.Destination.ByteLength);
        }
        if(covered.Any(c=>!c))throw new InvalidDataException("Native scene graph leaves an unowned final byte.");
        // Independent native parse checks the resulting owned fields and targets,
        // including the complete appended list; no generated padding is a target.
        var parsed=NativeMobySceneAppendSource.Capture(new(NativeAddressKind.Scene,"scene-readback",NativeAddressRevision.Original,final.Length),
            NativeMobySceneAppendSource.Hash(final),extent=>final.AsSpan((int)extent.Offset,(int)extent.ByteLength).ToArray());
        if(parsed.PlacedCount!=allocation.FinalRowCount||parsed.FixupCount!=finalFixups)
            throw new InvalidDataException("Native scene structural readback changed.");
        return new(source,allocation,graph,final,finalFixups);

        NativeMobyDependencyRead Read(int offset,int length)=>new("native",new(source.Original,offset,length),NativeMobySceneAppendSource.Hash(original.AsSpan(offset,length)));
        void Add(string id,NativeMobyDependencyKind kind,IReadOnlyList<string> deps,IReadOnlyList<NativeMobyDependencyRead> reads,
            Func<NativeMobyDependencyBuildContext,byte[]> build,NativeByteExtent destination,string? owner=null,IReadOnlyList<NativeMobyDependencyReference>? refs=null)
        {
            writes.Add(destination);nodes.Add(new(id,kind,NativeMobyDependencyResolution.ImportFromSource,
                owner==null?NativeMobyDependencySharing.Immutable:NativeMobyDependencySharing.PrivateInstance,
                reads[0].ExpectedSha256,deps,reads,c=>new(build(c),[new(id,destination)],refs??[]),owner));
        }
        void Preserve(int sourceStart,int finalStart,int length)
        {
            int end=finalStart+length,cursor=finalStart;
            foreach(var extent in writes.Where(w=>w.Space==finalScene&&w.Offset<end&&w.EndExclusive>finalStart).OrderBy(w=>w.Offset))
            {
                int start=Math.Max(finalStart,(int)extent.Offset);
                if(start>cursor)preserved.Add(new("native",sourceStart+cursor-finalStart,new(finalScene,cursor,start-cursor)));
                cursor=Math.Max(cursor,Math.Min(end,(int)extent.EndExclusive));
            }
            if(cursor<end)preserved.Add(new("native",sourceStart+cursor-finalStart,new(finalScene,cursor,end-cursor)));
        }
    }
    private static byte[] WordBytes(uint value){byte[] bytes=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(bytes,value);return bytes;}
}
