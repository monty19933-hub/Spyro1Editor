using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeTwoWizardResearchSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Actual private source-issued dependencies, not metadata or publication authority.</summary>
internal sealed record NativeWizardResearchDependencies(NativeMobySourceInventory Inventory,
    NativePlayerAnimationMergePlan Player,NativeModelSoundMergePlan Sound,NativeDarkHollowWizardTexturePlan Texture,
    NativeDarkHollowWizardParticlePlan Descriptor,NativeDarkHollowWizardBehaviorSource Behavior,
    NativeDarkHollowWizardParticleBehaviorSource Particles);

/// <summary>
/// One physical WAD/ISO owner shared by the separately issued one/two profiles.
/// Construction takes actual private resource/overlay plans, never byte/hash claims.
/// No caller sector callback, output profile, or publication receipt issues this owner.
/// </summary>
internal sealed class NativeWizardResearchArtifactPlan
{
    private readonly byte[] _wad,_root,_executable,_header,_originalData,_originalOverlay;
    private readonly NativeFreeFlightResearchSourceCue _cue;
    private readonly NativeWadGrowthSectorPreparation _preparation;
    private readonly NativeSkyWadRelocationPlan _allocation;
    private readonly byte _wadFlags,_exeFlags;
    private readonly NativeDarkHollowWizardResourcePlan _resources;
    private readonly NativeDarkHollowWizardOverlayPlan _overlay;
    private NativeWizardResearchArtifactPlan(string source,NativeFreeFlightResearchSourceCue cue,DiscLayout layout,
        NativeDarkHollowWizardResourcePlan resources,NativeDarkHollowWizardOverlayPlan overlay,
        NativeDarkHollowWizardDeathRewardPlan reward,NativeWizardResearchPackedWad wad,byte[] root,byte[] header,
        byte[] originalData,NativeSkyWadRelocationPlan allocation,NativeWadGrowthSectorPreparation preparation,byte wadFlags,byte exeFlags)
    {
        SourceImagePath=source;_cue=cue;Layout=layout;_resources=resources;_overlay=overlay;Reward=reward;
        _wad=wad.Bytes;Entries=wad.Entries;OriginalWadSha256=wad.OriginalSha256;_root=root;_header=header;_originalData=originalData;
        _originalOverlay=overlay.CopyOriginalOverlay();_executable=reward.CopyExecutable();_allocation=allocation;_preparation=preparation;
        _wadFlags=wadFlags;_exeFlags=exeFlags;WadSha256=Hash(_wad);RootSha256=Hash(_root);
        RequireStageReversion();
    }
    internal string SourceImagePath {get;}
    internal string SourceCuePath=>_cue.CuePath;
    internal string SourceCueSha256=>_cue.SourceSha256;
    internal DiscLayout Layout {get;}
    internal string OriginalWadSha256 {get;}
    internal string WadSha256 {get;}
    internal string RootSha256 {get;}
    internal int GrowthBytes=>_allocation.WadGrowthBytes;
    internal int RelocatedExecutableLba=>_allocation.RelocatedExecutableLba;
    internal int FinalExecutableEndLba=>checked(RelocatedExecutableLba+_executable.Length/0x800);
    internal IReadOnlyList<NativeWizardResearchEntry> Entries {get;}
    internal NativeDarkHollowWizardDeathRewardPlan Reward {get;}
    internal byte[] CopyCueBytes()=>_cue.CopyOutputBytes();
    internal byte[] CopyWad()=>_wad.ToArray();
    internal byte[] CopyExecutable()=>_executable.ToArray();
    internal byte[] CopyRoot()=>_root.ToArray();

    internal static async Task<NativeWizardResearchDependencies> CaptureDependenciesAsync(string path,LevelCatalog catalog,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(catalog);
        var inventory=NativeMobySourceInventory.Capture(path,catalog);
        var playerSource=await NativePlayerAnimationSource.CaptureAsync(path,token);
        var player=await playerSource.CompileMergeAsync(path,33,12,[8],token);
        var soundSource=await NativeGlobalSoundSource.CaptureAsync(path,token);
        var sound=await soundSource.CompileModelSlotMergeAsync(path,inventory,33,12,0x11B,[0,1,2],
            [new(0x1F,NativeGlobalSoundMappingPolicy.ExplicitRebindUnresolvedConsumers),new(0x26)],token);
        var texture=NativeDarkHollowWizardTexturePlan.Capture(path);
        var descriptor=await NativeDarkHollowWizardParticlePlan.CaptureAsync(path,texture,token);
        return new(inventory,player,sound,texture,descriptor,NativeDarkHollowWizardBehaviorSource.Capture(path),
            NativeDarkHollowWizardParticleBehaviorSource.Capture(path));
    }
    internal static async Task<NativeWizardResearchArtifactPlan> CaptureAsync(string path,NativeFreeFlightResearchSourceCue cue,
        NativeDarkHollowWizardResourcePlan resources,NativeDarkHollowWizardOverlayPlan overlay,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(cue);
        ArgumentNullException.ThrowIfNull(resources);ArgumentNullException.ThrowIfNull(overlay);
        if(resources.SceneRevision==null||!ReferenceEquals(resources.SceneRevision,overlay.SceneRevision)||
            !resources.CopyFinalLevelData().AsSpan().SequenceEqual(overlay.CopyFinalLevelData()))
            throw new InvalidDataException("Physical research owners must retain the actual same resource/scene ancestor.");
        // The reward is captured HERE from this exact overlay, not accepted as an
        // equal-byte reward made for a different private ledger ancestry.
        var reward=NativeDarkHollowWizardDeathRewardPlan.Capture(path,overlay);
        using FileStream image=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        var layout=DiscImage.DetectLayout(path);
        byte[] header=DiscImage.ReadFileBytes(image,layout,37,0,0x800),originalData=DiscImage.ReadFileBytes(image,layout,37,0xF01000,0x28D800);
        var wad=NativeWizardResearchPackedEntryComposer.Compose(header,0x6927000,
            [new(13,0xEF6000,0xB000,Hash(overlay.CopyOriginalOverlay()),overlay.CopyOverlay()),
             new(14,0xF01000,0x28D800,resources.SourceDataSha256,resources.CopyFinalLevelData())],
            (offset,length)=>DiscImage.ReadFileBytes(image,layout,37,offset,length));
        int growth=checked(wad.Bytes.Length-0x6927000),exeLba=checked(53875+growth/0x800);
        var files=NativeWadGrowthSectorPreparation.ReadFlatRootFiles(image,layout);
        var sourceExe=files.Single(f=>f.Name=="SCUS_942.28");var next=files.Where(f=>f.Lba>sourceExe.Lba).MinBy(f=>f.Lba)!;
        var changes=wad.Entries.Where(e=>e.FinalLength!=e.OriginalLength).ToDictionary(e=>e.Index,e=>e.FinalLength-e.OriginalLength);
        var allocation=new NativeSkyWadRelocationPlan(true,37,0x6927000,wad.Bytes.Length,growth,sourceExe.Lba,exeLba,sourceExe.Size,next.Lba,
            checked((next.Lba-(sourceExe.Lba+sourceExe.Size/0x800))*0x800),changes,wad.Entries.ToDictionary(e=>e.Index,e=>(long)e.FinalOffset),[]);
        if(growth!=0x14800||growth/0x800!=41||exeLba!=53916||sourceExe.Lba!=53875||sourceExe.Size!=0x66000||
            changes.Count!=2||changes.GetValueOrDefault(13)!=0x1800||changes.GetValueOrDefault(14)!=0x13000)
            throw new InvalidDataException("Research source-derived WAD/EXE allocation differs from the exact41-sector profile.");
        var preparation=NativeWadGrowthSectorPreparation.Create(image,layout,allocation);
        byte[] root=DiscImage.ReadFileBytes(image,layout,layout.RootExtent,0,layout.RootLength);
        foreach(var change in new[]{("WAD.WAD",37,wad.Bytes.Length),("SCUS_942.28",exeLba,sourceExe.Size)})
        {
            var file=files.Single(f=>f.Name==change.Item1);Both(root,file.RecordOffset+2,change.Item2);Both(root,file.RecordOffset+10,change.Item3);
        }
        byte Flags(int lba){image.Position=(long)lba*2352+18;return (byte)(image.ReadByte()&0x81);}

        var plan=new NativeWizardResearchArtifactPlan(path,cue,layout,resources,overlay,reward,wad,root,header,originalData,
            allocation,preparation,Flags(53874),Flags(54078));
        await plan.RequireFreshAsync(token);return plan;
    }
    internal async Task RequireFreshAsync(CancellationToken token=default)
    {
        _cue.RequireFresh();using FileStream input=new(SourceImagePath,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(input.Length!=NativeMobySourceInventory.OriginalImageByteLength||
            Convert.ToHexStringLower(await SHA256.HashDataAsync(input,token))!=NativeMobySourceInventory.OriginalImageSha256||
            input.Length!=NativeMobySourceInventory.OriginalImageByteLength||input.Position!=NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Research source image changed complete identity.");
        _cue.RequireFresh();
    }

    internal void RequireStageReversion()
    {
        var data=_resources.CopyFinalLevelData();_resources.RequireFinal(data);
        _overlay.RequireFinal(_overlay.CopyOverlay(),_overlay.CopyExecutable(),data);
        Reward.RequireExecutable(_executable);
        if(!Reward.RevertToOverlayExecutable(_executable).AsSpan().SequenceEqual(_overlay.CopyExecutable()))
            throw new InvalidDataException("Research reward did not revert exactly to source-issued overlay EXE.");
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);hash.AppendData(_header);
        foreach(var entry in Entries)
        {
            byte[] before=entry.Index==13?_originalOverlay:entry.Index==14?_originalData:_wad.AsSpan(entry.FinalOffset,entry.FinalLength).ToArray();
            if(before.Length!=entry.OriginalLength||Hash(before)!=entry.OriginalSha256)throw new InvalidDataException("Research inverse entry preimage changed.");
            hash.AppendData(before);
        }
        if(Convert.ToHexStringLower(hash.GetHashAndReset())!=OriginalWadSha256)
            throw new InvalidDataException("Research inverse WAD does not reconstruct the complete original source.");
    }
    internal void WriteNewImage(string outputPath,CancellationToken token=default)
    {
        using FileStream source=new(SourceImagePath,FileMode.Open,FileAccess.Read,FileShare.Read);
        using FileStream output=new(outputPath,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
        source.CopyTo(output);token.ThrowIfCancellationRequested();_preparation.ApplyToExactCopy(output);
        DiscImage.WriteFileBytes(output,Layout,37,0,_wad);DiscImage.WriteFileBytes(output,Layout,RelocatedExecutableLba,0,_executable);
        DiscImage.WriteFileBytes(output,Layout,Layout.RootExtent,0,_root);output.Flush(true);
    }
    internal (string Hash,int Changed) VerifyImage(string path)
    {
        using FileStream source=new(SourceImagePath,FileMode.Open,FileAccess.Read,FileShare.Read),output=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        var files=NativeWadGrowthSectorPreparation.ReadFlatRootFiles(output,Layout);
        if(files.Single(f=>f.Name=="WAD.WAD").Size!=_wad.Length||files.Single(f=>f.Name=="SCUS_942.28").Lba!=RelocatedExecutableLba)
            throw new InvalidDataException("Research actual ISO owners differ from the final plan.");
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

    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Both(byte[] bytes,int offset,int value)
    {BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset),value);BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset+4),value);}
}
