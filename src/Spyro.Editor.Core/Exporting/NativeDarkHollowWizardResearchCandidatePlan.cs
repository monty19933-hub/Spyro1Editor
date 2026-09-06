using System.Runtime.CompilerServices;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeWizardResearchSmoke")]

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeWizardResearchReplacement(int Index,int OriginalOffset,int OriginalLength,string OriginalSha256,byte[] Bytes);
public sealed record NativeWizardResearchEntry(int Index,int OriginalOffset,int FinalOffset,int OriginalLength,int FinalLength,string OriginalSha256,string FinalSha256);
internal sealed record NativeWizardResearchPackedWad(byte[] Bytes,IReadOnlyList<NativeWizardResearchEntry> Entries,string OriginalSha256);

internal static class NativeWizardResearchPackedEntryComposer
{
    internal static NativeWizardResearchPackedWad Compose(ReadOnlySpan<byte> header,int originalLength,
        IEnumerable<NativeWizardResearchReplacement> replacements,Func<int,int,byte[]> read)
    {
        ArgumentNullException.ThrowIfNull(replacements);ArgumentNullException.ThrowIfNull(read);
        if(header.Length!=0x800)throw new InvalidDataException("Research WAD header owner must be exactly one sector.");
        if(originalLength is <0x800 or >0x10000000||originalLength%0x800!=0)throw new InvalidDataException("Research original WAD exceeds its aligned budget.");
        Dictionary<int,NativeWizardResearchReplacement> installed=[];
        int raw=0;
        foreach(var replacement in replacements)
        {
            if(++raw>2)throw new InvalidDataException("Research replacement enumeration exceeds its two-owner budget.");
            if(replacement==null||replacement.Index is not(13 or 14)||replacement.Bytes==null)
                throw new InvalidDataException("Research replacement is not a recognized Dark entry owner.");
            if(replacement.Bytes.Length<replacement.OriginalLength||replacement.Bytes.Length>0x10000000||replacement.Bytes.Length%0x800!=0)
                throw new InvalidDataException("Research replacement alignment/length exceeds its bounded owner.");
            // Snapshot only the bounded immutable record references here. Do
            // not clone payloads until actual directory owners and the complete
            // final allocation budget have both been validated below.
            if(!installed.TryAdd(replacement.Index,replacement))
                throw new InvalidDataException("Research replacement contains a duplicate owner.");
        }
        if(installed.Count!=2)throw new InvalidDataException("Research requires exactly both Dark owners.");
        List<(int Index,int Offset,int Length)> entries=[];
        for(int index=0;index<256;index++)
        {
            uint offset=U32(header,index*8),length=U32(header,index*8+4);
            if(offset==0&&length==0)continue;
            if(offset<0x800||length==0||offset%0x800!=0||length%0x800!=0||offset+(long)length>originalLength)
                throw new InvalidDataException("Research source entry owner is out of bounds or unaligned.");
            entries.Add((index,(int)offset,(int)length));
        }
        var sorted=entries.OrderBy(e=>e.Offset).ToArray();int oldCursor=0x800,newLength=0x800;
        foreach(var e in sorted)
        {
            if(e.Offset!=oldCursor)throw new InvalidDataException("Research source WAD entries are not fully packed.");
            oldCursor=checked(oldCursor+e.Length);
            if(installed.TryGetValue(e.Index,out var r)&&(r.OriginalOffset!=e.Offset||r.OriginalLength!=e.Length))
                throw new InvalidDataException("Research replacement preimage does not match its actual directory owner.");
            newLength=checked(newLength+(installed.GetValueOrDefault(e.Index)?.Bytes.Length??e.Length));
            if(newLength>0x10000000)throw new InvalidDataException("Research final WAD exceeds its bounded budget.");
        }
        if(oldCursor!=originalLength||!installed.Keys.All(k=>entries.Any(e=>e.Index==k)))
            throw new InvalidDataException("Research packed WAD is incomplete or misses an owned entry.");
        foreach(int index in installed.Keys.ToArray())
        {
            var replacement=installed[index];
            installed[index]=replacement with{Bytes=replacement.Bytes.ToArray()};
        }
        byte[] final=new byte[newLength],finalHeader=header.ToArray();List<NativeWizardResearchEntry> reports=[];int cursor=0x800;
        using var sourceHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);sourceHash.AppendData(header);
        foreach(var e in sorted)
        {
            byte[] before=read(e.Offset,e.Length);
            if(before==null||before.Length!=e.Length)throw new InvalidDataException("Research source reader returned a different owner length before copying.");
            before=before.ToArray();string hash=Hash(before);sourceHash.AppendData(before);
            byte[] after=before;
            if(installed.TryGetValue(e.Index,out var r))
            {
                if(hash!=r.OriginalSha256)throw new InvalidDataException("Research replacement source preimage hash changed.");
                after=r.Bytes;
            }
            after.CopyTo(final,cursor);Put(finalHeader,e.Index*8,cursor);Put(finalHeader,e.Index*8+4,after.Length);
            reports.Add(new(e.Index,e.Offset,cursor,e.Length,after.Length,hash,Hash(after)));cursor+=after.Length;
        }
        finalHeader.CopyTo(final,0);
        return new(final,Array.AsReadOnly(reports.ToArray()),Convert.ToHexStringLower(sourceHash.GetHashAndReset()));
    }
    private static uint U32(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static void Put(byte[] bytes,int offset,int value)=>BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset),value);
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

/// <summary>One explicit source-bound research profile; not ordinary export or gameplay approval.</summary>
public sealed class NativeDarkHollowWizardResearchCandidatePlan
{
    private readonly byte[] _wad,_root,_executable,_header,_originalData,_originalOverlay;
    private readonly NativeFreeFlightResearchSourceCue _cue;
    private readonly NativeWadGrowthSectorPreparation _preparation;
    private readonly NativeSkyWadRelocationPlan _allocation;
    private readonly byte _wadFlags,_exeFlags;
    private NativeDarkHollowWizardResearchCandidatePlan(string source,NativeFreeFlightResearchSourceCue cue,DiscLayout layout,
        NativeDarkHollowWizardPlacementPlan placement,NativeDarkHollowWizardResourcePlan resources,
        NativeDarkHollowWizardOverlayPlan overlay,NativeDarkHollowWizardDeathRewardPlan reward,
        NativeWizardResearchPackedWad wad,byte[] root,byte[] header,byte[] originalData,
        NativeSkyWadRelocationPlan allocation,NativeWadGrowthSectorPreparation preparation,byte wadFlags,byte exeFlags)
    {
        SourceImagePath=source;_cue=cue;Layout=layout;Placement=placement;Resources=resources;Overlay=overlay;Reward=reward;
        _wad=wad.Bytes;Entries=wad.Entries;OriginalWadSha256=wad.OriginalSha256;_root=root;_header=header;_originalData=originalData;
        _originalOverlay=overlay.CopyOriginalOverlay();_executable=reward.CopyExecutable();_allocation=allocation;_preparation=preparation;
        _wadFlags=wadFlags;_exeFlags=exeFlags;WadSha256=Hash(_wad);RootSha256=Hash(_root);
        RequireStageReversion();
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{Profile,SourceImagePath,sourceImageSha256=NativeMobySourceInventory.OriginalImageSha256,
            sourceCuePath=cue.CuePath,sourceCueSha256=cue.SourceSha256,placement=placement.PlanSha256,resources=resources.GraphFingerprint,
            resources.FinalDataSha256,overlay.OverlaySha256,reward.ExecutableSha256,WadSha256,RootSha256,Entries,
            GrowthBytes,RelocatedExecutableLba,Memory,RuntimeVerified,NormalCreateBinApproved}));
    }
    public const string Profile="native-dark-wizard-dry-corridor-research-v1";
    public const int TargetLevelId=12;
    internal const string ImageName="dark-hollow-native-wizard-r01.bin",CueName="dark-hollow-native-wizard-r01.cue",ReceiptName="native-wizard.receipt.json";
    internal static string[] OutputNames=>[ImageName,ReceiptName,CueName];
    public string SourceImagePath {get;}
    public string PlanSha256 {get;}
    public string OriginalWadSha256 {get;}
    public string WadSha256 {get;}
    public string RootSha256 {get;}
    public int GrowthBytes=>_allocation.WadGrowthBytes;
    public int RelocatedExecutableLba=>_allocation.RelocatedExecutableLba;
    public int FinalExecutableEndLba=>checked(RelocatedExecutableLba+_executable.Length/0x800);
    public NativeDarkHollowWizardPlacementPlan Placement {get;}
    public NativeDarkHollowWizardResourcePlan Resources {get;}
    public NativeDarkHollowWizardOverlayPlan Overlay {get;}
    public NativeDarkHollowWizardDeathRewardPlan Reward {get;}
    public NativeLevelRuntimeMemoryReport Memory=>Overlay.RuntimeMemory;
    public IReadOnlyList<NativeWizardResearchEntry> Entries {get;}
    public bool RuntimeVerified=>false;
    public bool NormalCreateBinApproved=>false;
    public bool CompleteNativeClosureProven=>false;
    public bool TestLevelSelectorEnabled=>false;
    internal DiscLayout Layout {get;}
    internal string SourceCuePath=>_cue.CuePath;
    internal byte[] CopyCueBytes()=>_cue.CopyOutputBytes();
    internal byte[] CopyWad()=>_wad.ToArray();
    internal byte[] CopyExecutable()=>_executable.ToArray();
    internal byte[] CopyRoot()=>_root.ToArray();
    public static async Task<NativeDarkHollowWizardResearchCandidatePlan> CaptureAsync(string originalImagePath,string originalCuePath,
        LevelCatalog catalog,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(catalog);string path=Path.GetFullPath(originalImagePath);
        var cue=NativeFreeFlightResearchSourceCue.Capture(originalCuePath,path,ImageName);
        var inventory=NativeMobySourceInventory.Capture(path,catalog);
        var scene=NativeDarkHollowWizardScenePlan.Capture(path);
        var placement=NativeDarkHollowWizardPlacementSource.Capture(path,scene).Compile(new(92832,25248,10445,91808,25248));
        var playerSource=await NativePlayerAnimationSource.CaptureAsync(path,token);
        var player=await playerSource.CompileMergeAsync(path,33,12,[8],token);
        var soundSource=await NativeGlobalSoundSource.CaptureAsync(path,token);
        var sound=await soundSource.CompileModelSlotMergeAsync(path,inventory,33,12,0x11B,[0,1,2],
            [new(0x1F,NativeGlobalSoundMappingPolicy.ExplicitRebindUnresolvedConsumers),new(0x26)],token);
        var texture=NativeDarkHollowWizardTexturePlan.Capture(path);
        var descriptor=await NativeDarkHollowWizardParticlePlan.CaptureAsync(path,texture,token);
        var resources=await NativeDarkHollowWizardResourcePlan.CompileWithPlacementAsync(path,inventory,player,sound,texture,descriptor,scene,placement,token);
        var behavior=NativeDarkHollowWizardBehaviorSource.Capture(path);var particles=NativeDarkHollowWizardParticleBehaviorSource.Capture(path);
        var overlay=NativeDarkHollowWizardOverlayPlan.Compile(behavior,resources,particles);
        var reward=NativeDarkHollowWizardDeathRewardPlan.Capture(path,overlay);
        if(!resources.IncludesAuthoredPlacement||resources.PlacementPlanSha256!=placement.PlanSha256||
            !overlay.AllCodeTargetsMaterialized||!overlay.ActiveDispatchEncoded||!overlay.RuntimeMemory.KnownPostLoadExtentsDisjoint||
            overlay.RuntimeMemory.DynamicMobyCapacity!=90||overlay.RuntimeMemory.NormalReservedBufferGapBytes!=0x441CC)
            throw new InvalidDataException("Research canonical stage composition lost required source/placement/code/native extent conditions.");
        using FileStream image=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        var layout=DiscImage.DetectLayout(path);
        byte[] header=DiscImage.ReadFileBytes(image,layout,37,0,0x800),originalData=scene.CopyOriginalLevel();
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
        var plan=new NativeDarkHollowWizardResearchCandidatePlan(path,cue,layout,placement,resources,overlay,reward,wad,root,header,originalData,
            allocation,preparation,Flags(53874),Flags(54078));
        await plan.RequireFreshAsync(token);return plan;
    }
    public async Task RequireFreshAsync(CancellationToken token=default)
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
        var data=Resources.CopyFinalLevelData();Resources.RequireFinal(data);
        Overlay.RequireFinal(Overlay.CopyOverlay(),Overlay.CopyExecutable(),data);
        Reward.RequireExecutable(_executable);
        if(!Reward.RevertToOverlayExecutable(_executable).AsSpan().SequenceEqual(Overlay.CopyExecutable()))
            throw new InvalidDataException("Research reward did not revert exactly to source-issued overlay EXE.");
        Placement.RequireOriginalAndFinal(Placement.CopyBaseScene(),Placement.CopyScene());
        NativeDarkHollowWizardDeathRewardPlan.RequireSingleRewardWizard(data);
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
    internal byte[] BuildReceipt((string Hash,int Changed) verified)=>JsonSerializer.SerializeToUtf8Bytes(new
    {
        profile=Profile,PlanSha256,SourceImagePath,sourceImageSha256=NativeMobySourceInventory.OriginalImageSha256,
        sourceCuePath=SourceCuePath,sourceCueSha256=_cue.SourceSha256,outputImageSha256=verified.Hash,changedRawSectors=verified.Changed,
        outputCueSha256=Hash(CopyCueBytes()),OriginalWadSha256,WadSha256,RootSha256,GrowthBytes,RelocatedExecutableLba,
        FinalExecutableEndLba,Entries,placement=Placement,resourcePlan=Resources.GraphFingerprint,Resources.FinalDataSha256,
        overlaySha256=Overlay.OverlaySha256,executableSha256=Reward.ExecutableSha256,rewardSelector=Reward.RewardSelector,
        nativeRewardValue=Reward.NativeGemValue,treasureTarget=Reward.TreasureTargetAfter,Memory,
        wholeImagePhysicalReadback=true,completeOriginalWadInverseVerified=true,original128RowsPreserved=true,
        researchOnly=true,TestLevelSelectorEnabled,CompleteNativeClosureProven,RuntimeVerified,NormalCreateBinApproved,
        exceptionRollbackPublication=true,crashAtomicPublication=false,hostileFilesystemRaceSafe=false,
        pending=new[]{"full main-helper global/GTE and transient stack behavior","native body/route/bolt trajectory",
            "cold boot and normal portal entry","native patrol and intact lightning/hurt/effects/sound","native death, one10-value gem, +10 collection and persistence",
            "nearby native actors/collision/exit/death/re-entry","ordinary editor export approval"}
    },new JsonSerializerOptions{WriteIndented=true});
    internal static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Both(byte[] bytes,int offset,int value)
    {BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset),value);BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset+4),value);}
}
