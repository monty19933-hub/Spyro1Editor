using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Reconstructs existing live GPU ownership from actual retained Fresh producers.
/// The returned mask is a detached snapshot, not a native import, GPU lifetime,
/// runtime-fit or publication certificate. Generated Fresh bytes are never Original.
/// </summary>
internal static class NativeFreshWizardGpuOwnership
{
    private const int GpuLength=0x80000,UploadLength=0xE5800;
    private const int ResidueStart=0x65800,ResidueEnd=0x7F000;

    internal static byte[] Capture(NativeId65FreshWizardResearchFallbackResourceSource source,
        NativeFreshMobyFamilyCompositionPlan composition)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(composition);
        var body=composition.BaseBodyAncestor;var fallback=body.HostAncestor.FallbackAncestor;
        RequireSource(source,fallback);
        byte[] current=composition.CopyLevelData();composition.RequireExactLevelData(current);
        var final=composition.FinalBodyDomain;
        if(final.Kind!=NativeAddressKind.LevelData||final.Revision!=NativeAddressRevision.Final||
            final.ByteLength!=current.Length||Hash(current)!=composition.LevelDataSha256||
            !composition.CopyInverseBaseBody().AsSpan().SequenceEqual(body.CopyLevelData()))
            throw new InvalidDataException("GPU ownership requires the complete actual Final composition and inverse Body ancestor.");
        byte[] upload=fallback.CopyUploadPayload(),data=fallback.CopyDataComponents(),models=fallback.CopyModelPayload();
        byte[] occupied=ReproduceFallback(source,fallback,upload,data,models);
        if(body.MixedResourceAncestor is { } mixed)
        {
            body.RequireMixedAncestry(mixed);
            byte[] mixedUpload=mixed.CopyUploadPayload();
            if(mixedUpload.Length!=UploadLength||Hash(mixedUpload)!=mixed.UploadSha256||
                !mixedUpload.AsSpan(0,GpuLength).SequenceEqual(mixed.Allocation.CopyGpuPayload()))
                throw new InvalidDataException("GPU ownership lost the complete actual mixed upload/allocation.");
            var materialOwners=new bool[GpuLength];int materialBytes=0;
            foreach(var region in mixed.Allocation.Allocations)
            {
                if(region.TargetOffset<0||region.RowByteLength is <1 or >1024||region.RowCount is <1 or >512||
                    region.TargetOffset%1024+region.RowByteLength>1024||
                    region.TargetOffset+(long)(region.RowCount-1)*1024+region.RowByteLength>GpuLength)
                    throw new InvalidDataException("GPU ownership mixed material extent escaped its complete GPU owner.");
                byte[] pixels=new byte[region.RowByteLength*region.RowCount];
                for(int row=0;row<region.RowCount;row++)
                {
                    int at=region.TargetOffset+row*1024;
                    if(materialOwners.AsSpan(at,region.RowByteLength).Contains(true)||occupied.AsSpan(at,region.RowByteLength).ContainsAnyExcept((byte)0))
                        throw new InvalidDataException("GPU ownership mixed material overlaps an existing live owner.");
                    materialOwners.AsSpan(at,region.RowByteLength).Fill(true);materialBytes+=region.RowByteLength;
                    mixedUpload.AsSpan(at,region.RowByteLength).CopyTo(pixels.AsSpan(row*region.RowByteLength));
                }
                if(Hash(pixels)!=region.Sha256)throw new InvalidDataException("GPU ownership complete mixed material payload changed.");
            }
            if(materialBytes!=mixed.Allocation.AllocatedByteCount)
                throw new InvalidDataException("GPU ownership mixed allocation accounting changed.");
            for(int i=0;i<UploadLength;i++)
            {
                bool allocated=i<GpuLength&&mixed.Allocation.IsAllocated(i);
                if(i<GpuLength&&allocated!=materialOwners[i])
                    throw new InvalidDataException("GPU ownership mixed allocation mask changed a complete zero-inclusive material owner.");
                if((i>=GpuLength||occupied[i]!=0||!allocated)&&mixedUpload[i]!=upload[i])
                    throw new InvalidDataException("GPU ownership mixed upload changed a retained owner or complete transfer complement.");
                if(allocated)occupied[i]=1;
            }
            upload=mixedUpload;
        }
        // Decode the actual current body, never a Town destination offset.
        var layout=new NativeLevelSoundLayout(current);
        if(layout.VramLength!=upload.Length||!current.AsSpan(layout.VramOffset,layout.VramLength).SequenceEqual(upload))
            throw new InvalidDataException("GPU ownership current upload differs from its actual fallback or mixed producer.");
        return occupied;
    }

    // Shared by issuance of the real material resource and capture of a later
    // actual composition. Reproduce every fallback upload/data/model byte first.
    internal static byte[] ReproduceFallback(NativeId65FreshWizardResearchFallbackResourceSource source,
        NativeId65FreshWizardResearchFallbackResourcePlan fallback,byte[] upload,byte[] data,byte[] models)
    {
        RequireSource(source,fallback);
        source.Resource.RequireExactOutputs(source.Upload,source.Data,source.Models);
        NativeFreshWizardMaterialResource.RequirePartition(source.Resource.Owners,[source.Upload,source.Data,source.Models]);
        fallback.RequireExactOutputs(upload,data,models);
        NativeFreshWizardMaterialResource.RequirePartition(fallback.Owners,[upload,data,models]);
        if(upload.Length!=UploadLength||!source.TextureSourceSpans.SequenceEqual(fallback.TextureSourceSpans)||
            !source.RetainedTextureAliases.SequenceEqual(fallback.RetainedTextureAliases)||
            source.Mask.Length!=GpuLength||source.Mask.Any(b=>b>3)||!data.AsSpan().SequenceEqual(source.Data))
            throw new InvalidDataException("Mixed allocation lost complete actual fallback/source ownership.");
        // Retained zero pixels remain live. A mutable mask cannot reclassify a
        // complete producer-owned range, even when its payload happens to be zero.
        foreach(var owner in source.Resource.Owners.Where(o=>o.Domain==NativeFreshWizardResourceDomain.Upload&&o.Offset<GpuLength))
        {
            if(owner.ByteLength>GpuLength-owner.Offset)
                throw new InvalidDataException("GPU ownership producer crosses the GPU/bank boundary.");
            byte tag=owner.Provenance switch {"fresh-zero-upload-background"=>0,"retained-fixed-Town-presentation-VRAM"=>1,
                "issued-detached-companion-VRAM"=>2,"source-texture-insertion"=>3,
                _=>throw new InvalidDataException("GPU ownership has an unknown actual producer classification.")};
            if(source.Mask.AsSpan(owner.Offset,owner.ByteLength).ContainsAnyExcept(tag)||
                (tag==0&&source.Upload.AsSpan(owner.Offset,owner.ByteLength).ContainsAnyExcept((byte)0)))
                throw new InvalidDataException("GPU ownership retained source mask differs from its actual producer.");
        }
        byte[] expected=source.Upload.ToArray(),occupied=source.Mask.Select(b=>b==0?(byte)0:(byte)1).ToArray();
        var assigned=new byte[GpuLength];
        foreach(bool alias in new[]{false,true})
        foreach(var span in alias?source.RetainedTextureAliases:source.TextureSourceSpans)
        {
            if(span.Offset<0||span.ByteLength<=0||span.ByteLength>GpuLength-span.Offset||
                span.SourceWadOffset!=NativeId65FreshWizardResearchFallbackResourceSource.TownWad+0x800+span.Offset||
                assigned.AsSpan(span.Offset,span.ByteLength).ContainsAnyExcept((byte)0)||
                Hash(source.Town.AsSpan(0x800+span.Offset,span.ByteLength))!=span.Sha256)
                throw new InvalidDataException("Mixed fallback source span is not its complete original byte owner.");
            for(int i=span.Offset;i<span.Offset+span.ByteLength;i++)
            {
                NativeId65FreshWizardResearchFallbackResourceSource.RequireTextureOwner(source.Mask[i],source.Upload[i],source.Town[0x800+i],alias);
                expected[i]=source.Town[0x800+i];occupied[i]=1;assigned[i]=1;
            }
        }
        byte[] expectedModels=new byte[0x19000];source.Models.CopyTo(expectedModels,0);
        int cursor=source.Models.Length;
        foreach(var spec in NativeId65FreshWizardResearchFallbackResourceSource.PackageSpecs)
        {source.Packages[spec.Class].CopyTo(expectedModels,cursor);cursor+=spec.Length;}
        if(cursor!=0x18AA8||!upload.AsSpan().SequenceEqual(expected)||!models.AsSpan().SequenceEqual(expectedModels))
            throw new InvalidDataException("Mixed capture changed the full actual fallback/upload/bank/model/padding complement.");
        // Same-upload CPU/DMA residue includes live zero and final DMA-only16 bytes.
        occupied.AsSpan(ResidueStart,ResidueEnd-ResidueStart).Fill(1);
        return occupied;
    }
    private static void RequireSource(NativeId65FreshWizardResearchFallbackResourceSource source,
        NativeId65FreshWizardResearchFallbackResourcePlan fallback)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(fallback);
        if(!ReferenceEquals(source.Resource,fallback.ResourceAncestor))
            throw new InvalidDataException("GPU ownership requires the actual fallback resource ancestor, not an equal reissued source.");
        fallback.RequireAncestry(source.Resource);
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
