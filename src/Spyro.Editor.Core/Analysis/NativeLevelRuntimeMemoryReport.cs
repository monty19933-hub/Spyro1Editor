using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Native normal-load address accounting, not a peak RAM or publication certificate.
/// Model and scene extents include all bytes requested by the CD loader, while the
/// level-data relocator returns its used component end, excluding sector padding.
/// The reserved Moby arena is shared dynamic row/properties storage, not free space.
/// </summary>
public sealed class NativeLevelRuntimeMemoryReport
{
    private const string TownOriginalHash="7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    private NativeLevelRuntimeMemoryReport(int levelId,ReadOnlySpan<byte> data,uint copyBuffer,
        uint normalLowerBound,string originalHash,bool native,string binding)
    {
        if(copyBuffer<0x80010000||copyBuffer>=0x80200000||copyBuffer%4!=0||normalLowerBound<=copyBuffer||normalLowerBound>0x80200000)
            throw new InvalidDataException("Native copy-buffer binding is outside aligned main RAM.");
        // Reuse the complete packed descriptor and root-0/model-owner validation;
        // this does not reinterpret resident model padding as reclaimable storage.
        var domain=new NativeAddressSpace(NativeAddressKind.LevelData,"ram-report",NativeAddressRevision.Original,data.Length);
        _=NativeMobyModelRegistrationSource.DecodeFixture(domain,data);
        int start=Integer(data,8),ownerLength=Integer(data,12),ownerEnd=checked(start+ownerLength);
        int cursor=start;
        for(int i=0;i<6;i++)cursor=checked(cursor+Component(data,cursor,ownerEnd,4));
        Range(cursor,4,ownerEnd);int portals=Integer(data,cursor);cursor+=4;
        if(portals>(ownerEnd-cursor)/0x38)throw new InvalidDataException("Native portal count exceeds its data owner.");
        for(int i=0;i<portals;i++)
        {
            Range(cursor,0x34,ownerEnd);int points=Integer(data,cursor+4);
            if(points<1||points>(ownerEnd-cursor-0x34)/12)throw new InvalidDataException("Native portal point list is empty or exceeds its owner.");
            int body=checked(cursor+0x34+12*points);
            cursor=checked(body+Component(data,body,ownerEnd,4));
        }
        int particle=cursor;cursor=checked(cursor+Component(data,cursor,ownerEnd,4));
        int sound=cursor;cursor=checked(cursor+Component(data,cursor,ownerEnd,4));
        if(data.Slice(cursor,ownerEnd-cursor).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Native used-data tail contains nonzero unexplained sector padding.");
        int sceneOffset=Integer(data,0x18),sceneLength=Integer(data,0x1C);
        int sceneEnd=checked(sceneOffset+sceneLength),moby=checked(sceneOffset+0x88);
        for(int i=0;i<7;i++)moby=checked(moby+Component(data,moby,sceneEnd,8));
        int mobyLength=Component(data,moby,sceneEnd,8),placed=Integer(data,moby+4);
        if(placed>4096||8L+placed*88L>mobyLength)
            throw new InvalidDataException("Native placed rows exceed their Moby component.");
        LevelId=levelId;SourceDataSha256=originalHash;AnalyzedDataSha256=Hash(data);
        NativeSourceCertified=native;CopyBufferBinding=binding;CopyBufferAddress=copyBuffer;
        NormalReservedBufferStart=normalLowerBound;UsedLevelDataBytes=cursor-start;
        LevelDataSectorPaddingBytes=ownerEnd-cursor;PortalCount=portals;
        ParticleComponentDataOffset=particle;SoundComponentDataOffset=sound;
        ModelBytesRead=Integer(data,0x14);SceneBytesRead=sceneLength;
        ModelRamStart=checked(copyBuffer+(uint)UsedLevelDataBytes);
        SceneRamStart=checked(ModelRamStart+(uint)ModelBytesRead);
        SceneReadEnd=checked(SceneRamStart+(uint)SceneBytesRead);
        if(SceneReadEnd>0x80200000)throw new InvalidDataException("Native level CD-read extents exceed main RAM.");
        NormalReservedBufferGapBytes=(long)normalLowerBound-SceneReadEnd;
        PlacedMobyCount=placed;DynamicArenaBytes=mobyLength-8-placed*88;
        // Native allocator 800524C4 advances rows by 0x58 and properties down by
        // 0x18; scene loader 80013F24..13FC0 initializes this same shared arena.
        DynamicMobyCapacity=DynamicArenaBytes/(0x58+0x18);
        MobyTableRamStart=checked(SceneRamStart+(uint)(moby-sceneOffset+8));
    }
    public int LevelId { get; }
    public string SourceDataSha256 { get; }
    public string AnalyzedDataSha256 { get; }
    public bool NativeSourceCertified { get; }
    public string CopyBufferBinding { get; }
    public uint CopyBufferAddress { get; }
    public int UsedLevelDataBytes { get; }
    public int LevelDataSectorPaddingBytes { get; }
    public int PortalCount { get; }
    public int ParticleComponentDataOffset { get; }
    public int SoundComponentDataOffset { get; }
    public int ModelBytesRead { get; }
    public int SceneBytesRead { get; }
    public uint ModelRamStart { get; }
    public uint SceneRamStart { get; }
    public uint SceneReadEnd { get; }
    public uint NormalReservedBufferStart { get; }
    public long NormalReservedBufferGapBytes { get; }
    public bool KnownPostLoadExtentsDisjoint=>NormalReservedBufferGapBytes>=0;
    public int PlacedMobyCount { get; }
    public int DynamicArenaBytes { get; }
    public int DynamicMobyCapacity { get; }
    public uint MobyTableRamStart { get; }
    public bool RuntimeFitProven=>false;
    public bool PeakTransientFitProven=>false;
    public bool PublicationApproved=>false;
    public string Exclusions=>"Normal-load resident address accounting only. Does not certify transient CD staging, level-transition " +
        "and alternate buffer modes, stack/scratch/renderer peak usage, dynamic live actor/particle occupancy, VRAM, SPU, " +
        "behavior dependencies or gameplay. Positive remaining capacity is not permission to consume it.";

    public static NativeLevelRuntimeMemoryReport CaptureOriginal(NativeMobySourceLevel level,ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> executable,ReadOnlySpan<byte> peteHeader,ReadOnlySpan<byte> peteRuntimeCopy)
    {
        ArgumentNullException.ThrowIfNull(level);
        if(data.Length!=level.SourceDataByteLength||Hash(data)!=level.SourceDataSha256)
            throw new InvalidDataException("Native RAM report data differs from its complete source inventory certificate.");
        _=NativeMobyModelLoaderCertificate.CaptureRetail(executable,peteHeader,peteRuntimeCopy);
        uint pc=Word(executable,checked(0x1BA4+4*level.LevelId));
        int entry=checked((int)(pc-0x8000F800));
        uint copy=DecodeCopyBuffer(executable,entry);
        // Exact executable/PETE certificates bind both terms, not an unexplained
        // fixed 'maximum level size'. Native 8005B8B8 selects normal mode a0=0.
        uint pete=checked(0x80200000-Word(executable,0x65DA4)-Word(peteHeader,0));
        uint lower=checked(pete-0x6008-2*0x1C000);
        return new(level.LevelId,data,copy,lower,level.SourceDataSha256,true,
            $"retail EXE case {pc:X8}; g785D8 to g785DC; PETE normal buffer mode");
    }

    public NativeLevelRuntimeMemoryReport AnalyzePlayerMerge(NativePlayerAnimationMergePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if(!NativeSourceCertified||AnalyzedDataSha256!=SourceDataSha256||plan.TargetLevelId!=LevelId||
            plan.SourceImageSha256!=NativeMobySourceInventory.OriginalImageSha256||plan.OriginalDataSha256!=SourceDataSha256)
            throw new InvalidDataException("Player merge does not own this original native RAM report source.");
        byte[] data=plan.CopyFinalLevelData();
        if(data.Length!=plan.FinalDataByteLength||Hash(data)!=plan.FinalDataSha256)
            throw new InvalidDataException("Player merge final source binding changed.");
        return new(LevelId,data,CopyBufferAddress,NormalReservedBufferStart,SourceDataSha256,true,
            CopyBufferBinding+"; preserved by source-bound player merge "+plan.PlanSha256);
    }

    public NativeLevelRuntimeMemoryReport AnalyzeResourceComposition(NativeDarkHollowWizardResourcePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if(!NativeSourceCertified||AnalyzedDataSha256!=SourceDataSha256||plan.TargetLevelId!=LevelId||
            plan.SourceImageSha256!=NativeMobySourceInventory.OriginalImageSha256||plan.SourceDataSha256!=SourceDataSha256)
            throw new InvalidDataException("Resource composition does not own this original native RAM report source.");
        byte[] data=plan.CopyFinalLevelData();
        if(data.Length!=plan.FinalDataByteLength||Hash(data)!=plan.FinalDataSha256)
            throw new InvalidDataException("Resource composition final source binding changed.");
        plan.RequireFinal(data);
        return new(LevelId,data,CopyBufferAddress,NormalReservedBufferStart,SourceDataSha256,true,
            CopyBufferBinding+"; unchanged EXE with source-issued resource composition "+plan.FinalDataSha256);
    }

    public NativeLevelRuntimeMemoryReport AnalyzeDarkWizardOverlay(NativeDarkHollowWizardOverlayPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        byte[] data=plan.CopyFinalLevelData(),originalExe=plan.CopyOriginalExecutable(),executable=plan.CopyExecutable(),overlay=plan.CopyOverlay();
        if(!NativeSourceCertified||LevelId!=12||Hash(data)!=AnalyzedDataSha256||CopyBufferAddress!=plan.OriginalCopyBufferAddress||
            Hash(originalExe)!="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"||
            DecodeCopyBuffer(originalExe,0x4B048)!=CopyBufferAddress||executable.Length!=originalExe.Length||
            !executable.AsSpan(0,0x4B048).SequenceEqual(originalExe.AsSpan(0,0x4B048))||
            !executable.AsSpan(0x4B050).SequenceEqual(originalExe.AsSpan(0x4B050)))
            throw new InvalidDataException("Native overlay does not own this source-certified resource/copy-buffer report.");
        uint copy=DecodeCopyBuffer(executable,0x4B048);
        if(copy!=plan.FinalCopyBufferAddress||overlay.Length<=0xB000||overlay.Length%0x800!=0||copy!=0x8007AA38L+overlay.Length)
            throw new InvalidDataException("Native overlay copy buffer is not its exact complete final code-owner end.");
        plan.RequireFinal(overlay,executable,data);
        return new(LevelId,data,copy,NormalReservedBufferStart,SourceDataSha256,true,
            CopyBufferBinding+"; source-certified appended Wizard overlay "+plan.OverlaySha256);
    }

    internal static NativeLevelRuntimeMemoryReport AnalyzeTownClosure(NativeTownSquareLockedChestClosure closure)
    {
        // This read-only adapter recognizes the two existing immutable receipts;
        // it does not authorize arbitrary caller-authored copy-buffer patches.
        string expected=closure.Recipe switch
        {
            NativeTownSquareClosureRecipe.PropertiesAndSoundV2=>"e442a02d3aa899dca0d5ddac4392f4b602aabb7ac6dc5dd1e4616c00fab6908d",
            NativeTownSquareClosureRecipe.SpatiallySeparatedV3=>"880c487599e8382447cd7b5c2d1799abec4eafd5af26351f37a22f66ed8ec9b0",
            _=>throw new InvalidDataException("Unrecognized native Town RAM report recipe.")
        };
        if(!closure.ModelRegistration.Loader.IsRetailCertified||Hash(closure.Data)!=expected||Hash(closure.Executable)!="4268f9943233b9f8c24d1d4b0924cba8747547b44b82eddd13f0a2ac10ac17de")
            throw new InvalidDataException("Native Town RAM report requires the exact retained data and executable closure.");
        closure.ModelRegistration.RequireFinal(closure.Data,closure.Graph);
        return new(13,closure.Data,DecodeCopyBuffer(closure.Executable,0x4B098),0x80187BB0,TownOriginalHash,true,
            "exact retained Town V2/V3 EXE copy-buffer patch; unchanged source-certified PETE normal buffers");
    }

    internal static NativeLevelRuntimeMemoryReport DecodeFixture(int id,ReadOnlySpan<byte> data,uint copy)=>
        new(id,data,copy,0x80187BB0,Hash(data),false,"synthetic normal-buffer fixture; no native source certificate");
    private static uint DecodeCopyBuffer(ReadOnlySpan<byte> executable,int offset)
    {
        Range(offset,16,executable.Length);
        uint high=Word(executable,offset),low=Word(executable,offset+4);
        if(high>>16!=0x3C02||low>>16!=0x2442||Word(executable,offset+8)!=0x3C018008||Word(executable,offset+12)!=0xAC2285D8)
            throw new InvalidDataException("Native level copy-buffer initialization instructions changed.");
        int immediate=(int)(low&0xFFFF);if(immediate>=0x8000)immediate-=0x10000;
        return checked((uint)(((long)(high&0xFFFF)<<16)+immediate));
    }
    private static int Component(ReadOnlySpan<byte> data,int offset,int end,int minimum)
    {
        Range(offset,4,end);int length=Integer(data,offset);
        if(length<minimum||length%4!=0||length>(long)end-offset)
            throw new InvalidDataException("Native RAM component exceeds its aligned source owner.");
        return length;
    }
    private static void Range(int o,int n,int end)
    {if(o<0||n<0||o%4!=0||o+(long)n>end)throw new InvalidDataException("Native RAM component is outside its source owner.");}
    private static int Integer(ReadOnlySpan<byte>b,int o)
    {uint v=Word(b,o);return v<=int.MaxValue?(int)v:throw new InvalidDataException("Native RAM component integer exceeds its source owner.");}
    private static uint Word(ReadOnlySpan<byte>b,int o)=>BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);
    private static string Hash(ReadOnlySpan<byte>b)=>NativeMobyModelLoaderCertificate.Hash(b);
}
