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
    private readonly NativeWizardResearchArtifactPlan _artifacts;
    private NativeDarkHollowWizardResearchCandidatePlan(string source,NativeFreeFlightResearchSourceCue cue,
        NativeDarkHollowWizardPlacementPlan placement,NativeDarkHollowWizardResourcePlan resources,
        NativeDarkHollowWizardOverlayPlan overlay,NativeWizardResearchArtifactPlan artifacts)
    {
        SourceImagePath=source;_artifacts=artifacts;Layout=artifacts.Layout;Placement=placement;Resources=resources;Overlay=overlay;Reward=artifacts.Reward;
        Entries=artifacts.Entries;OriginalWadSha256=artifacts.OriginalWadSha256;WadSha256=artifacts.WadSha256;RootSha256=artifacts.RootSha256;
        var reward=Reward;
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
    public int GrowthBytes=>_artifacts.GrowthBytes;
    public int RelocatedExecutableLba=>_artifacts.RelocatedExecutableLba;
    public int FinalExecutableEndLba=>_artifacts.FinalExecutableEndLba;
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
    internal string SourceCuePath=>_artifacts.SourceCuePath;
    internal byte[] CopyCueBytes()=>_artifacts.CopyCueBytes();
    internal byte[] CopyWad()=>_artifacts.CopyWad();
    internal byte[] CopyExecutable()=>_artifacts.CopyExecutable();
    internal byte[] CopyRoot()=>_artifacts.CopyRoot();
    public static async Task<NativeDarkHollowWizardResearchCandidatePlan> CaptureAsync(string originalImagePath,string originalCuePath,
        LevelCatalog catalog,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(catalog);string path=Path.GetFullPath(originalImagePath);
        var cue=NativeFreeFlightResearchSourceCue.Capture(originalCuePath,path,ImageName);
        var dependencies=await NativeWizardResearchArtifactPlan.CaptureDependenciesAsync(path,catalog,token);
        var scene=NativeDarkHollowWizardScenePlan.Capture(path);
        var placement=NativeDarkHollowWizardPlacementSource.Capture(path,scene).Compile(new(92832,25248,10445,91808,25248));
        var resources=await NativeDarkHollowWizardResourcePlan.CompileWithPlacementAsync(path,dependencies.Inventory,dependencies.Player,
            dependencies.Sound,dependencies.Texture,dependencies.Descriptor,scene,placement,token);
        var overlay=NativeDarkHollowWizardOverlayPlan.Compile(dependencies.Behavior,resources,dependencies.Particles);
        if(!resources.IncludesAuthoredPlacement||resources.PlacementPlanSha256!=placement.PlanSha256||
            !overlay.AllCodeTargetsMaterialized||!overlay.ActiveDispatchEncoded||!overlay.RuntimeMemory.KnownPostLoadExtentsDisjoint||
            overlay.RuntimeMemory.DynamicMobyCapacity!=90||overlay.RuntimeMemory.NormalReservedBufferGapBytes!=0x441CC)
            throw new InvalidDataException("Research canonical stage composition lost required source/placement/code/native extent conditions.");
        var artifacts=await NativeWizardResearchArtifactPlan.CaptureAsync(path,cue,resources,overlay,token);
        return new(path,cue,placement,resources,overlay,artifacts);
    }
    public Task RequireFreshAsync(CancellationToken token=default)=>_artifacts.RequireFreshAsync(token);
    internal void RequireStageReversion()
    {
        Placement.RequireOriginalAndFinal(Placement.CopyBaseScene(),Placement.CopyScene());
        NativeDarkHollowWizardDeathRewardPlan.RequireSingleRewardWizard(Resources.CopyFinalLevelData());
        _artifacts.RequireStageReversion();
    }
    internal void WriteNewImage(string outputPath,CancellationToken token=default)=>_artifacts.WriteNewImage(outputPath,token);
    internal (string Hash,int Changed) VerifyImage(string path)=>_artifacts.VerifyImage(path);
    internal byte[] ExpectedSector(int lba,byte[] before)=>_artifacts.ExpectedSector(lba,before);
    internal byte[] BuildReceipt((string Hash,int Changed) verified)=>JsonSerializer.SerializeToUtf8Bytes(new
    {
        profile=Profile,PlanSha256,SourceImagePath,sourceImageSha256=NativeMobySourceInventory.OriginalImageSha256,
        sourceCuePath=SourceCuePath,sourceCueSha256=_artifacts.SourceCueSha256,outputImageSha256=verified.Hash,changedRawSectors=verified.Changed,
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
