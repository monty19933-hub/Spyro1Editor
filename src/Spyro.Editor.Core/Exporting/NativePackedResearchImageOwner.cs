using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Exact-USA packed-WAD physical mechanics shared by closed source-plan factories.
/// No arbitrary byte-array, sector callback or metadata constructor issues this owner.
/// Profile/placement/publication decisions remain in the separate typed wrappers.
/// </summary>
internal sealed class NativePackedResearchImageOwner
{
    private readonly byte[] _wad,_root,_executable,_header;
    private readonly Dictionary<int,byte[]> _originalReplacements;
    private readonly NativeWadGrowthSectorPreparation _preparation;
    private readonly NativeSkyWadRelocationPlan _allocation;
    private readonly byte _wadFlags,_exeFlags;
    private NativePackedResearchImageOwner(string source,DiscLayout layout,NativeWizardResearchPackedWad wad,
        byte[] root,byte[] executable,byte[] header,Dictionary<int,byte[]> originals,
        NativeSkyWadRelocationPlan allocation,NativeWadGrowthSectorPreparation preparation,byte wadFlags,byte exeFlags)
    {
        SourceImagePath=source;Layout=layout;_wad=wad.Bytes;Entries=wad.Entries;OriginalWadSha256=wad.OriginalSha256;
        _root=root;_executable=executable;_header=header;_originalReplacements=originals;_allocation=allocation;
        _preparation=preparation;_wadFlags=wadFlags;_exeFlags=exeFlags;WadSha256=Hash(_wad);RootSha256=Hash(_root);
        RequireOriginalWadReversion();
    }
    internal string SourceImagePath{get;}
    internal DiscLayout Layout{get;}
    internal string OriginalWadSha256{get;}
    internal string WadSha256{get;}
    internal string RootSha256{get;}
    internal int GrowthBytes=>_allocation.WadGrowthBytes;
    internal int RelocatedExecutableLba=>_allocation.RelocatedExecutableLba;
    internal int FinalExecutableEndLba=>RelocatedExecutableLba+_executable.Length/0x800;
    internal IReadOnlyList<NativeWizardResearchEntry> Entries{get;}
    internal byte[] CopyWad()=>_wad.ToArray();
    internal byte[] CopyRoot()=>_root.ToArray();
    internal byte[] CopyExecutable()=>_executable.ToArray();

    internal static NativePackedResearchImageOwner CaptureWizard(string source,NativeDarkHollowWizardResourcePlan resources,
        NativeDarkHollowWizardOverlayPlan overlay,NativeDarkHollowWizardDeathRewardPlan reward)
    {
        ArgumentNullException.ThrowIfNull(resources);ArgumentNullException.ThrowIfNull(overlay);ArgumentNullException.ThrowIfNull(reward);
        if(resources.SceneRevision==null||!ReferenceEquals(resources.SceneRevision,overlay.SceneRevision)||
            !resources.CopyFinalLevelData().AsSpan().SequenceEqual(overlay.CopyFinalLevelData())||
            !reward.RevertToOverlayExecutable(reward.CopyExecutable()).AsSpan().SequenceEqual(overlay.CopyExecutable()))
            throw new InvalidDataException("Wizard physical owner lost actual resource/overlay/reward ancestry.");
        byte[] originalOverlay=overlay.CopyOriginalOverlay();
        return Capture(source,reward.CopyExecutable(),0x14800,new Dictionary<int,int>{{13,0x1800},{14,0x13000}},
            (header,read)=>NativeWizardResearchPackedEntryComposer.Compose(header,0x6927000,[
                new(13,0xEF6000,0xB000,Hash(originalOverlay),overlay.CopyOverlay()),
                new(14,0xF01000,0x28D800,resources.SourceDataSha256,resources.CopyFinalLevelData())],read),
            read=>new(){{13,originalOverlay},{14,read(0xF01000,0x28D800)}});
    }

    internal static NativePackedResearchImageOwner CaptureEgg(string source,NativeJapaneseEggArtisansIntegrationPlan integration,
        NativeJapaneseEggRenderAdmissionPlan render,NativeJapaneseEggMemoryReport memory)
    {
        ArgumentNullException.ThrowIfNull(integration);ArgumentNullException.ThrowIfNull(render);ArgumentNullException.ThrowIfNull(memory);
        if(!integration.SourceCertified||!render.SourceCertified||!ReferenceEquals(render.Integration,integration)||
            !render.AnimatedListAndModelOwnersProven||memory.IntegrationPlanSha256!=integration.PlanSha256||
            !memory.SourceCertified||!memory.ModeledLoadExtentsFitProven||!memory.SynchronousVisualStackFitProven||
            integration.Rows.Count!=2||integration.Rows.Any(r=>r.Placement.ProfileIndex!=1))
            throw new InvalidDataException("Egg physical research owner requires the same source-issued two-mode1 integration/render/memory ancestry.");
        integration.RequireFinal(integration.CopyOverlay(),integration.CopyExecutable(),integration.CopyLevelData());
        if(!integration.RevertDataExact(integration.CopyLevelData()).AsSpan().SequenceEqual(integration.CopyOriginalLevelData())||
            !integration.RevertExecutableExact(integration.CopyExecutable()).AsSpan().SequenceEqual(integration.CopyOriginalExecutable()))
            throw new InvalidDataException("Egg physical research inverse differs from original source.");
        return Capture(source,integration.CopyExecutable(),0x1800,new Dictionary<int,int>{{9,0x800},{10,0x1000}},
            (header,read)=>NativePackedResearchEntryComposer.ComposeArtisans(header,0x6927000,integration,read),
            _=>new(){{9,integration.CopyOriginalOverlay()},{10,integration.CopyOriginalLevelData()}});
    }

    internal static NativePackedResearchImageOwner CaptureAuthoredEgg(string source,NativeJapaneseEggAuthoredClassesPlan plan,
        NativeJapaneseEggRenderAdmissionPlan render,NativeJapaneseEggAuthoredMemoryReport memory)
    {
        ArgumentNullException.ThrowIfNull(plan);ArgumentNullException.ThrowIfNull(render);ArgumentNullException.ThrowIfNull(memory);
        var integration=plan.Integration;
        if(!plan.SourceCertified||!render.SourceCertified||!ReferenceEquals(render.Integration,integration)||
            !render.AnimatedListAndModelOwnersProven||memory.PlanSha256!=plan.PlanSha256||
            memory.FinalDataSha256!=plan.FinalDataSha256||memory.FinalOverlaySha256!=plan.FinalOverlaySha256||
            !memory.ModeledLoadExtentsFitProven||!memory.SynchronousVisualStackFitProven||
            memory.Values.SceneEnd!=plan.Projection.ProjectedSceneReadEnd||memory.Values.NormalGap!=plan.Projection.ProjectedNormalGap||
            integration.Rows.Count!=2||integration.Rows.Any(r=>r.Placement.ProfileIndex!=1)||
            !plan.Bindings.Select(b=>b.AuthoredDestinationClass).SequenceEqual(new ushort[]{0x1E9,0x1F2}))
            throw new InvalidDataException("Authored egg physical owner requires the actual final class/render/memory ancestry, not the smaller ancestor gap.");
        if(!integration.RevertDataExact(plan.RevertDataExact(plan.CopyLevelData())).AsSpan().SequenceEqual(integration.CopyOriginalLevelData())||
            !integration.RevertOverlayExact(plan.RevertOverlayExact(plan.CopyOverlay())).AsSpan().SequenceEqual(integration.CopyOriginalOverlay())||
            !integration.RevertExecutableExact(plan.CopyExecutable()).AsSpan().SequenceEqual(integration.CopyOriginalExecutable()))
            throw new InvalidDataException("Authored egg inverse does not reach the complete original resource/overlay/executable.");
        return Capture(source,plan.CopyExecutable(),0x3000,new Dictionary<int,int>{{9,0x800},{10,0x2800}},
            (header,read)=>NativePackedResearchEntryComposer.ComposeAuthoredArtisans(header,0x6927000,plan,read),
            _=>new(){{9,integration.CopyOriginalOverlay()},{10,integration.CopyOriginalLevelData()}});
    }

    internal static NativePackedResearchImageOwner CaptureCardLoad(string source,NativeCollectibleCardLoadResearchComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);composition.RequireAncestry();
        if(!composition.RevertExecutableExact(composition.CopyExecutable()).AsSpan().SequenceEqual(composition.CopyOriginalExecutable()))
            throw new InvalidDataException("Card-load physical owner lost complete original executable ancestry.");
        byte[] original=composition.Card.CopyOriginalOverlay();
        return Capture(source,composition.CopyExecutable(),0x7000,new Dictionary<int,int>{{2,0x7000}},
            (header,read)=>NativeWizardResearchPackedEntryComposer.ComposeCardLoad(header,0x6927000,composition,read),
            _=>new(){{2,original}});
    }

    // Only the closed typed factories above can choose these payloads.
    private static NativePackedResearchImageOwner Capture(string source,byte[] executable,int expectedGrowth,
        Dictionary<int,int> expectedChanges,Func<byte[],Func<int,int,byte[]>,NativeWizardResearchPackedWad> compose,
        Func<Func<int,int,byte[]>,Dictionary<int,byte[]>> originals)
    {
        using FileStream image=new(source,FileMode.Open,FileAccess.Read,FileShare.Read);
        RequireExactSource(image);
        DiscLayout layout=DiscImage.DetectLayout(source);
        if(layout.SectorSize!=2352||layout.UserOffset!=24||executable.Length!=0x66000)
            throw new InvalidDataException("Packed research physical owner requires exact-USA raw sector/EXE geometry.");
        byte[] Read(int offset,int length)=>DiscImage.ReadFileBytes(image,layout,37,offset,length);
        byte[] header=Read(0,0x800);var wad=compose(header,Read);var before=originals(Read);
        int growth=checked(wad.Bytes.Length-0x6927000),exeLba=checked(53875+growth/0x800);
        var files=NativeWadGrowthSectorPreparation.ReadFlatRootFiles(image,layout);
        var sourceExe=files.Single(f=>f.Name=="SCUS_942.28");var next=files.Where(f=>f.Lba>sourceExe.Lba).MinBy(f=>f.Lba)!;
        var changes=wad.Entries.Where(e=>e.FinalLength!=e.OriginalLength).ToDictionary(e=>e.Index,e=>e.FinalLength-e.OriginalLength);
        if(growth!=expectedGrowth||sourceExe.Lba!=53875||sourceExe.Size!=0x66000||changes.Count!=expectedChanges.Count||
            expectedChanges.Any(p=>changes.GetValueOrDefault(p.Key)!=p.Value))
            throw new InvalidDataException("Packed source-derived WAD/EXE geometry differs from the closed profile.");
        var allocation=new NativeSkyWadRelocationPlan(true,37,0x6927000,wad.Bytes.Length,growth,sourceExe.Lba,exeLba,sourceExe.Size,next.Lba,
            checked((next.Lba-(sourceExe.Lba+sourceExe.Size/0x800))*0x800),changes,wad.Entries.ToDictionary(e=>e.Index,e=>(long)e.FinalOffset),[]);
        var preparation=NativeWadGrowthSectorPreparation.Create(image,layout,allocation);
        byte[] root=DiscImage.ReadFileBytes(image,layout,layout.RootExtent,0,layout.RootLength);
        NativeFreshResearchPhysicalAfterimages.PatchRoot(root,files.Single(f=>f.Name=="WAD.WAD"),37,wad.Bytes.Length);
        NativeFreshResearchPhysicalAfterimages.PatchRoot(root,sourceExe,exeLba,sourceExe.Size);
        byte Flags(int lba){image.Position=(long)lba*2352+18;return (byte)(image.ReadByte()&0x81);}
        byte wadFlags=Flags(53874),exeFlags=Flags(54078);
        RequireExactSource(image);
        return new(source,layout,wad,root,executable,header,before,allocation,preparation,wadFlags,exeFlags);
    }
    internal void RequireOriginalWadReversion()
    {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);hash.AppendData(_header);
        foreach(var entry in Entries)
        {
            byte[] original=_originalReplacements.TryGetValue(entry.Index,out var b)?b:_wad.AsSpan(entry.FinalOffset,entry.FinalLength).ToArray();
            if(original.Length!=entry.OriginalLength||Hash(original)!=entry.OriginalSha256)
                throw new InvalidDataException("Packed research inverse entry preimage changed.");
            hash.AppendData(original);
        }
        if(Convert.ToHexStringLower(hash.GetHashAndReset())!=OriginalWadSha256)
            throw new InvalidDataException("Packed research inverse does not reconstruct the complete original WAD.");
    }
    internal async Task RequireFreshAsync(CancellationToken token=default)
    {
        using FileStream image=new(SourceImagePath,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(image.Length!=NativeMobySourceInventory.OriginalImageByteLength||
            Convert.ToHexStringLower(await SHA256.HashDataAsync(image,token))!=NativeMobySourceInventory.OriginalImageSha256||
            image.Length!=NativeMobySourceInventory.OriginalImageByteLength||image.Position!=NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Packed research source image changed complete identity.");
    }
    internal void WriteNewImage(string path,CancellationToken token=default)
    {
        using FileStream source=new(SourceImagePath,FileMode.Open,FileAccess.Read,FileShare.Read);
        using FileStream output=new(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
        source.CopyTo(output);token.ThrowIfCancellationRequested();_preparation.ApplyToExactCopy(output);
        DiscImage.WriteFileBytes(output,Layout,37,0,_wad);DiscImage.WriteFileBytes(output,Layout,RelocatedExecutableLba,0,_executable);
        DiscImage.WriteFileBytes(output,Layout,Layout.RootExtent,0,_root);output.Flush(true);
    }
    internal (string Hash,int Changed) VerifyImage(string path)
    {
        using FileStream source=new(SourceImagePath,FileMode.Open,FileAccess.Read,FileShare.Read),output=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        var files=NativeWadGrowthSectorPreparation.ReadFlatRootFiles(output,Layout);
        if(files.Single(f=>f.Name=="WAD.WAD").Size!=_wad.Length||files.Single(f=>f.Name=="SCUS_942.28").Lba!=RelocatedExecutableLba)
            throw new InvalidDataException("Packed research actual ISO owners differ from plan.");
        return NativeTownSquareClosurePhysicalReadback.Verify(source,output,NativeMobySourceInventory.OriginalImageByteLength,
            NativeMobySourceInventory.OriginalImageSha256,ExpectedSector);
    }
    internal byte[] ExpectedSector(int lba,byte[] before)
    {
        byte[] expected=before.ToArray();
        if(lba>=54079&&lba<FinalExecutableEndLba)expected=RawMode2Form1SectorIntegrity.PrepareOwnedZeroPaddingSector(before,Layout,lba);
        if(lba>=37&&lba<RelocatedExecutableLba)_wad.AsSpan((lba-37)*0x800,0x800).CopyTo(expected.AsSpan(24));
        if(lba>=RelocatedExecutableLba&&lba<FinalExecutableEndLba)_executable.AsSpan((lba-RelocatedExecutableLba)*0x800,0x800).CopyTo(expected.AsSpan(24));
        int rootOffset=(lba-Layout.RootExtent)*0x800;
        if(rootOffset>=0&&rootOffset<_root.Length)_root.AsSpan(rootOffset,Math.Min(0x800,_root.Length-rootOffset)).CopyTo(expected.AsSpan(24));
        if(lba==53874||lba==54078||lba==RelocatedExecutableLba-1||lba==FinalExecutableEndLba-1)
        {
            byte flags=lba==RelocatedExecutableLba-1?_wadFlags:lba==FinalExecutableEndLba-1?_exeFlags:(byte)0;
            expected[18]=expected[22]=(byte)((expected[18]&~0x81)|flags);
        }
        return expected.AsSpan().SequenceEqual(before)?expected:RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(expected,lba);
    }
    private static void RequireExactSource(FileStream image)
    {
        image.Position=0;
        if(image.Length!=NativeMobySourceInventory.OriginalImageByteLength||HashStream(image)!=NativeMobySourceInventory.OriginalImageSha256||
            image.Length!=NativeMobySourceInventory.OriginalImageByteLength||image.Position!=NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Packed research source is not the complete exact-USA image.");
    }
    private static string HashStream(Stream s)=>Convert.ToHexStringLower(SHA256.HashData(s));
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}
