using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// The extracted family's original Town key-particle support bytes already
/// resident in one actual Fresh composition. No package, callback, renderer,
/// frame-lifetime, GPU execution, Peace equivalence or family admission is issued.
/// </summary>
internal sealed class NativeFreshMobyParticleResidencyBinding
{
    private const string Profile="native-fresh-locked-chest-key-particle-resident-bytes-v1";
    private const int KeyDescriptorId=0x0C;
    private readonly string _compositionPlanSha256,_levelDataSha256;

    private NativeFreshMobyParticleResidencyBinding(NativeLockedChestFamilySource source,
        NativeFreshMobyFamilyCompositionPlan composition)
    {
        SourceAncestor=source;CompositionAncestor=composition;
        var inspected=Inspect(source,composition);
        Graph=inspected.Graph;Descriptor=inspected.Descriptor;
        PixelRows=Array.AsReadOnly(inspected.PixelRows);Clut=inspected.Clut;
        _compositionPlanSha256=composition.PlanSha256;_levelDataSha256=composition.LevelDataSha256;
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            Profile,CompositionPlanSha256=_compositionPlanSha256,LevelDataSha256=_levelDataSha256,
            Graph.Fingerprint,DescriptorId,Descriptor,PixelRows,Clut}));
    }

    internal NativeLockedChestFamilySource SourceAncestor{get;}
    internal NativeFreshMobyFamilyCompositionPlan CompositionAncestor{get;}
    internal NativeMobyDependencyClosurePlan Graph{get;}
    internal int DescriptorId=>KeyDescriptorId;
    internal NativeByteExtent Descriptor{get;}
    internal IReadOnlyList<NativeByteExtent> PixelRows{get;}
    internal NativeByteExtent Clut{get;}
    internal string PlanSha256{get;}

    internal static NativeFreshMobyParticleResidencyBinding BindLockedChestKey(NativeLockedChestFamilySource source,
        NativeFreshMobyFamilyCompositionPlan composition)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(composition);
        return new(source,composition);
    }

    internal void RequireAncestry(NativeLockedChestFamilySource source,NativeFreshMobyFamilyCompositionPlan composition)
    {
        if(!ReferenceEquals(source,SourceAncestor)||!ReferenceEquals(composition,CompositionAncestor))
            throw new InvalidDataException("Key-particle residency requires its actual source/composition pair, not equal reissued owners.");
        RequireRetainedOwners();
    }

    internal void RequireRetainedOwners()
    {
        if(CompositionAncestor.PlanSha256!=_compositionPlanSha256||CompositionAncestor.LevelDataSha256!=_levelDataSha256)
            throw new InvalidDataException("Key-particle residency lost its complete retained composition identity.");
        var actual=Inspect(SourceAncestor,CompositionAncestor);
        if(actual.Graph.Fingerprint!=Graph.Fingerprint||actual.Descriptor!=Descriptor||
            !actual.PixelRows.SequenceEqual(PixelRows)||actual.Clut!=Clut)
            throw new InvalidDataException("Retained key-particle descriptor, complete footprint or preservation binding changed.");
        RequireFinalPreservations(Graph,SourceAncestor,CompositionAncestor.FinalBodyDomain,CompositionAncestor.CopyLevelData());
    }

    internal void RequireExactLevelData(ReadOnlySpan<byte> bytes)
    {
        // Existing complete-body ownership also protects Wizard/material bytes,
        // sound, transfer residue and every complement outside these 34 owners.
        CompositionAncestor.RequireExactLevelData(bytes);
        RequireRetainedOwners();
        RequireFinalPreservations(Graph,SourceAncestor,CompositionAncestor.FinalBodyDomain,bytes);
    }

    private static (NativeMobyDependencyClosurePlan Graph,NativeByteExtent Descriptor,NativeByteExtent[] PixelRows,
        NativeByteExtent Clut) Inspect(NativeLockedChestFamilySource source,NativeFreshMobyFamilyCompositionPlan composition)
    {
        source.RequireRetainedOwners();
        byte[] body=composition.CopyLevelData();composition.RequireExactLevelData(body);
        var finalDomain=composition.FinalBodyDomain;
        if(finalDomain.Kind!=NativeAddressKind.LevelData||finalDomain.Revision!=NativeAddressRevision.Final||finalDomain.ByteLength!=body.Length)
            throw new InvalidDataException("Key-particle residency requires the actual complete FinalBody address owner.");

        var descriptorRead=source.ResourceInputs["key.particle-descriptor"].Single();
        var originalDomain=descriptorRead.Extent.Space;
        byte[] original=source.ReadSource(new(originalDomain,0,originalDomain.ByteLength));
        var originalLayout=new NativeLevelSoundLayout(original);
        var finalLayout=new NativeLevelSoundLayout(body);
        NativeByteExtent originalDescriptor=DescriptorExtent(original,originalLayout,originalDomain);
        NativeByteExtent descriptor=DescriptorExtent(body,finalLayout,finalDomain);
        if(descriptorRead.Extent!=originalDescriptor)
            throw new InvalidDataException("Extracted key-particle support is not its exact original indexed descriptor record.");
        byte[] record=source.ReadSource(descriptorRead.Extent);
        ReadOnlySpan<byte> finalRecord=body.AsSpan(checked((int)descriptor.Offset),checked((int)descriptor.ByteLength));
        if(!record.AsSpan().SequenceEqual(finalRecord))
            throw new InvalidDataException("The actual Fresh key-particle descriptor differs from its complete Town support record.");
        var sourceFootprint=Footprint(record);var finalFootprint=Footprint(finalRecord);
        if(sourceFootprint.PixelRows.Count!=32||sourceFootprint.PixelRows.Any(r=>r.ByteLength!=16)||sourceFootprint.Palette.ByteLength!=32||
            !sourceFootprint.PixelRows.SequenceEqual(finalFootprint.PixelRows)||sourceFootprint.Palette!=finalFootprint.Palette)
            throw new InvalidDataException("Key-particle support requires its complete unchanged 32-row indexed footprint and CLUT.");

        List<NativeMobyDependencyNode> nodes=[];
        Resident("key.particle-descriptor",descriptorRead,originalDescriptor,descriptor);
        var pixelRows=new NativeByteExtent[finalFootprint.PixelRows.Count];
        for(int row=0;row<pixelRows.Length;row++)
        {
            var sourceRow=sourceFootprint.PixelRows[row];var finalRow=finalFootprint.PixelRows[row];
            var read=source.ResourceInputs["key.particle-pixels."+row].Single();
            NativeByteExtent sourceExtent=new(originalDomain,checked(originalLayout.VramOffset+sourceRow.VramRelativeOffset),sourceRow.ByteLength);
            pixelRows[row]=new(finalDomain,checked(finalLayout.VramOffset+finalRow.VramRelativeOffset),finalRow.ByteLength);
            Resident("key.particle-pixels."+row,read,sourceExtent,pixelRows[row]);
        }
        var clutRead=source.ResourceInputs["key.particle-clut"].Single();
        NativeByteExtent sourceClut=new(originalDomain,checked(originalLayout.VramOffset+sourceFootprint.Palette.VramRelativeOffset),sourceFootprint.Palette.ByteLength);
        NativeByteExtent clut=new(finalDomain,checked(finalLayout.VramOffset+finalFootprint.Palette.VramRelativeOffset),finalFootprint.Palette.ByteLength);
        Resident("key.particle-clut",clutRead,sourceClut,clut);

        const string root="key.particle-residency";
        nodes.Add(new(root,NativeMobyDependencyKind.Bundle,NativeMobyDependencyResolution.Aggregate,
            NativeMobyDependencySharing.Immutable,descriptorRead.ExpectedSha256,nodes.Select(n=>n.Id).ToArray(),[]));
        var graph=NativeMobyDependencyClosureCompiler.Compile(nodes,[root],source.ReadSource);
        RequireFinalPreservations(graph,source,finalDomain,body);
        return(graph,descriptor,pixelRows,clut);

        void Resident(string id,NativeMobyDependencyRead read,NativeByteExtent expectedSource,NativeByteExtent destination)
        {
            if(read.Extent!=expectedSource||read.Extent.Space.Revision!=NativeAddressRevision.Original||
                read.Extent.ByteLength!=destination.ByteLength)
                throw new InvalidDataException("A key-particle support read differs from its complete descriptor-derived original owner.");
            nodes.Add(new(id,NativeMobyDependencyKind.Texture,NativeMobyDependencyResolution.DestinationResident,
                NativeMobyDependencySharing.Immutable,read.ExpectedSha256,[],[read],Preserved:[new(read.Id,0,destination)]));
        }
    }

    private static NativeByteExtent DescriptorExtent(ReadOnlySpan<byte> bytes,NativeLevelSoundLayout layout,NativeAddressSpace domain)
    {
        var table=NativeParticleDescriptorCodec.Decode(bytes.Slice(layout.ParticleOffset,layout.SoundOffset-layout.ParticleOffset));
        int ordinal=table.Ids.ToList().IndexOf(KeyDescriptorId);
        if(ordinal<0)
            throw new InvalidDataException("The particle stream has no owned key support descriptor0C; callback selectors are another namespace.");
        return new(domain,checked(layout.ParticleOffset+8+ordinal*12),12);
    }

    private static NativeId65TextureFootprint Footprint(ReadOnlySpan<byte> record)
    {
        if(NativeParticleDescriptorCodec.RequireRecord(record)!=KeyDescriptorId)
            throw new InvalidDataException("Key-particle support requires the complete signed-ID0C/eight-byte-payload record.");
        // Same frame expansion as NativeId65NormalResourceBundle: two opposite
        // UV corners become the neutral rectangle. This is not frame evolution.
        ReadOnlySpan<byte> frame=record[4..];
        byte[] descriptor=new byte[12];frame.CopyTo(descriptor);
        descriptor[8]=frame[0];descriptor[9]=frame[5];
        descriptor[10]=frame[4];descriptor[11]=frame[1];
        return NativeId65PlayerTextureOwners.DecodeFootprint(descriptor);
    }

    private static void RequireFinalPreservations(NativeMobyDependencyClosurePlan graph,NativeLockedChestFamilySource source,
        NativeAddressSpace finalDomain,ReadOnlySpan<byte> body)
    {
        var preserved=graph.Nodes.SelectMany(n=>n.Preserved).ToArray();
        if(graph.Installs.Count!=0||graph.References.Count!=0||preserved.Length!=34||
            preserved.Sum(p=>p.Final.ByteLength)!=556||body.Length!=finalDomain.ByteLength)
            throw new InvalidDataException("Key-particle residency must preserve exactly 34 owners/556 bytes without installs or references.");
        // Successful graph compilation verifies source promises, not final
        // bytes. Read every actual afterimage, including referenced zero bytes.
        foreach(var node in graph.Nodes)
        foreach(var preservation in node.Preserved)
        {
            var read=node.Reads.Single(r=>r.Id==preservation.ReadId);
            byte[] original=source.ReadSource(read.Extent);
            if(!ReferenceEquals(preservation.Final.Space,finalDomain)||Hash(original)!=read.ExpectedSha256||
                !body.Slice(checked((int)preservation.Final.Offset),checked((int)preservation.Final.ByteLength)).SequenceEqual(
                    original.AsSpan(preservation.ReadOffset,checked((int)preservation.Final.ByteLength))))
                throw new InvalidDataException("Key-particle source promise differs from its actual preserved FinalBody bytes.");
        }
    }

    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
