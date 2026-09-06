using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Loader/table evidence is not a memory-fit certificate. The retail generic loop starts
/// at slot 1, stops at the first nonpositive root and ends before slot 64. The separate
/// shared class table has 512 entries. Slot 0 belongs to the special player stream.
/// </summary>
internal sealed class NativeMobyModelLoaderCertificate
{
    private NativeMobyModelLoaderCertificate(IEnumerable<ushort> classes,bool retail)
    {
        ArgumentNullException.ThrowIfNull(classes);
        ushort[] ids=classes.Take(65).ToArray();
        if(ids.Length>64||ids.Any(c=>c>=512)||ids.Distinct().Count()!=ids.Length)
            throw new InvalidDataException("Shared model registration exceeds its native 64/512 bounds or repeats a class.");
        SharedClasses=Array.AsReadOnly(ids.Order().ToArray());IsRetailCertified=retail;
    }
    internal IReadOnlyList<ushort> SharedClasses { get; }
    internal bool IsRetailCertified { get; }
    internal static NativeMobyModelLoaderCertificate CaptureRetail(ReadOnlySpan<byte> executable,
        ReadOnlySpan<byte> peteHeader,ReadOnlySpan<byte> peteRuntimeCopy)
    {
        Require(executable,0x66000,"a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998");
        Require(peteHeader,0x800,"b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1");
        // The native copy extent (not merely PETE's shorter declared WAD entry) is the owner.
        Require(peteRuntimeCopy,0x39C48,"1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d");
        if(Word(peteHeader,0)!=peteRuntimeCopy.Length)throw new InvalidDataException("Native PETE copy owner changed.");
        List<ushort> classes=[];bool ended=false;uint previous=0;
        for(int i=0;i<64;i++)
        {
            uint root=Word(peteHeader,4+i*8),id=Word(peteHeader,8+i*8);
            if(root==0){ended=true;if(id!=0)throw new InvalidDataException("Native PETE terminator retains a class.");continue;}
            if(ended||root<0x800||root%4!=0||root<=previous||root+4L>0x800L+peteRuntimeCopy.Length||id>=512)
                throw new InvalidDataException("Native PETE shared class/package owner changed.");
            classes.Add((ushort)id);previous=root;
        }
        return new(classes,true);
    }
    // Synthetic tests do not issue the native certificate required by the production adapter.
    internal static NativeMobyModelLoaderCertificate DecodeFixture(IEnumerable<ushort> classes)=>new(classes,false);
    private static void Require(ReadOnlySpan<byte> b,int length,string hash)
    {if(b.Length!=length||Hash(b)!=hash)throw new InvalidDataException("Native model loader/PETE source certificate changed.");}
    internal static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    internal static uint Word(ReadOnlySpan<byte> b,int o)=>BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);
}

/// <summary>Complete source snapshot with decoded model ownership, not a package codec.</summary>
internal sealed class NativeMobyModelRegistrationSource
{
    private readonly byte[] _source;
    private NativeMobyModelRegistrationSource(NativeAddressSpace original,ReadOnlySpan<byte> bytes)
    {
        if(original.Kind!=NativeAddressKind.LevelData||original.Revision!=NativeAddressRevision.Original||original.ByteLength!=bytes.Length||bytes.Length<0x2800||bytes.Length%0x800!=0)
            throw new InvalidDataException("Native registration source has no complete original level address domain.");
        _source=bytes.ToArray();Original=original;SourceSha256=Hash(bytes);
        int cursor=0x800;bool descriptorEnd=false;
        for(int i=0;i<10;i++)
        {
            uint offset=Word(bytes,i*8),length=Word(bytes,i*8+4);
            if(offset==0&&length==0){descriptorEnd=true;continue;}
            if(descriptorEnd||offset!=cursor||length==0||length%0x800!=0||offset+(long)length>bytes.Length)
                throw new InvalidDataException("Native model source descriptors do not own a complete packed level.");
            cursor=checked(cursor+(int)length);
        }
        if(cursor!=bytes.Length||Word(bytes,0x18)==0||bytes.Slice(0x1D0,0x630).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Native registration source header or complete descriptor coverage changed.");
        ModelOffset=checked((int)Word(bytes,0x10));ModelLength=checked((int)Word(bytes,0x14));
        if(ModelOffset<0x800||ModelLength==0||ModelOffset+(long)ModelLength!=Word(bytes,0x18))
            throw new InvalidDataException("Native model owner does not end at the next scene subfile.");
        List<NativeMobySourceRegistration> roots=[];HashSet<ushort> classes=[];bool ended=false;uint previous=0;
        for(int i=0;i<64;i++)
        {
            uint root=Word(bytes,0x50+i*4);ushort cls=BinaryPrimitives.ReadUInt16LittleEndian(bytes[(0x150+i*2)..]);
            if(i==0&&(root==0||cls!=0))throw new InvalidDataException("Native slot 0 must retain the special Spyro stream.");
            if(root==0){ended=true;if(cls!=0)throw new InvalidDataException("Native root terminator retains a class ID.");continue;}
            if(ended)throw new InvalidDataException("Native model registrations must form a contiguous positive prefix.");
            if(root>int.MaxValue)throw new InvalidDataException("Native loader requires positive signed root offsets.");
            if(i!=0&&cls==0)throw new InvalidDataException("Generic registrations cannot replace special class 0.");
            if(cls>=512)throw new InvalidDataException("Native class identity exceeds the 512-pointer table.");
            if(root<ModelOffset||root+4L>ModelOffset+(long)ModelLength||root%4!=0)
                throw new InvalidDataException("Native registration points outside its aligned model owner.");
            if(root<=previous||!classes.Add(cls))throw new InvalidDataException("Native roots must be ascending and class identities unique.");
            roots.Add(new(i,cls,root));previous=root;
        }
        Registrations=Array.AsReadOnly(roots.ToArray());
    }
    internal NativeAddressSpace Original { get; }
    internal string SourceSha256 { get; }
    internal int ModelOffset { get; }
    internal int ModelLength { get; }
    internal int ModelEnd=>checked(ModelOffset+ModelLength);
    internal IReadOnlyList<NativeMobySourceRegistration> Registrations { get; }
    internal byte[] CopyHeader()=>_source.AsSpan(0,0x800).ToArray();
    internal byte[] CopyModels()=>_source.AsSpan(ModelOffset,ModelLength).ToArray();
    internal void RequireSource(Func<NativeByteExtent,byte[]> read)
    {byte[] b=read(new(Original,0,Original.ByteLength));if(b==null||b.Length!=_source.Length||Hash(b)!=SourceSha256)throw new InvalidDataException("Native registration complete source preimage changed.");}
    internal static NativeMobyModelRegistrationSource CaptureInventory(NativeMobySourceLevel level,
        NativeAddressSpace original,Func<NativeByteExtent,byte[]> read)
    {
        ArgumentNullException.ThrowIfNull(level);
        byte[] b=read(new(original,0,original.ByteLength))??throw new InvalidDataException("Native model source reader returned no complete source bytes.");
        if(b.Length!=level.SourceDataByteLength||Hash(b)!=level.SourceDataSha256)
            throw new InvalidDataException("Native model source differs from its source-inventory certificate.");
        return new(original,b);
    }
    internal static NativeMobyModelRegistrationSource CaptureExactTown(NativeAddressSpace original,Func<NativeByteExtent,byte[]> read)=>
        CaptureExact(original,read,"7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0");
    internal static NativeMobyModelRegistrationSource CaptureExactPeace(NativeAddressSpace original,Func<NativeByteExtent,byte[]> read)=>
        CaptureExact(original,read,"c140bd422471c66cd57bd85b29912819f798f5b080779153674714889306c9d3");
    private static NativeMobyModelRegistrationSource CaptureExact(NativeAddressSpace original,Func<NativeByteExtent,byte[]> read,string hash)
    {byte[] b=read(new(original,0,original.ByteLength));if(Hash(b)!=hash)throw new InvalidDataException("Native complete model source certificate changed.");return new(original,b);}
    internal static NativeMobyModelRegistrationSource DecodeFixture(NativeAddressSpace original,ReadOnlySpan<byte> b)=>new(original,b);
    private static uint Word(ReadOnlySpan<byte>b,int o)=>NativeMobyModelLoaderCertificate.Word(b,o);
    private static string Hash(ReadOnlySpan<byte>b)=>NativeMobyModelLoaderCertificate.Hash(b);
}

/// <summary>
/// A code-owned closure's immutable package output. This producer does not infer internal
/// pointer schemas from bytes. The existing native family factory remains responsible for
/// complete animation/face/sound/effect semantics and typed internal references. Full source
/// interval extraction, canonical domains and closure context are checked again here.
/// </summary>
internal sealed class NativeMobyModelPackageBinding
{
    private readonly NativeMobyDependencyClosurePlan _closure;
    private NativeMobyModelPackageBinding(NativeMobyModelRegistrationSource source,ushort actor,
        NativeMobyDependencyClosurePlan closure,string nodeId,string readId,string policy)
    {
        if(string.IsNullOrWhiteSpace(policy)||actor==0||actor>=512)throw new InvalidDataException("A generic package requires a non-player class below 512 and an explicit native relocation policy.");
        var root=source.Registrations.SingleOrDefault(r=>r.ActorId==actor&&r.Slot>0)
            ??throw new InvalidDataException("Donor package class has no native generic registration.");
        var node=closure.Nodes.SingleOrDefault(n=>n.Id==nodeId)??throw new InvalidDataException("No selected package node.");
        if(node.Kind!=NativeMobyDependencyKind.ActorPackage||node.Resolution!=NativeMobyDependencyResolution.ImportFromSource||
            closure.Nodes.Any(n=>n.Sharing!=NativeMobyDependencySharing.Immutable))
            throw new InvalidDataException("Model registration requires a closed immutable package, not mutable private instance state.");
        var read=node.Reads.SingleOrDefault(r=>r.Id==readId)??throw new InvalidDataException("No package extraction preimage.");
        long end=root.Slot+1<source.Registrations.Count?source.Registrations[root.Slot+1].RootDataOffset:source.ModelEnd;
        if(read.Extent.Space!=source.Original||read.Extent.Offset!=root.RootDataOffset||read.Extent.EndExclusive!=end)
            throw new InvalidDataException("Package extraction must bind its complete registered source interval and canonical address domain.");
        var installs=closure.Installs.Where(i=>i.NodeId==nodeId).ToArray();
        if(installs.Length!=1||installs[0].Destination.Space.Kind!=NativeAddressKind.LevelData||installs[0].Destination.Space.Revision!=NativeAddressRevision.Final||
            (installs[0].Destination.Offset|installs[0].Destination.ByteLength)%4!=0)
            throw new InvalidDataException("Package output requires one aligned final level-data install.");
        Source=source;ActorId=actor;NodeId=nodeId;ReadId=readId;Policy=policy;Destination=installs[0].Destination;_closure=closure;
        PayloadSha256=Hash(closure.CopyPayload(nodeId));
        if(PayloadSha256!=installs[0].PayloadSha256)throw new InvalidDataException("Package output hash differs from its closure install.");
        Identity=Hash(System.Text.Encoding.UTF8.GetBytes($"{actor}|{nodeId}|{readId}|{policy}|{source.SourceSha256}|{closure.Fingerprint}|{Destination.Space.Kind}|{Destination.Space.OwnerIdentity}|{Destination.Offset}|{Destination.ByteLength}|{PayloadSha256}"));
    }
    internal NativeMobyModelRegistrationSource Source { get; }
    internal ushort ActorId { get; }
    internal string NodeId { get; }
    internal string ReadId { get; }
    internal string Policy { get; }
    internal NativeByteExtent Destination { get; }
    internal string PayloadSha256 { get; }
    internal string Identity { get; }
    internal byte[] CopyPayload()=>_closure.CopyPayload(NodeId);
    internal static NativeMobyModelPackageBinding Capture(NativeMobyModelRegistrationSource source,ushort actor,
        NativeMobyDependencyClosurePlan closure,string nodeId,string readId,string policy,Func<NativeByteExtent,byte[]> read)
    {
        source.RequireSource(read);
        foreach(var preimage in closure.Nodes.SelectMany(n=>n.Reads))
        {
            byte[] bytes=read(preimage.Extent);
            if(bytes==null||bytes.LongLength!=preimage.Extent.ByteLength||Hash(bytes)!=preimage.ExpectedSha256)
                throw new InvalidDataException("Registered package closure source preimage changed.");
        }
        return new(source,actor,closure,nodeId,readId,policy);
    }
    internal void RequireClosure(NativeMobyDependencyClosurePlan final)
    {
        foreach(var original in _closure.Nodes)
        {
            var n=final.Nodes.SingleOrDefault(n=>n.Id==original.Id);
            if(n==null||n.Kind!=original.Kind||n.Resolution!=original.Resolution||n.Sharing!=original.Sharing||n.EvidenceSha256!=original.EvidenceSha256||
                n.OwnerInstanceId!=original.OwnerInstanceId||n.PayloadSha256!=original.PayloadSha256||!n.Dependencies.SequenceEqual(original.Dependencies)||
                !n.Reads.SequenceEqual(original.Reads)||!n.Preserved.SequenceEqual(original.Preserved))
                throw new InvalidDataException("Registered package dependency context changed after allocation.");
        }
        foreach(var install in _closure.Installs)if(!final.Installs.Contains(install))throw new InvalidDataException("Registered package install address domain changed.");
        foreach(var reference in _closure.References)if(!final.References.Contains(reference))throw new InvalidDataException("Registered package typed reference changed.");
        if(Hash(final.CopyPayload(NodeId))!=PayloadSha256)throw new InvalidDataException("Registered package payload changed.");
    }
    private static string Hash(ReadOnlySpan<byte>b)=>NativeMobyModelLoaderCertificate.Hash(b);
}

internal sealed record NativeMobyModelRegistrationAppend(string NodeId,ushort ActorId,int Slot,
    NativeByteExtent RootField,NativeByteExtent ClassField,NativeByteExtent Package,
    NativeMobyDependencyReference Reference);

/// <summary>
/// Detached, deterministic append transaction. Existing roots—including unplaced resources—
/// are never reclaimed. File-sector growth and slot capacity are proved here; RAM, texture,
/// sound, behavior and instance capacity remain separate mandatory producer certificates.
/// </summary>
internal sealed class NativeMobyModelRegistrationPlan
{
    // Ingestion bound only, not a native capacity. Bound before materializing/deduplicating
    // hostile or infinite streams of repeated declarations; output is still capped at 63.
    internal const int MaximumRequestDeclarations=4096;
    private readonly byte[] _header;
    private readonly NativeMobyModelPackageBinding[] _bindings;
    private readonly NativeMobyModelRegistrationSource _source;
    private NativeMobyModelRegistrationPlan(NativeMobyModelRegistrationSource source,NativeMobyModelLoaderCertificate loader,
        NativeAddressSpace final,IEnumerable<NativeMobyModelPackageBinding> requests,Func<NativeByteExtent,byte[]> read)
    {
        source.RequireSource(read);_source=source;Loader=loader;Final=final;
        if(final.Kind!=NativeAddressKind.LevelData||final.Revision!=NativeAddressRevision.Final)
            throw new InvalidDataException("Model registration requires the final level-data address domain.");
        ArgumentNullException.ThrowIfNull(requests);
        var list=requests.Take(MaximumRequestDeclarations+1).ToArray();
        if(list.Length>MaximumRequestDeclarations)throw new InvalidDataException("Model registration request declaration budget exceeded before allocation.");
        if(list.Length==0||list.Any(p=>p==null))throw new InvalidDataException("Registration append requires at least one closed package.");
        List<NativeMobyModelPackageBinding> unique=[];
        foreach(var group in list.GroupBy(p=>p.NodeId,StringComparer.Ordinal))
        {
            var first=group.First();
            if(group.Any(p=>p.Identity!=first.Identity||p.Destination.Space!=first.Destination.Space||p.Source.Original!=first.Source.Original))
                throw new InvalidDataException("Repeated package identity has different payload, policy or address ownership.");
            unique.Add(first);
        }
        _bindings=unique.OrderBy(p=>p.ActorId).ThenBy(p=>p.NodeId,StringComparer.Ordinal).ToArray();
        if(_bindings.Select(p=>p.ActorId).Distinct().Count()!=_bindings.Length)
            throw new InvalidDataException("Requested package class conflict cannot share one global class pointer.");
        GenericCountBefore=source.Registrations.Count-1;
        GenericCountAfter=checked(GenericCountBefore+_bindings.Length);
        if(GenericCountAfter>63)throw new InvalidDataException("Native generic registration capacity is 63; existing roots cannot be reclaimed.");
        if(source.Registrations.Skip(1).Any(r=>loader.SharedClasses.Contains(r.ActorId)))
            throw new InvalidDataException("Destination local/shared class collision has no proved native ownership.");
        foreach(var p in _bindings)
        {
            if(p.Destination.Space!=final)throw new InvalidDataException("Package belongs to another final address domain.");
            if(loader.SharedClasses.Contains(p.ActorId))throw new InvalidDataException("Requested class is already owned by a shared PETE resource.");
            if(source.Registrations.Any(r=>r.ActorId==p.ActorId))throw new InvalidDataException("Requested class is already registered; resident equivalence requires its own certificate, not overwrite.");
            p.Source.RequireSource(read);
        }
        long bytes=_bindings.Sum(p=>p.Destination.ByteLength);
        ModelGrowthBytes=checked((int)((bytes+0x7FF)/0x800*0x800));
        if(final.ByteLength!=source.Original.ByteLength+ModelGrowthBytes)
            throw new InvalidDataException("Final level owner length does not equal sector-rounded model append growth.");
        FinalModelOffset=source.ModelOffset;FinalModelLength=checked(source.ModelLength+ModelGrowthBytes);
        _header=source.CopyHeader();Put(_header,0x14,(uint)FinalModelLength);
        for(int i=3;i<10;i++)if(Word(_header,i*8)!=0)Put(_header,i*8,checked(Word(_header,i*8)+(uint)ModelGrowthBytes));
        List<NativeMobyModelRegistrationAppend> entries=[];int offset=source.ModelEnd,slot=source.Registrations.Count;
        foreach(var p in _bindings)
        {
            if(p.Destination.Offset!=offset)throw new InvalidDataException("Package final extent differs from deterministic append position.");
            int field=0x50+slot*4,cls=0x150+slot*2;
            if(Word(_header,field)!=0||BinaryPrimitives.ReadUInt16LittleEndian(_header.AsSpan(cls))!=0)
                throw new InvalidDataException("Native tail registration preimage is occupied.");
            Put(_header,field,(uint)offset);BinaryPrimitives.WriteUInt16LittleEndian(_header.AsSpan(cls),p.ActorId);
            entries.Add(new(p.NodeId,p.ActorId,slot,new(final,field,4),new(final,cls,2),p.Destination,
                new("actor-root",0,p.NodeId,0,4,0)));
            offset=checked(offset+(int)p.Destination.ByteLength);slot++;
        }
        Packages=Array.AsReadOnly(entries.ToArray());
        Fingerprint=Hash(System.Text.Encoding.UTF8.GetBytes($"append-only-local-model-v1|{source.SourceSha256}|{final.OwnerIdentity}|{Hash(_header)}|{string.Join(',',loader.SharedClasses)}|{string.Join(',',_bindings.Select(p=>p.Identity))}"));
    }
    internal NativeMobyModelLoaderCertificate Loader { get; }
    internal NativeAddressSpace Final { get; }
    internal int GenericCountBefore { get; }
    internal int GenericCountAfter { get; }
    internal int FreeSlotsAfter=>63-GenericCountAfter;
    internal int ModelGrowthBytes { get; }
    internal int FinalModelOffset { get; }
    internal int FinalModelLength { get; }
    internal bool RuntimeMemoryCertified=>false;
    internal bool NativeImportApproved=>false;
    internal string Fingerprint { get; }
    internal IReadOnlyList<NativeMobyModelRegistrationAppend> Packages { get; }
    internal byte[] CopyHeader()=>_header.ToArray();
    internal static NativeMobyModelRegistrationPlan Build(NativeMobyModelRegistrationSource source,NativeMobyModelLoaderCertificate loader,
        NativeAddressSpace final,IEnumerable<NativeMobyModelPackageBinding> requests,Func<NativeByteExtent,byte[]> read)=>new(source,loader,final,requests,read);
    internal void RequireFinal(ReadOnlySpan<byte> finalData,NativeMobyDependencyClosurePlan closure)
    {
        if(finalData.Length!=Final.ByteLength||!finalData[..0x800].SequenceEqual(_header))
            throw new InvalidDataException("Final registration header/owner changed after planning.");
        if(!finalData.Slice(FinalModelOffset,_source.ModelLength).SequenceEqual(_source.CopyModels()))
            throw new InvalidDataException("Model append altered an existing registered/unplaced package or player stream.");
        foreach(var p in _bindings)
        {
            p.RequireClosure(closure);
            if(!finalData.Slice((int)p.Destination.Offset,(int)p.Destination.ByteLength).SequenceEqual(p.CopyPayload()))
                throw new InvalidDataException("Final registered package differs from its closed payload.");
        }
        int end=checked((int)_bindings.Max(p=>p.Destination.EndExclusive));
        if(finalData.Slice(end,FinalModelOffset+FinalModelLength-end).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("New model alignment padding is not empty owned allocation.");
    }
    private static uint Word(ReadOnlySpan<byte>b,int o)=>NativeMobyModelLoaderCertificate.Word(b,o);
    private static void Put(Span<byte>b,int o,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b[o..],v);
    private static string Hash(ReadOnlySpan<byte>b)=>NativeMobyModelLoaderCertificate.Hash(b);
}
