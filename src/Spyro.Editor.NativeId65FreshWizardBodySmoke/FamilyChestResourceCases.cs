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
        byte[] before=ancestor.CopyLevelData(),scene=ancestor.CopyScene();
        check(!ancestor.HasUnplacedModelResources&&ancestor.ResourceAncestor is null&&
            ancestor.ChestModelContribution is null&&ancestor.ChestTextureMaterialization is null,
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
        byte[] after=child.CopyLevelData();var package=child.ChestModelContribution!;var texture=child.ChestTextureMaterialization!;
        check(child.HasUnplacedModelResources&&ReferenceEquals(child.ResourceAncestor,ancestor)&&
            ReferenceEquals(child.BaseBodyAncestor,ancestor.BaseBodyAncestor)&&
            !ReferenceEquals(child.FinalBodyDomain,ancestor.FinalBodyDomain)&&
            child.FinalBodyDomain.Revision==NativeAddressRevision.Final&&child.FinalBodyDomain.ByteLength==after.Length&&
            ReferenceEquals(child.FinalSceneDomain,ancestor.FinalSceneDomain)&&ReferenceEquals(child.Allocation,ancestor.Allocation),
            "Resource composition retains its actual parent, unchanged scene allocation and a new complete Final Body owner.");
        check(child.CopyScene().SequenceEqual(scene)&&child.Placements.SequenceEqual(ancestor.Placements)&&
            child.Layout.PlacedCount==ancestor.Layout.PlacedCount&&child.Layout.DynamicCapacity==ancestor.Layout.DynamicCapacity&&
            !child.CompleteNativeBehaviorProven&&!child.RuntimeVerified&&!child.PublicationApproved,
            "AE resource installation adds no key/chest/reward rows, behavior or export approval.");
        int oldModels=Word(before,0x10),oldLength=Word(before,0x14),oldScene=Word(before,0x18);
        int newModels=Word(after,0x10),newLength=Word(after,0x14),newScene=Word(after,0x18);
        var oldRoots=NativeMobyRegistrationTableLayout.Decode(before.AsSpan(0,0x800),oldModels,oldLength).Registrations;
        var newRoots=NativeMobyRegistrationTableLayout.Decode(after.AsSpan(0,0x800),newModels,newLength).Registrations;
        var chestRoot=newRoots.Single(r=>r.ActorId==0xAE);
        check(package.ActorId==0xAE&&package.ByteLength==0x1008&&Word(package.CopyBytes(),4)==unchecked((int)0xFFFFFF36)&&
            newRoots.Count==oldRoots.Count+1&&newRoots.Take(oldRoots.Count).SequenceEqual(oldRoots)&&
            chestRoot.Slot==oldRoots.Count&&chestRoot.RootDataOffset==oldModels+oldLength&&
            newModels==oldModels&&newLength==oldLength+0x1800&&newScene==oldScene+0x1800&&after.Length==before.Length+0x1800,
            "One complete registered AE package grows the actual model allocation once by1800, retaining all old roots.");
        check(after.AsSpan((int)chestRoot.RootDataOffset,0x1008).SequenceEqual(package.CopyBytes())&&
            !after.AsSpan((int)chestRoot.RootDataOffset+0x1008,0x7F8).ContainsAnyExcept((byte)0)&&
            after.AsSpan(newScene,scene.Length).SequenceEqual(scene)&&
            child.ModelRuntimeAddress==ancestor.ModelRuntimeAddress&&child.SceneRuntimeAddress==ancestor.SceneRuntimeAddress+0x1800&&
            child.KnownSceneEndAddress==ancestor.KnownSceneEndAddress+0x1800&&
            child.KnownSceneEndAddress<=ancestor.BaseBodyAncestor.NormalReservedLowerAddress&&newLength+scene.Length<=0x80000,
            "Installed AE bytes, generated model padding, unchanged scene and existing runtime-reserve bounds remain exact.");

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
        check(child.ChestTextureConsumers.Count==146&&child.ChestTextureConsumers.All(c=>
            ReferenceEquals(c.Descriptor.Space,child.FinalBodyDomain)&&c.Descriptor.ByteLength==12&&c.TileIndex is >=0 and <4&&
            c.Descriptor.Offset==chestRoot.RootDataOffset+c.Rewrite.Offset&&
            after.AsSpan((int)c.Descriptor.Offset,12).SequenceEqual(c.Rewrite.CopyAfter())),
            "All146 renderer consumers bind complete installed fields in the actual new Final model domain.");
        var tail=NativeModelTailBatch.Compile(before,[package],[]);byte[] expectedBody=tail.CopyFinalBody();
        var touched=new bool[0x80000];
        foreach(var patch in texture.Patches)
        {
            check(!touched.AsSpan(patch.Offset,patch.ByteLength).Contains(true)&&
                before.AsSpan(oldUploadAt+patch.Offset,patch.ByteLength).SequenceEqual(patch.CopyBefore()),
                "Every texture patch owns one disjoint exact parent-upload preimage, including zero bytes.");
            Array.Fill(touched,true,patch.Offset,patch.ByteLength);
            patch.CopyAfter().CopyTo(expectedBody,uploadAt+patch.Offset);
        }
        check(after.SequenceEqual(expectedBody)&&after.AsSpan(uploadAt,0x80000).SequenceEqual(texture.CopyUpload())&&
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
            uploadAt+texture.Locations[0].PixelOffset,uploadAt+texture.Locations[3].PaletteOffset,newScene,after.Length-1})
        {byte[] bad=after.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>child.RequireExactLevelData(bad),check);}
        Reject<InvalidDataException>(()=>child.RequireExactLevelData(after[..^1]),check);
        byte[] inverse=child.CopyInverseResourceAncestorBody(),modelCopy=package.CopyBytes(),uploadCopy=texture.CopyUpload();
        inverse[0]^=1;modelCopy[4]^=1;uploadCopy[0]^=1;after[^1]^=1;
        child.RequireExactLevelData(child.CopyLevelData());sound.RequireRetainedOwners();
        check(Hash(child.CopyLevelData())==child.LevelDataSha256&&child.CopyInverseResourceAncestorBody().SequenceEqual(before)&&
            ancestor.CopyLevelData().SequenceEqual(before)&&ancestor.CopyScene().SequenceEqual(scene),
            "Copy mutation and all refusals preserve child resources, actual parent Body/Scene and complete inverse.");
        Console.WriteLine($"NATIVE_FRESH_CHEST_RESOURCE_BODY_SOURCE_OK mixed={ancestor.BaseBodyAncestor.MixedResourceAncestor is not null} body={child.LevelDataSha256} package=1008 textures=4 refs=146 rowsAdded=0 export=false");
    }

    private static int Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Expected chest resource refusal "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
