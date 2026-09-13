using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeFreshMobyTextureConsumer(NativeByteExtent Descriptor,int TileIndex,
    NativeMobyTextureBytePatch Rewrite);

/// <summary>
/// Closed resource transformation consumed by the family Body composer. It owns
/// a complete registered Original AE package, its sound retargeting and all four
/// texture footprints. No caller bytes, behavior/instance policy or output path.
/// </summary>
internal sealed class NativeFreshMobyChestModelMaterialization
{
    private readonly NativeFreshMobySoundResidencyBinding _sound;
    private readonly NativeId65FreshWizardResearchFallbackResourceSource _fallback;
    private readonly NativeMobyTextureBytePatch[] _descriptors;
    private readonly int[] _descriptorTiles;
    private readonly NativeMobyDependencyRead[] _textureReads;
    private readonly byte[] _mask;

    internal NativeFreshMobyChestModelMaterialization(NativeFreshMobyFamilyCompositionPlan ancestor,
        NativeFreshMobySoundResidencyBinding sound,NativeId65FreshWizardResearchFallbackResourceSource fallback)
    {
        ArgumentNullException.ThrowIfNull(ancestor);ArgumentNullException.ThrowIfNull(sound);ArgumentNullException.ThrowIfNull(fallback);
        if(ancestor.HasUnplacedModelResources)
            throw new InvalidDataException("Unplaced resource composition cannot be repeated as another family import.");
        Ancestor=ancestor;_sound=sound;_fallback=fallback;
        sound.RequireAncestry(sound.SourceAncestor,ancestor);
        _mask=NativeFreshWizardGpuOwnership.Capture(fallback,ancestor);
        var source=sound.SourceAncestor;
        byte[] original=source.CopyPackage(),package=sound.CopySoundRetargetedSourcePackage();
        var model=NativeMobyModelTextureLayout.Decode(original);
        var groups=model.References.GroupBy(r=>(r.Palette,r.Page)).OrderBy(g=>g.Key.Palette).ThenBy(g=>g.Key.Page).ToArray();
        if(model.References.Count!=146||groups.Length!=4)
            throw new InvalidDataException("Chest resource transformation requires its complete146-reference/four-tile source grammar.");
        var requests=groups.Select((group,i)=>
        {
            var uv=group.SelectMany(r=>{var b=r.CopyBytes();return new[]{0,4,8,10}.Select(p=>(U:(int)b[p],V:(int)b[p+1]));}).ToArray();
            return new NativeMobyTextureTileRequest("chest.tile."+i,group.Key.Palette,group.Key.Page,
                uv.Min(p=>p.U),uv.Min(p=>p.V),uv.Max(p=>p.U),uv.Max(p=>p.V));
        }).ToArray();
        if(requests.Any(r=>r.MaxU-r.MinU!=31||r.MaxV-r.MinV!=31))
            throw new InvalidDataException("Chest source texture footprint is not four complete32-square tiles.");
        byte[] current=ancestor.CopyLevelData();var layout=new NativeLevelSoundLayout(current);UploadOffset=layout.VramOffset;
        var ranges=new List<NativeTexturePageOwnedRange>();
        for(int i=0;i<_mask.Length;)
        {
            if(_mask[i]==0){i++;continue;}int start=i;while(i<_mask.Length&&_mask[i]!=0)i++;
            ranges.Add(new(start,i-start,"actual-retained-fresh-gpu-owner"));
        }
        var sourceDomain=source.PackageRead.Extent.Space;
        Texture=NativeMobyTextureMaterialization.Compile(source.ReadSource(new(sourceDomain,0x800,0x80000)),
            current.AsSpan(UploadOffset,0x80000),ranges,requests);
        var sourceReads=source.ResourceInputs.Where(p=>p.Key.StartsWith("texture.",StringComparison.Ordinal))
            .SelectMany(p=>p.Value).ToArray();
        List<NativeMobyDependencyRead> reads=[];List<NativeMobyTextureBytePatch> descriptors=[];List<int> tiles=[];
        for(int i=0;i<groups.Length;i++)
        {
            var allocation=Texture.Allocations[i];var target=Texture.Locations[i];
            for(int row=0;row<=32;row++)
            {
                long at=0x800L+(row==32?allocation.SourcePaletteOffset:allocation.SourcePixelOffset+row*1024);
                int length=row==32?32:16;
                var read=sourceReads.SingleOrDefault(r=>ReferenceEquals(r.Extent.Space,sourceDomain)&&r.Extent.Offset==at&&r.Extent.ByteLength==length)
                    ??throw new InvalidDataException("Chest texture installation has no exact retained Original footprint owner.");
                reads.Add(read);
            }
            foreach(var reference in groups[i])
            {
                var after=NativeMobyModelTextureLayout.Rebase(reference,target.U-requests[i].MinU,target.V-requests[i].MinV,
                    target.Palette,(ushort)((reference.Page&~31)|target.Page));
                after.CopyTo(package,reference.PackageOffset);
                descriptors.Add(new("chest.model",reference.PackageOffset,reference.CopyBytes(),after));tiles.Add(i);
            }
        }
        _textureReads=reads.ToArray();_descriptors=descriptors.ToArray();_descriptorTiles=tiles.ToArray();
        if(Texture.Patches.Count!=132||Texture.Patches.Sum(p=>p.ByteLength)!=2176||_textureReads.Length!=132)
            throw new InvalidDataException("Chest texture materialization lost its complete row/CLUT accounting.");
        for(int i=0;i<_textureReads.Length;i++)
            if(!source.ReadSource(_textureReads[i].Extent).AsSpan().SequenceEqual(Texture.Patches[i].CopyAfter()))
                throw new InvalidDataException("Chest texture patch differs from its complete actual Original read.");
        // Reverse texture rewrites, then the existing complete sound inverse.
        byte[] inverse=package.ToArray();
        foreach(var field in _descriptors)
        {
            if(!package.AsSpan(field.Offset,12).SequenceEqual(field.CopyAfter())||!original.AsSpan(field.Offset,12).SequenceEqual(field.CopyBefore()))
                throw new InvalidDataException("Chest texture descriptor source/afterimage changed.");
            field.CopyBefore().CopyTo(inverse,field.Offset);
        }
        if(!sound.RevertExactSoundRetargetedPackage(inverse).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("Chest model transformation failed its complete texture/sound inverse.");
        _=NativeMobyModelTextureLayout.Decode(package);
        ModelContribution=new(0xAE,"chest.model",package);
        ancestor.BaseBodyAncestor.HostAncestor.SceneAncestor.FamilyAncestor.RequireNonSharedModelClass(0xAE);
    }

    internal NativeFreshMobyFamilyCompositionPlan Ancestor{get;}
    internal NativeLockedChestFamilySource Source=>_sound.SourceAncestor;
    internal NativeModelTailPackage ModelContribution{get;}
    internal NativeMobyTextureMaterialization Texture{get;}
    internal int UploadOffset{get;}
    internal void RequireRetainedOwners()
    {
        _sound.RequireAncestry(Source,Ancestor);
        if(!NativeFreshWizardGpuOwnership.Capture(_fallback,Ancestor).AsSpan().SequenceEqual(_mask))
            throw new InvalidDataException("Chest resources lost their complete actual parent GPU ownership.");
    }

    internal NativeMobyDependencyClosurePlan BindFinal(NativeAddressSpace final,NativeModelTailBatchEntry package,
        ReadOnlySpan<byte> completeBody,out IReadOnlyList<NativeFreshMobyTextureConsumer> consumers)
    {
        RequireRetainedOwners();
        if(!ReferenceEquals(package.Input,ModelContribution)||final.Revision!=NativeAddressRevision.Final||
            final.Kind!=NativeAddressKind.LevelData||final.ByteLength!=completeBody.Length)
            throw new InvalidDataException("Chest resource binding requires the actual installed package and complete Final Body.");
        List<NativeMobyDependencyNode> nodes=[];
        for(int i=0;i<Texture.Patches.Count;i++)
        {
            var read=_textureReads[i];var patch=Texture.Patches[i];string id="chest.texture."+i;
            nodes.Add(new(id,NativeMobyDependencyKind.Texture,NativeMobyDependencyResolution.ImportFromSource,
                NativeMobyDependencySharing.Immutable,read.ExpectedSha256,[],[read],
                context=>new(context.Read(read.Id),[new("gpu-row-or-clut",new(final,UploadOffset+patch.Offset,patch.ByteLength))],[])));
        }
        var packageRead=Source.PackageRead;
        nodes.Add(new("chest.model",NativeMobyDependencyKind.ActorPackage,NativeMobyDependencyResolution.ImportFromSource,
            NativeMobyDependencySharing.Immutable,packageRead.ExpectedSha256,nodes.Select(n=>n.Id).ToArray(),[packageRead],
            context=>
            {
                if(!context.Read(packageRead.Id).AsSpan().SequenceEqual(Source.CopyPackage()))
                    throw new InvalidDataException("Chest model materializer lost its complete Original source package.");
                return new(ModelContribution.CopyBytes(),[new("registered-chest-package",new(final,package.PackageOffset,package.ByteLength))],[]);
            }));
        var graph=NativeMobyDependencyClosureCompiler.Compile(nodes,["chest.model"],Source.ReadSource);
        foreach(var install in graph.Installs)
            if(!ReferenceEquals(install.Destination.Space,final)||!completeBody.Slice((int)install.Destination.Offset,
                (int)install.Destination.ByteLength).SequenceEqual(graph.CopyPayload(install.NodeId)))
                throw new InvalidDataException("Chest resource graph differs from its actual complete Final model/GPU afterimage.");
        // UV/TPAGE/CLUT fields use the renderer codec, not linear address-pointer
        // arithmetic. Bind every complete descriptor to the actual Final model.
        consumers=Array.AsReadOnly(_descriptors.Select((d,i)=>new NativeFreshMobyTextureConsumer(
            new(final,package.PackageOffset+d.Offset,12),_descriptorTiles[i],d)).ToArray());
        foreach(var consumer in consumers)
            if(!completeBody.Slice((int)consumer.Descriptor.Offset,12).SequenceEqual(consumer.Rewrite.CopyAfter()))
                throw new InvalidDataException("A chest renderer consumer is absent from the actual Final package.");
        return graph;
    }
}
