using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeFreshMobyTextureConsumer(NativeByteExtent Descriptor,int TileIndex,
    NativeMobyTextureBytePatch Rewrite);

/// <summary>
/// Closed resource transformation consumed by the family Body composer. It owns
/// complete registered Original AE/debris packages, chest sound retargeting and
/// their five texture footprints. No behavior/instance policy or output path.
/// </summary>
internal sealed class NativeFreshMobyChestModelMaterialization
{
    private readonly NativeFreshMobySoundResidencyBinding _sound;
    private readonly NativeId65FreshWizardResearchFallbackResourceSource _fallback;
    private readonly NativeMobyTextureBytePatch[] _descriptors;
    private readonly int[] _descriptorTiles;
    private readonly int[] _descriptorModels;
    private readonly NativeMobyDependencyRead[] _textureReads;
    private readonly NativeMobyDependencyRead[] _modelReads;
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
        if(Texture.Patches.Count!=132||Texture.Patches.Sum(p=>p.ByteLength)!=2176||reads.Count!=132)
            throw new InvalidDataException("Chest texture materialization lost its complete row/CLUT accounting.");
        // Reverse texture rewrites, then the existing complete sound inverse.
        byte[] inverse=package.ToArray();
        foreach(var field in descriptors)
        {
            if(!package.AsSpan(field.Offset,12).SequenceEqual(field.CopyAfter())||!original.AsSpan(field.Offset,12).SequenceEqual(field.CopyBefore()))
                throw new InvalidDataException("Chest texture descriptor source/afterimage changed.");
            field.CopyBefore().CopyTo(inverse,field.Offset);
        }
        if(!sound.RevertExactSoundRetargetedPackage(inverse).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("Chest model transformation failed its complete texture/sound inverse.");
        _=NativeMobyModelTextureLayout.Decode(package);
        List<NativeModelTailPackage> contributions=[new(0xAE,"chest.model",package)];
        List<NativeMobyDependencyRead> modelReads=[source.PackageRead];
        List<int> descriptorModels=Enumerable.Repeat(0,descriptors.Count).ToList();

        // Town already owns the exact native debris packages. Its old recipe
        // proves their non-texture equivalence to Peace; decode the actual
        // renderer fields here instead of treating the old target UVs as free.
        var debrisSpecs=new[]{(Class:(ushort)0x135,At:0x1CC804,Length:0x224,Faces:20),
            (Class:(ushort)0x136,At:0x1CCA28,Length:0x1D4,Faces:16),
            (Class:(ushort)0x137,At:0x1CCBFC,Length:0x2D8,Faces:28)};
        var debrisReads=debrisSpecs.Select(s=>source.ResourceInputs["effects.native-debris."+s.Class].Single()).ToArray();
        var townDomain=debrisReads[0].Extent.Space;
        byte[] town=source.ReadSource(new(townDomain,0,townDomain.ByteLength));
        var registrations=NativeMobyRegistrationTableLayout.Decode(town.AsSpan(0,0x800),
            NativeLevelSoundLayout.Integer(town,0x10),NativeLevelSoundLayout.Integer(town,0x14)).Registrations;
        byte[][] debrisOriginals=debrisReads.Select(r=>source.ReadSource(r.Extent)).ToArray();
        NativeMobyModelTextureLayout[] debrisLayouts=debrisOriginals.Select(b=>NativeMobyModelTextureLayout.Decode(b)).ToArray();
        for(int i=0;i<debrisSpecs.Length;i++)
        {
            var spec=debrisSpecs[i];var read=debrisReads[i];var grammar=debrisLayouts[i];
            if(!ReferenceEquals(read.Extent.Space,townDomain)||read.Extent.Offset!=spec.At||read.Extent.ByteLength!=spec.Length||
                registrations.Single(r=>r.ActorId==spec.Class).RootDataOffset!=spec.At||
                grammar.References.Count!=spec.Faces||!grammar.References.Select(r=>r.PackageOffset)
                    .SequenceEqual(Enumerable.Range(0,spec.Faces).Select(f=>0x74+f*0x14)))
                throw new InvalidDataException("Debris materialization lost its exact Original registration/package/renderer grammar.");
        }
        var debrisGroups=debrisLayouts.SelectMany(l=>l.References).GroupBy(r=>(r.Palette,r.Page)).ToArray();
        if(debrisGroups.Length!=1)throw new InvalidDataException("Native debris requires its one shared source tile/CLUT.");
        var debrisGroup=debrisGroups[0];
        var debrisUv=debrisGroup.SelectMany(r=>{var b=r.CopyBytes();return new[]{0,4,8,10}.Select(p=>(U:(int)b[p],V:(int)b[p+1]));}).ToArray();
        var debrisRequest=new NativeMobyTextureTileRequest("chest.debris.tile",debrisGroup.Key.Palette,debrisGroup.Key.Page,
            debrisUv.Min(p=>p.U),debrisUv.Min(p=>p.V),debrisUv.Max(p=>p.U),debrisUv.Max(p=>p.V));
        if(debrisRequest.MaxU-debrisRequest.MinU!=31||debrisRequest.MaxV-debrisRequest.MinV!=31)
            throw new InvalidDataException("Native debris requires its complete32-square source footprint.");
        // Every first-pass allocation remains occupied, including zero pixels
        // and palette entries. Both passes remain private until Body composition.
        ranges.AddRange(Texture.Patches.Select(p=>new NativeTexturePageOwnedRange(p.Offset,p.ByteLength,"actual-chest-gpu-allocation")));
        DebrisTexture=NativeMobyTextureMaterialization.Compile(town.AsSpan(0x800,0x80000),Texture.CopyUpload(),ranges,[debrisRequest]);
        var debrisAllocation=DebrisTexture.Allocations.Single();var debrisTarget=DebrisTexture.Locations.Single();
        if(debrisAllocation.SourcePixelOffset!=0x58A70-0x800||debrisAllocation.SourcePaletteOffset!=0x78AA0-0x800||
            DebrisTexture.Patches.Count!=33||DebrisTexture.Patches.Sum(p=>p.ByteLength)!=544)
            throw new InvalidDataException("Debris texture allocation disagrees with its complete retained Original footprint.");
        for(int row=0;row<=32;row++)
        {
            var read=source.ResourceInputs[row==32?"effects.native-debris-clut":"effects.native-debris-pixels."+row].Single();
            if(!ReferenceEquals(read.Extent.Space,townDomain)||read.Extent.Offset!=(row==32?0x78AA0:0x58A70+row*0x400)||
                read.Extent.ByteLength!=(row==32?32:16))
                throw new InvalidDataException("Debris pixel/CLUT patch has no exact retained Original source owner.");
            reads.Add(read);
        }
        for(int i=0;i<debrisSpecs.Length;i++)
        {
            byte[] transformed=debrisOriginals[i].ToArray();string id="effects.native-debris."+debrisSpecs[i].Class;
            foreach(var reference in debrisLayouts[i].References)
            {
                var after=NativeMobyModelTextureLayout.Rebase(reference,debrisTarget.U-debrisRequest.MinU,debrisTarget.V-debrisRequest.MinV,
                    debrisTarget.Palette,(ushort)((reference.Page&~31)|debrisTarget.Page));
                after.CopyTo(transformed,reference.PackageOffset);
                descriptors.Add(new(id,reference.PackageOffset,reference.CopyBytes(),after));tiles.Add(4);descriptorModels.Add(i+1);
            }
            byte[] recovered=transformed.ToArray();
            foreach(var reference in debrisLayouts[i].References)reference.CopyBytes().CopyTo(recovered,reference.PackageOffset);
            if(!recovered.AsSpan().SequenceEqual(debrisOriginals[i])||
                NativeMobyModelTextureLayout.Decode(transformed).References.Count!=debrisSpecs[i].Faces)
                throw new InvalidDataException("Debris texture transformation lost a renderer field or complete model complement.");
            contributions.Add(new(debrisSpecs[i].Class,id,transformed));modelReads.Add(debrisReads[i]);
        }
        ModelContributions=contributions.AsReadOnly();_modelReads=modelReads.ToArray();
        TexturePatches=Array.AsReadOnly(Texture.Patches.Concat(DebrisTexture.Patches).ToArray());
        _textureReads=reads.ToArray();_descriptors=descriptors.ToArray();_descriptorTiles=tiles.ToArray();_descriptorModels=descriptorModels.ToArray();
        if(ModelContributions.Sum(p=>p.ByteLength)!=0x16D8||_descriptors.Length!=210||_textureReads.Length!=165||TexturePatches.Count!=165)
            throw new InvalidDataException("Chest/debris resources lost their complete four-package/five-tile accounting.");
        for(int i=0;i<_textureReads.Length;i++)
            if(!source.ReadSource(_textureReads[i].Extent).AsSpan().SequenceEqual(TexturePatches[i].CopyAfter()))
                throw new InvalidDataException("Chest/debris texture patch differs from its complete actual Original read.");
        foreach(var contribution in ModelContributions)
            ancestor.BaseBodyAncestor.HostAncestor.SceneAncestor.FamilyAncestor.RequireNonSharedModelClass(contribution.ActorId);
    }

    internal NativeFreshMobyFamilyCompositionPlan Ancestor{get;}
    internal NativeLockedChestFamilySource Source=>_sound.SourceAncestor;
    internal NativeModelTailPackage ModelContribution=>ModelContributions[0];
    internal IReadOnlyList<NativeModelTailPackage> ModelContributions{get;}
    internal NativeMobyTextureMaterialization Texture{get;}
    internal NativeMobyTextureMaterialization DebrisTexture{get;}
    internal IReadOnlyList<NativeMobyTextureBytePatch> TexturePatches{get;}
    internal byte[] CopyUpload()=>DebrisTexture.CopyUpload();
    internal int UploadOffset{get;}
    internal void RequireRetainedOwners()
    {
        _sound.RequireAncestry(Source,Ancestor);
        if(!NativeFreshWizardGpuOwnership.Capture(_fallback,Ancestor).AsSpan().SequenceEqual(_mask))
            throw new InvalidDataException("Chest resources lost their complete actual parent GPU ownership.");
    }

    internal NativeMobyDependencyClosurePlan BindFinal(NativeAddressSpace final,IReadOnlyList<NativeModelTailBatchEntry> packages,
        ReadOnlySpan<byte> completeBody,out IReadOnlyList<NativeFreshMobyTextureConsumer> consumers)
    {
        RequireRetainedOwners();
        if(packages.Count!=ModelContributions.Count||packages.Where((p,i)=>!ReferenceEquals(p.Input,ModelContributions[i])).Any()||
            final.Revision!=NativeAddressRevision.Final||
            final.Kind!=NativeAddressKind.LevelData||final.ByteLength!=completeBody.Length)
            throw new InvalidDataException("Chest resource binding requires the actual installed package and complete Final Body.");
        List<NativeMobyDependencyNode> nodes=[];
        for(int i=0;i<TexturePatches.Count;i++)
        {
            var read=_textureReads[i];var patch=TexturePatches[i];string id="chest.texture."+i;
            nodes.Add(new(id,NativeMobyDependencyKind.Texture,NativeMobyDependencyResolution.ImportFromSource,
                NativeMobyDependencySharing.Immutable,read.ExpectedSha256,[],[read],
                context=>new(context.Read(read.Id),[new("gpu-row-or-clut",new(final,UploadOffset+patch.Offset,patch.ByteLength))],[])));
        }
        for(int i=0;i<packages.Count;i++)
        {
            var package=packages[i];var contribution=ModelContributions[i];var packageRead=_modelReads[i];
            string[] dependencies=Enumerable.Range(i==0?0:132,i==0?132:33).Select(n=>"chest.texture."+n).ToArray();
            nodes.Add(new(contribution.Id,NativeMobyDependencyKind.ActorPackage,NativeMobyDependencyResolution.ImportFromSource,
                NativeMobyDependencySharing.Immutable,packageRead.ExpectedSha256,dependencies,[packageRead],
                context=>
                {
                    if(!context.Read(packageRead.Id).AsSpan().SequenceEqual(Source.ReadSource(packageRead.Extent)))
                        throw new InvalidDataException("Chest/debris materializer lost its complete Original source package.");
                    return new(contribution.CopyBytes(),[new("registered-package",new(final,package.PackageOffset,package.ByteLength))],[]);
                }));
        }
        var graph=NativeMobyDependencyClosureCompiler.Compile(nodes,ModelContributions.Select(p=>p.Id).ToArray(),Source.ReadSource);
        foreach(var install in graph.Installs)
            if(!ReferenceEquals(install.Destination.Space,final)||!completeBody.Slice((int)install.Destination.Offset,
                (int)install.Destination.ByteLength).SequenceEqual(graph.CopyPayload(install.NodeId)))
                throw new InvalidDataException("Chest resource graph differs from its actual complete Final model/GPU afterimage.");
        // UV/TPAGE/CLUT fields use the renderer codec, not linear address-pointer
        // arithmetic. Bind every complete descriptor to the actual Final model.
        consumers=Array.AsReadOnly(_descriptors.Select((d,i)=>new NativeFreshMobyTextureConsumer(
            new(final,packages[_descriptorModels[i]].PackageOffset+d.Offset,12),_descriptorTiles[i],d)).ToArray());
        foreach(var consumer in consumers)
            if(!completeBody.Slice((int)consumer.Descriptor.Offset,12).SequenceEqual(consumer.Rewrite.CopyAfter()))
                throw new InvalidDataException("A chest renderer consumer is absent from the actual Final package.");
        return graph;
    }
}
