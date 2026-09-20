using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class FamilyChestResourceCases
{
    internal static void Free(Action<bool,string> check)
    {
        Reject<ArgumentNullException>(()=>NativeFreshMobyFamilyCompositionPlan.ComposeWithLockedChestResources(null!,null!,null!),check);
        Console.WriteLine("NATIVE_FRESH_CHEST_RESOURCE_COMPOSITION_FREE_OK nativeAdmission=false");
    }

    internal static async Task Source(string original,NativeLockedChestFamilySource source,
        NativeFreshMobySoundResidencyBinding sound,NativeFreshMobyFamilyCompositionPlan ancestor,
        NativeFreshMobyFamilyCompositionPlan equalAncestor,Action<bool,string> check)
    {
        CheckRetainedDebrisGrammar(source,check);
        NativeFreshLockedChestResearchHostExtension? sceneHost=null;
        if(ancestor.BaseBodyAncestor.MixedResourceAncestor is not null)
        {
            var current=await NativeId65FreshWizardResearchExecutablePlan.CaptureFamilyComposedAsync(original,ancestor);
            FamilyChestHostCases.Source(current,source,check);
            var extension=NativeFreshLockedChestResearchHostExtension.Compose(current,source);
            sceneHost=extension;
            var equalCurrent=await NativeId65FreshWizardResearchExecutablePlan.CaptureFamilyComposedAsync(original,ancestor);
            check(equalCurrent.ExecutableSha256==current.ExecutableSha256&&!ReferenceEquals(equalCurrent,current),
                "A genuinely equal reissued current executable remains another actual owner.");
            Reject<InvalidDataException>(()=>extension.RequireAncestry(equalCurrent,source),check);
            var oldArm=await NativeId65FreshWizardResearchExecutablePlan.CaptureMaterialAuthoredAsync(original,ancestor.BaseBodyAncestor);
            check(oldArm.ExecutableSha256==current.ExecutableSha256,"Historical material arm genuinely shares executable bytes.");
            Reject<InvalidDataException>(()=>NativeFreshLockedChestResearchHostExtension.Compose(oldArm,source),check);
        }
        byte[] before=ancestor.CopyLevelData(),scene=ancestor.CopyScene();
        check(!ancestor.HasUnplacedModelResources&&ancestor.ResourceAncestor is null&&
            ancestor.ChestModelContribution is null&&ancestor.ChestTextureMaterialization is null&&
            ancestor.ChestModelContributions.Count==0&&ancestor.ChestDebrisTextureMaterialization is null,
            "Existing family compositions retain their original no-unplaced-resource state.");
        // One additional exact capture for the actual fallback ownership mask;
        // the chest source and sound binding reuse the surrounding source pass.
        var fallback=await NativeId65FreshWizardResearchFallbackResourceSource.CaptureAsync(original,
            ancestor.BaseBodyAncestor.HostAncestor.FallbackAncestor.ResourceAncestor,CancellationToken.None);
        var ownership=NativeFreshWizardGpuOwnership.Capture(fallback,ancestor);
        check(ownership.Length==0x80000&&!ownership.AsSpan(0x65800,0x19800).Contains((byte)0),
            "Complete CPU/DMA transfer residue remains protected even where source bytes are zero.");
        int liveZero=Enumerable.Range(0,0x80000).First(i=>fallback.Mask[i]!=0&&fallback.Upload[i]==0);
        byte savedTag=fallback.Mask[liveZero];
        try
        {
            fallback.Mask[liveZero]=0;
            Reject<InvalidDataException>(()=>NativeFreshWizardGpuOwnership.Capture(fallback,ancestor),check);
            Reject<InvalidDataException>(()=>NativeFreshMobyFamilyCompositionPlan.ComposeWithLockedChestResources(ancestor,sound,fallback),check);
        }
        finally{fallback.Mask[liveZero]=savedTag;}
        check(NativeFreshWizardGpuOwnership.Capture(fallback,ancestor).SequenceEqual(ownership),
            "Refused live-zero ownership mutation leaves the exact retry mask intact after fixture restoration.");
        Reject<ArgumentNullException>(()=>NativeFreshMobyFamilyCompositionPlan.ComposeWithLockedChestResources(ancestor,null!,fallback),check);
        Reject<ArgumentNullException>(()=>NativeFreshMobyFamilyCompositionPlan.ComposeWithLockedChestResources(ancestor,sound,null!),check);
        Reject<InvalidDataException>(()=>NativeFreshMobyFamilyCompositionPlan.ComposeWithLockedChestResources(equalAncestor,sound,fallback),check);
        var equalSound=NativeFreshMobySoundResidencyBinding.BindLockedChest(source,equalAncestor);
        Reject<InvalidDataException>(()=>NativeFreshMobyFamilyCompositionPlan.ComposeWithLockedChestResources(ancestor,equalSound,fallback),check);

        var child=NativeFreshMobyFamilyCompositionPlan.ComposeWithLockedChestResources(ancestor,sound,fallback);
        if(sceneHost is not null)FamilyChestSceneCases.Source(child,sceneHost,check);
        byte[] after=child.CopyLevelData();var package=child.ChestModelContribution!;var texture=child.ChestTextureMaterialization!;
        var packages=child.ChestModelContributions;var debrisTexture=child.ChestDebrisTextureMaterialization!;
        check(child.HasUnplacedModelResources&&ReferenceEquals(child.ResourceAncestor,ancestor)&&
            ReferenceEquals(child.BaseBodyAncestor,ancestor.BaseBodyAncestor)&&
            !ReferenceEquals(child.FinalBodyDomain,ancestor.FinalBodyDomain)&&
            child.FinalBodyDomain.Revision==NativeAddressRevision.Final&&child.FinalBodyDomain.ByteLength==after.Length&&
            ReferenceEquals(child.FinalSceneDomain,ancestor.FinalSceneDomain)&&ReferenceEquals(child.Allocation,ancestor.Allocation),
            "Resource composition retains its actual parent, unchanged scene allocation and a new complete Final Body owner.");
        check(child.CopyScene().SequenceEqual(scene)&&child.Placements.SequenceEqual(ancestor.Placements)&&
            child.Layout.PlacedCount==ancestor.Layout.PlacedCount&&child.Layout.DynamicCapacity==ancestor.Layout.DynamicCapacity&&
            !child.CompleteNativeBehaviorProven&&!child.RuntimeVerified&&!child.PublicationApproved,
            "AE/debris resource installation adds no key/chest/reward rows, behavior or export approval.");
        int oldModels=Word(before,0x10),oldLength=Word(before,0x14),oldScene=Word(before,0x18);
        int newModels=Word(after,0x10),newLength=Word(after,0x14),newScene=Word(after,0x18);
        var oldRoots=NativeMobyRegistrationTableLayout.Decode(before.AsSpan(0,0x800),oldModels,oldLength).Registrations;
        var newRoots=NativeMobyRegistrationTableLayout.Decode(after.AsSpan(0,0x800),newModels,newLength).Registrations;
        check(newRoots.Count==oldRoots.Count+4&&newRoots.Skip(oldRoots.Count).Select(r=>r.ActorId)
            .SequenceEqual(new ushort[]{0xAE,0x135,0x136,0x137}),
            "The actual resource Body must install AE and all three native debris packages in one ordered model-tail batch.");
        var chestRoot=newRoots.Single(r=>r.ActorId==0xAE);
        check(package.ActorId==0xAE&&package.ByteLength==0x1008&&Word(package.CopyBytes(),4)==unchecked((int)0xFFFFFF36)&&
            packages.Count==4&&ReferenceEquals(packages[0],package)&&packages.Sum(p=>p.ByteLength)==0x16D8&&
            newRoots.Count==oldRoots.Count+4&&newRoots.Take(oldRoots.Count).SequenceEqual(oldRoots)&&
            chestRoot.Slot==oldRoots.Count&&chestRoot.RootDataOffset==oldModels+oldLength&&
            newModels==oldModels&&newLength==oldLength+0x1800&&newScene==oldScene+0x1800&&after.Length==before.Length+0x1800,
            "AE and all three debris packages share one1800 model allocation growth, retaining all old roots.");
        check(after.AsSpan((int)chestRoot.RootDataOffset,0x1008).SequenceEqual(package.CopyBytes())&&
            !after.AsSpan((int)chestRoot.RootDataOffset+0x16D8,0x128).ContainsAnyExcept((byte)0)&&
            after.AsSpan(newScene,scene.Length).SequenceEqual(scene)&&
            child.ModelRuntimeAddress==ancestor.ModelRuntimeAddress&&child.SceneRuntimeAddress==ancestor.SceneRuntimeAddress+0x1800&&
            child.KnownSceneEndAddress==ancestor.KnownSceneEndAddress+0x1800&&
            child.KnownSceneEndAddress<=ancestor.BaseBodyAncestor.NormalReservedLowerAddress&&newLength+scene.Length<=0x80000,
            "Installed AE bytes, final four-package padding, unchanged scene and existing runtime-reserve bounds remain exact.");
        int packageAt=oldModels+oldLength;
        for(int i=0;i<packages.Count;i++)
        {
            var contribution=packages[i];var root=newRoots[oldRoots.Count+i];
            check(root.ActorId==contribution.ActorId&&root.Slot==oldRoots.Count+i&&root.RootDataOffset==packageAt&&
                after.AsSpan(packageAt,contribution.ByteLength).SequenceEqual(contribution.CopyBytes()),
                "Every actual ordered package is complete at its appended registration; no old model bytes are overwritten.");
            packageAt+=contribution.ByteLength;
        }

        check(texture.Locations.Count==4&&texture.Allocations.Count==4&&texture.Patches.Count==132&&
            texture.Patches.Sum(p=>p.ByteLength)==2176&&texture.Locations.All(l=>l.PixelRowBytes==16&&l.Rows==32),
            "Four complete AE tile/CLUT pairs install132 disjoint source-byte patches totaling2176 bytes.");
        byte[] expectedPackage=sound.CopySoundRetargetedSourcePackage();
        var sourceLayout=NativeMobyModelTextureLayout.Decode(source.CopyPackage());
        var groups=sourceLayout.References.GroupBy(r=>(r.Page,r.Palette)).OrderBy(g=>g.Key.Palette).ThenBy(g=>g.Key.Page).ToArray();
        check(sourceLayout.References.Count==146&&groups.Length==4,"Actual AE shared grammar supplies146 consumers in four ordered source groups.");
        int[] uv=[0,4,8,10];int uploadAt=Word(after,0),oldUploadAt=Word(before,0);
        for(int i=0;i<groups.Length;i++)
        {
            var group=groups[i];var location=texture.Locations[i];var allocation=texture.Allocations[i];
            var coordinates=group.SelectMany(r=>{byte[] b=r.CopyBytes();return uv.Select(at=>(U:(int)b[at],V:(int)b[at+1]));}).ToArray();
            int minU=coordinates.Min(p=>p.U),minV=coordinates.Min(p=>p.V);
            int sourceX=(group.Key.Page&15)*128+minU/2-1024,sourceY=((group.Key.Page&16)!=0?256:0)+minV;
            int sourcePalette=(group.Key.Palette>>6)*1024+(group.Key.Palette&63)*32-1024;
            check(coordinates.Max(p=>p.U)==minU+31&&coordinates.Max(p=>p.V)==minV+31&&
                allocation.SourcePixelOffset==sourceY*1024+sourceX&&allocation.SourcePaletteOffset==sourcePalette&&
                allocation.TargetPixelOffset==location.PixelOffset&&allocation.TargetPaletteOffset==location.PaletteOffset,
                "Each actual source descriptor group maps to its ordered complete allocated tile and CLUT.");
            foreach(var reference in group)
                NativeMobyModelTextureLayout.Rebase(reference,location.U-minU,location.V-minV,location.Palette,
                    (ushort)((reference.Page&~31)|location.Page)).CopyTo(expectedPackage,reference.PackageOffset);
            for(int row=0;row<32;row++)
                check(after.AsSpan(uploadAt+location.PixelOffset+row*1024,16).SequenceEqual(source.ReadSource(
                    new(source.PackageRead.Extent.Space,0x800+(sourceY+row)*1024+sourceX,16))),
                    "Installed AE pixel row equals its complete actual Original source owner.");
            check(after.AsSpan(uploadAt+location.PaletteOffset,32).SequenceEqual(source.ReadSource(
                new(source.PackageRead.Extent.Space,0x800+sourcePalette,32))),
                "Installed AE CLUT equals its complete actual Original source owner.");
        }
        check(package.CopyBytes().SequenceEqual(expectedPackage)&&NativeMobyModelTextureLayout.Decode(package.CopyBytes()).References.Count==146,
            "The installed complete AE package contains exactly shared texture rebasing plus sound47-to54, preserving its whole complement.");
        var modelRoots=packages.ToDictionary(p=>p.Id,p=>newRoots.Single(r=>r.ActorId==p.ActorId));
        check(child.ChestTextureConsumers.Count==210&&child.ChestTextureConsumers.Count(c=>c.TileIndex==4)==64&&
            child.ChestTextureConsumers.All(c=>
            ReferenceEquals(c.Descriptor.Space,child.FinalBodyDomain)&&c.Descriptor.ByteLength==12&&c.TileIndex is >=0 and <5&&
            c.Descriptor.Offset==modelRoots[c.Rewrite.Owner].RootDataOffset+c.Rewrite.Offset&&
            after.AsSpan((int)c.Descriptor.Offset,12).SequenceEqual(c.Rewrite.CopyAfter())),
            "All210 chest/debris renderer consumers bind complete installed fields in the actual new Final model domain.");
        CheckInstalledDebris(source,child,after,uploadAt,check);
        var tail=NativeModelTailBatch.Compile(before,packages,[]);byte[] expectedBody=tail.CopyFinalBody();
        var touched=new bool[0x80000];
        foreach(var patch in texture.Patches.Concat(debrisTexture.Patches))
        {
            check(!touched.AsSpan(patch.Offset,patch.ByteLength).Contains(true)&&
                !ownership.AsSpan(patch.Offset,patch.ByteLength).ContainsAnyExcept((byte)0)&&
                before.AsSpan(oldUploadAt+patch.Offset,patch.ByteLength).SequenceEqual(patch.CopyBefore()),
                "Every texture patch owns one disjoint exact parent-upload preimage outside all live owners, including zero bytes.");
            Array.Fill(touched,true,patch.Offset,patch.ByteLength);
            patch.CopyAfter().CopyTo(expectedBody,uploadAt+patch.Offset);
        }
        check(after.SequenceEqual(expectedBody)&&after.AsSpan(uploadAt,0x80000).SequenceEqual(debrisTexture.CopyUpload())&&
            before.AsSpan(oldUploadAt+0x65800,0x19800).SequenceEqual(after.AsSpan(uploadAt+0x65800,0x19800))&&
            child.CopyInverseResourceAncestorBody().SequenceEqual(before)&&
            child.CopyInverseBaseBody().SequenceEqual(ancestor.BaseBodyAncestor.CopyLevelData()),
            "Complete child Body equals shared model growth plus declared GPU patches, preserves CPU/DMA residue, and inverses through its parent to the actual base.");

        var reboundSound=NativeFreshMobySoundResidencyBinding.BindLockedChest(source,child);
        var reboundParticle=NativeFreshMobyParticleResidencyBinding.BindLockedChestKey(source,child);
        reboundSound.RequireAncestry(source,child);reboundSound.RequireExactLevelData(after);
        reboundParticle.RequireAncestry(source,child);reboundParticle.RequireExactLevelData(after);
        foreach(var entry in new[]{(Graph:reboundSound.Graph,Count:8),(Graph:reboundParticle.Graph,Count:34)})
        {
            var preserved=entry.Graph.Nodes.SelectMany(n=>n.Preserved.Select(p=>(Node:n,Preservation:p))).ToArray();
            check(preserved.Length==entry.Count&&preserved.All(p=>ReferenceEquals(p.Preservation.Final.Space,child.FinalBodyDomain)),
                "Existing sound/particle binders rebind every preservation to the actual new Final Body, never the parent domain.");
            foreach(var pair in preserved)
            {
                var p=pair.Preservation;var read=pair.Node.Reads.Single(r=>r.Id==p.ReadId);
                check(after.AsSpan((int)p.Final.Offset,(int)p.Final.ByteLength).SequenceEqual(
                    source.ReadSource(read.Extent).AsSpan(p.ReadOffset,(int)p.Final.ByteLength)),
                    "Every rebound sound/particle preservation equals the complete actual final bytes and retained Original read.");
            }
        }
        Reject<InvalidDataException>(()=>sound.RequireAncestry(source,child),check);
        Reject<InvalidDataException>(()=>reboundSound.RequireAncestry(source,ancestor),check);
        Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchExecutableSource.RequireFamilyComposition(child),check);
        Reject<InvalidDataException>(()=>NativeId65FreshWizardDiscGeometry.CompileFamily(child),check);
        Reject<InvalidDataException>(()=>NativeFreshMobyFamilyCompositionPlan.ComposeWithLockedChestResources(child,reboundSound,fallback),check);
        var repeat=NativeFreshMobyFamilyCompositionPlan.ComposeWithLockedChestResources(ancestor,sound,fallback);
        check(repeat.PlanSha256==child.PlanSha256&&repeat.CopyLevelData().SequenceEqual(after),
            "Repeated actual resource composition is deterministic without accumulating another package or allocation.");
        foreach(int at in new[]{0,(int)chestRoot.RootDataOffset,(int)chestRoot.RootDataOffset+4,
            uploadAt+texture.Locations[0].PixelOffset,uploadAt+texture.Locations[3].PaletteOffset,
            uploadAt+debrisTexture.Locations[0].PixelOffset,uploadAt+debrisTexture.Locations[0].PaletteOffset,newScene,after.Length-1}
            .Concat(packages.Skip(1).SelectMany(p=>new[]{(int)modelRoots[p.Id].RootDataOffset,
                (int)modelRoots[p.Id].RootDataOffset+p.ByteLength-1})))
        {byte[] bad=after.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>child.RequireExactLevelData(bad),check);}
        Reject<InvalidDataException>(()=>child.RequireExactLevelData(after[..^1]),check);
        byte[] inverse=child.CopyInverseResourceAncestorBody(),modelCopy=package.CopyBytes(),uploadCopy=texture.CopyUpload();
        byte[] debrisModelCopy=packages[1].CopyBytes(),debrisUploadCopy=debrisTexture.CopyUpload();
        inverse[0]^=1;modelCopy[4]^=1;uploadCopy[0]^=1;debrisModelCopy[0]^=1;debrisUploadCopy[0]^=1;after[^1]^=1;
        child.RequireExactLevelData(child.CopyLevelData());sound.RequireRetainedOwners();
        check(Hash(child.CopyLevelData())==child.LevelDataSha256&&child.CopyInverseResourceAncestorBody().SequenceEqual(before)&&
            ancestor.CopyLevelData().SequenceEqual(before)&&ancestor.CopyScene().SequenceEqual(scene),
            "Copy mutation and all refusals preserve child resources, actual parent Body/Scene and complete inverse.");
        Console.WriteLine($"NATIVE_FRESH_CHEST_RESOURCE_BODY_SOURCE_OK mixed={ancestor.BaseBodyAncestor.MixedResourceAncestor is not null} body={child.LevelDataSha256} packages=4 payload=16D8 textures=5 refs=210 rowsAdded=0 export=false");
    }

    private static void CheckInstalledDebris(NativeLockedChestFamilySource source,NativeFreshMobyFamilyCompositionPlan child,
        byte[] after,int uploadAt,Action<bool,string> check)
    {
        var texture=child.ChestDebrisTextureMaterialization!;var location=texture.Locations.Single();
        var allocation=texture.Allocations.Single();
        check(texture.Patches.Count==33&&texture.Patches.Sum(p=>p.ByteLength)==544&&location.PixelRowBytes==16&&location.Rows==32&&
            allocation.SourcePixelOffset==0x58A70-0x800&&allocation.SourcePaletteOffset==0x78AA0-0x800&&
            allocation.TargetPixelOffset==location.PixelOffset&&allocation.TargetPaletteOffset==location.PaletteOffset,
            "Three debris models share one installed32-square tile/CLUT,33patches and544bytes.");
        for(int row=0;row<=32;row++)
        {
            var read=source.ResourceInputs[row==32?"effects.native-debris-clut":"effects.native-debris-pixels."+row].Single();
            int at=row==32?location.PaletteOffset:location.PixelOffset+row*1024,length=row==32?32:16;
            check(texture.Patches[row].Offset==at&&texture.Patches[row].ByteLength==length&&
                after.AsSpan(uploadAt+at,length).SequenceEqual(source.ReadSource(read.Extent)),
                "Every installed debris row/CLUT equals its complete retained Original source footprint.");
        }
        foreach(var contribution in child.ChestModelContributions.Skip(1))
        {
            var read=source.ResourceInputs["effects.native-debris."+contribution.ActorId].Single();
            byte[] original=source.ReadSource(read.Extent),expected=original.ToArray();
            var grammar=NativeMobyModelTextureLayout.Decode(original);var first=grammar.References[0];
            // Derive the common tile origin from the historical owned pixels,
            // not from whichever subset of UVs this individual fragment uses.
            int sourceX=(0x58A70-0x800)%1024,sourceY=(0x58A70-0x800)/1024;
            int minU=(sourceX+1024-(first.Page&15)*128)*2,minV=sourceY-((first.Page&16)!=0?256:0);
            foreach(var reference in grammar.References)
                NativeMobyModelTextureLayout.Rebase(reference,location.U-minU,location.V-minV,location.Palette,
                    (ushort)((reference.Page&~31)|location.Page)).CopyTo(expected,reference.PackageOffset);
            check(contribution.CopyBytes().SequenceEqual(expected),
                "Each complete debris package contains exactly its independently derived descriptor rebases and unchanged model complement.");
            byte[] inverse=contribution.CopyBytes();
            var consumers=child.ChestTextureConsumers.Where(c=>c.Rewrite.Owner==contribution.Id).ToArray();
            check(consumers.Length==grammar.References.Count&&consumers.All(c=>c.TileIndex==4)&&
                consumers.Select(c=>c.Rewrite.Offset).SequenceEqual(grammar.References.Select(r=>r.PackageOffset)),
                "Every debris field has exactly one typed Final consumer of the shared fifth tile.");
            foreach(var consumer in consumers)
            {
                check(inverse.AsSpan(consumer.Rewrite.Offset,12).SequenceEqual(consumer.Rewrite.CopyAfter())&&
                    original.AsSpan(consumer.Rewrite.Offset,12).SequenceEqual(consumer.Rewrite.CopyBefore()),
                    "Every debris descriptor inverse checks its complete Original preimage and transformed afterimage.");
                consumer.Rewrite.CopyBefore().CopyTo(inverse,consumer.Rewrite.Offset);
            }
            check(inverse.SequenceEqual(original),"Debris descriptor inversion recovers the complete Original package.");
        }
    }

    private static void CheckRetainedDebrisGrammar(NativeLockedChestFamilySource source,Action<bool,string> check)
    {
        // Read only the source object already retained by this source pass. The
        // existing Town recipe is the independent descriptor-offset oracle.
        var specifications=new[]{(Class:0x135,At:0x1CC804,Length:0x224,Faces:20),
            (Class:0x136,At:0x1CCA28,Length:0x1D4,Faces:16),(Class:0x137,At:0x1CCBFC,Length:0x2D8,Faces:28)};
        List<NativeMobyModelTextureReference> references=[];
        foreach(var spec in specifications)
        {
            string id="effects.native-debris."+spec.Class;
            var read=source.ResourceInputs[id].Single();byte[] original=source.ReadSource(read.Extent);
            var grammar=NativeMobyModelTextureLayout.Decode(original);
            check(read.Extent.Offset==spec.At&&read.Extent.ByteLength==spec.Length&&Hash(original)==read.ExpectedSha256&&
                grammar.References.Count==spec.Faces&&grammar.References.Select(r=>r.PackageOffset)
                    .SequenceEqual(Enumerable.Range(0,spec.Faces).Select(f=>0x74+f*0x14)),
                "Each retained debris package decodes with the shared renderer grammar and the exact historical Town descriptor census.");
            var equivalent=source.ResourceInputs[id+".equivalence"];
            byte[] donor=source.ReadSource(equivalent.Single(r=>r.Id=="donor").Extent);
            byte[] target=source.ReadSource(equivalent.Single(r=>r.Id=="target").Extent);
            check(target.SequenceEqual(original)&&NativeMobyModelTextureLayout.Decode(donor).References.Count==spec.Faces,
                "The actual retained Town package and Peace equivalence input share the known complete debris grammar.");
            foreach(var reference in grammar.References)
            {donor.AsSpan(reference.PackageOffset,12).Clear();target.AsSpan(reference.PackageOffset,12).Clear();}
            check(donor.SequenceEqual(target),"Native debris source equivalence preserves every byte outside complete texture descriptors.");
            references.AddRange(grammar.References);
        }
        var groups=references.GroupBy(r=>(r.Page,r.Palette)).ToArray();
        check(references.Count==64&&groups.Length==1,"All64 retained debris renderer references share exactly one source tile and CLUT.");
        var group=groups.Single();int[] uv=[0,4,8,10];
        var coordinates=group.SelectMany(r=>{byte[] bytes=r.CopyBytes();return uv.Select(at=>(U:(int)bytes[at],V:(int)bytes[at+1]));}).ToArray();
        int minU=coordinates.Min(p=>p.U),minV=coordinates.Min(p=>p.V);
        int x=(group.Key.Page&15)*128+minU/2-1024,y=((group.Key.Page&16)!=0?256:0)+minV;
        int palette=(group.Key.Palette>>6)*1024+(group.Key.Palette&63)*32-1024;
        check(coordinates.Max(p=>p.U)==minU+31&&coordinates.Max(p=>p.V)==minV+31&&
            0x800+y*1024+x==0x58A70&&0x800+palette==0x78AA0,
            "The decoded debris footprint matches the existing complete32-square Town pixel/CLUT owners.");
        var domain=source.ResourceInputs["effects.native-debris.309"].Single().Extent.Space;
        for(int row=0;row<32;row++)
        {
            var read=source.ResourceInputs["effects.native-debris-pixels."+row].Single();
            check(ReferenceEquals(read.Extent.Space,domain)&&read.Extent.Offset==0x58A70+row*0x400&&read.Extent.ByteLength==16&&
                Hash(source.ReadSource(read.Extent))==read.ExpectedSha256,
                "Every decoded debris pixel row has its exact retained Original source read.");
        }
        var clut=source.ResourceInputs["effects.native-debris-clut"].Single();
        check(ReferenceEquals(clut.Extent.Space,domain)&&clut.Extent.Offset==0x78AA0&&clut.Extent.ByteLength==32&&
            Hash(source.ReadSource(clut.Extent))==clut.ExpectedSha256,"The complete debris CLUT retains its exact Original owner.");
        Console.WriteLine("NATIVE_FRESH_CHEST_DEBRIS_RETAINED_GRAMMAR_OK packages=3 refs=64 textures=1 pixels=58A70 clut=78AA0");
    }

    private static int Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Expected chest resource refusal "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
