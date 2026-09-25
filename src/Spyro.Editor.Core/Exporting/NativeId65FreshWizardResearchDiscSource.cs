using System.Runtime.CompilerServices;
using Spyro.Editor.Core.Analysis;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshWizardDiscSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Actual-EXE source capture with one retained original per-LBA index and a newly validated larger gap.</summary>
internal sealed class NativeId65FreshWizardResearchDiscSource
{
    private readonly NativeId65FreshDiscSource _original;
    private readonly NativeFreshResearchPhysicalSourceOwners _owners;
    private NativeId65FreshWizardResearchDiscSource(NativeId65FreshDiscSource original,
        NativeId65FreshWizardResearchExecutablePlan executable,NativeFreshResearchPhysicalSourceOwners owners)
    {
        if(executable.FamilyCompositionAncestor is { } family)
        {
            RequireFamilyExecutable(executable);FamilyCompositionAncestor=family;MixedResourceAncestor=executable.MixedResourceAncestor;
            Geometry=NativeId65FreshWizardDiscGeometry.CompileFamily(family);
        }
        else if(executable.MixedResourceAncestor is { } mixed)
        {
            RequireMaterialExecutable(executable);MixedResourceAncestor=mixed;
            Geometry=NativeId65FreshWizardDiscGeometry.Compile(executable.BodyAncestor.CopyOverlay().Length,executable.BodyAncestor.CopyLevelData().Length);
        }
        else
        {
            RequireExecutable(executable);
            Geometry=NativeId65FreshWizardDiscGeometry.Compile(executable.BodyAncestor.CopyOverlay().Length,executable.BodyAncestor.CopyLevelData().Length);
        }
        _original=original;Executable=executable;_owners=owners;
    }
    internal NativeId65FreshWizardResearchExecutablePlan Executable{get;}
    internal NativeFreshResearchPhysicalSourceOwners PhysicalOwners=>_owners;
    internal NativeFreshMobyFamilyCompositionPlan? FamilyCompositionAncestor{get;}
    internal NativeId65FreshWizardDiscGeometry Geometry{get;}
    internal NativeFreshWizardMaterialResource? MixedResourceAncestor{get;}
    internal IReadOnlyList<NativeFlatIsoFile> RootFiles=>_original.RootFiles;
    internal byte[] CopyRoot()=>_owners.CopyRoot();
    internal byte[] CopyWadHeader()=>_owners.CopyWadHeader();
    internal byte[] CopyPreimage(int lba)=>_owners.CopyPreimage(lba);
    internal void RequireRawSector(int lba,ReadOnlySpan<byte> bytes)=>_original.RequireRawSector(lba,bytes);
    internal void RequireAncestry()
    {
        if(FamilyCompositionAncestor is { } family)
        {
            if(!ReferenceEquals(family,Executable.FamilyCompositionAncestor)||!ReferenceEquals(MixedResourceAncestor,Executable.MixedResourceAncestor))
                throw new InvalidDataException("Disc lost its retained actual executable/family/mixed source chain.");
            RequireFamilyExecutable(Executable);
        }
        else if(MixedResourceAncestor is { } mixed)
        {
            if(!ReferenceEquals(mixed,Executable.MixedResourceAncestor))
                throw new InvalidDataException("Disc lost its retained actual executable/mixed source pair.");
            RequireMaterialExecutable(Executable);
        }
        else RequireExecutable(Executable);
    }
    internal void RequireRetainedOriginalOwners()
    {
        if(!Executable.RevertExact(Executable.CopyExecutable()).SequenceEqual(_original.CopyExecutable()))
            throw new InvalidDataException("Retained Wizard physical source owners changed.");
        _owners.RequireRetainedOriginalOwners(_original,Geometry);
    }
    internal static void RequireExecutable(NativeId65FreshWizardResearchExecutablePlan executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        if(executable.FamilyCompositionAncestor is not null)
            throw new InvalidDataException("Family-composed executable requires its separate actual-family Disc arm.");
        if(executable.IsMaterialAuthored)
            throw new InvalidDataException("Material-authored Wizard executable disc composition is not enabled.");
        var body=executable.BodyAncestor;
        executable.RequireAncestry(body);executable.RequireExactExecutable(executable.CopyExecutable());
        NativeId65FreshWizardResearchExecutableSource.RequireBody(body);
        var g=NativeId65FreshWizardDiscGeometry.Compile(body.CopyOverlay().Length,body.CopyLevelData().Length);
        if(executable.CopyExecutable().Length!=0x66000||body.CopyBufferAddress!=0x8007AA38u+g.OverlayByteLength||
            body.ModelRuntimeAddress!=body.CopyBufferAddress+0x8FC||body.SceneRuntimeAddress!=body.ModelRuntimeAddress+0x19000||
            body.KnownSceneEndAddress!=body.SceneRuntimeAddress+0x3000||body.KnownSceneEndAddress>body.NormalReservedLowerAddress)
            throw new InvalidDataException("Wizard disc requires the complete actual fixed EXE/Body/Host layout.");
    }
    internal static void RequireMaterialExecutable(NativeId65FreshWizardResearchExecutablePlan executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        if(executable.FamilyCompositionAncestor is not null)
            throw new InvalidDataException("Family-composed executable requires its separate actual-family Disc arm.");
        var mixed=executable.MixedResourceAncestor;
        if(mixed is null)
            throw new InvalidDataException("Material-authored disc requires its actual mixed executable.");
        var body=executable.BodyAncestor;
        executable.RequireAncestry(body);body.RequireMixedAncestry(mixed);
        NativeId65FreshWizardResearchExecutableSource.RequireMaterialBody(body);
        byte[] exe=executable.CopyExecutable();executable.RequireExactExecutable(exe);
        var geometry=NativeId65FreshWizardDiscGeometry.Compile(body.CopyOverlay().Length,body.CopyLevelData().Length);
        int used=mixed.CopyDataComponents().Length;
        if(exe.Length!=0x66000||used!=0xA6C||body.CopyBufferAddress!=0x8007AA38u+geometry.OverlayByteLength||
            body.CopyBufferAddress!=0x8008BA38||body.ModelRuntimeAddress!=body.CopyBufferAddress+used||
            body.ModelRuntimeAddress!=0x8008C4A4||body.SceneRuntimeAddress!=body.ModelRuntimeAddress+0x19000||
            body.KnownSceneEndAddress!=body.SceneRuntimeAddress+0x3000||body.NormalReservedLowerAddress!=0x80187BB0||
            body.KnownSceneEndAddress>body.NormalReservedLowerAddress||body.ResidentPeteAddress!=0x801C5BB8)
            throw new InvalidDataException("Material-authored disc requires its actual A6C runtime relation and unchanged physical allocation.");
    }
    internal static void RequireFamilyExecutable(NativeId65FreshWizardResearchExecutablePlan executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        var family=executable.FamilyCompositionAncestor??
            throw new InvalidDataException("Family Disc requires its actual family-composed executable.");
        executable.RequireFamilyAncestry(family);
        var body=family.BaseBodyAncestor;var geometry=NativeId65FreshWizardDiscGeometry.CompileFamily(family);
        if(executable.CopyExecutable().Length!=0x66000||body.CopyOverlay().Length!=0x11000||
            body.CopyBufferAddress!=0x8007AA38u+geometry.OverlayByteLength||body.CopyBufferAddress!=0x8008BA38||
            family.ModelRuntimeAddress!=body.CopyBufferAddress+0xA6C||family.ModelRuntimeAddress!=0x8008C4A4||
            family.SceneRuntimeAddress!=family.ModelRuntimeAddress+0x19800||family.KnownSceneEndAddress!=family.SceneRuntimeAddress+0x3800||
            body.NormalReservedLowerAddress!=0x80187BB0||body.ResidentPeteAddress!=0x801C5BB8||
            family.KnownSceneEndAddress>body.NormalReservedLowerAddress||executable.FamilyCensus?.StaticTreasureTarget!=10)
            throw new InvalidDataException("Family Disc requires actual source-bound P2 growth, static census and retained Host/PETE bounds.");
    }
    internal static async Task<NativeId65FreshWizardResearchDiscSource> CaptureFamilyComposedAsync(Stream image,
        NativeId65FreshWizardResearchExecutablePlan executable,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(image);RequireFamilyExecutable(executable);
        return await CaptureOwnersAsync(image,executable,null,token);
    }
    internal static async Task<NativeId65FreshWizardResearchDiscSource> CaptureMaterialAuthoredAsync(Stream image,
        NativeId65FreshWizardResearchExecutablePlan executable,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(image);RequireMaterialExecutable(executable);
        // The new path's acquired-owner finalization belongs to the Plan. It
        // must not inherit the old finally's primary-masking error path.
        return await CaptureOwnersAsync(image,executable,null,token);
    }
    internal static Task<NativeId65FreshWizardResearchDiscSource> CaptureAsync(Stream image,
        NativeId65FreshWizardResearchExecutablePlan executable,CancellationToken token)=>CaptureCoreAsync(image,executable,null,token);
    // Only a mutation trigger after the actual old source/index capture. It
    // cannot submit an index, bytes, metadata, hashes or an issued source object.
    internal static Task<NativeId65FreshWizardResearchDiscSource> CaptureForSmokeAsync(Stream image,
        NativeId65FreshWizardResearchExecutablePlan executable,Action? afterOriginalCapture,CancellationToken token)
        =>CaptureCoreAsync(image,executable,afterOriginalCapture,token);
    private static async Task<NativeId65FreshWizardResearchDiscSource> CaptureCoreAsync(Stream image,
        NativeId65FreshWizardResearchExecutablePlan executable,Action? afterOriginalCapture,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(image);RequireExecutable(executable);
        await NativeId65FreshDiscSource.RequireWholeOriginalAsync(image,token);
        try
        {
            return await CaptureOwnersAsync(image,executable,afterOriginalCapture,token);
        }
        finally
        {
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(image,CancellationToken.None);
            token.ThrowIfCancellationRequested();
        }
    }
    // Exact shared physical reads and original-index capture. Arm validation
    // and acquired stream ownership remain with their distinct entry points.
    private static async Task<NativeId65FreshWizardResearchDiscSource> CaptureOwnersAsync(Stream image,
        NativeId65FreshWizardResearchExecutablePlan executable,Action? afterOriginalCapture,CancellationToken token)
    {
            var original=await NativeId65FreshDiscSource.CaptureAsync(image,token);
            if(!executable.RevertExact(executable.CopyExecutable()).SequenceEqual(original.CopyExecutable()))
                throw new InvalidDataException("Actual retained EXE is not bound to this authenticated original executable.");
            afterOriginalCapture?.Invoke();token.ThrowIfCancellationRequested();
            var geometry=executable.FamilyCompositionAncestor is { } family?NativeId65FreshWizardDiscGeometry.CompileFamily(family):
                NativeId65FreshWizardDiscGeometry.Compile(0x11000,0x20C800);
            var owners=NativeFreshResearchPhysicalSourceOwners.Capture(image,original,geometry,token);
            token.ThrowIfCancellationRequested();return new(original,executable,owners);
    }
}

/// <summary>Closed-arm arithmetic only; not source, payload or gap ownership.</summary>
internal sealed class NativeId65FreshWizardDiscGeometry
{
    private NativeId65FreshWizardDiscGeometry(int bodyLength,int overlayLength=0x11000)
    {BodyByteLength=bodyLength;OverlayByteLength=overlayLength;}
    internal bool SourceCertified=>false;
    internal int OverlayWadOffset=>0x6927000;
    internal int OverlayByteLength{get;}
    internal int BodyWadOffset=>checked(OverlayWadOffset+OverlayByteLength);
    internal int BodyLba=>checked(37+BodyWadOffset/2048);
    internal int BodyByteLength{get;}
    internal int GrowthByteLength=>checked(OverlayByteLength+BodyByteLength);
    internal int FinalWadByteLength=>checked(OverlayWadOffset+GrowthByteLength);
    internal int FinalExecutableLba=>checked(53875+GrowthByteLength/2048);
    internal int FinalExecutableEndExclusive=>checked(FinalExecutableLba+204);
    internal int ChangedSectorOwnerCount=>checked(2+FinalExecutableEndExclusive-53874);
    internal static NativeId65FreshWizardDiscGeometry Compile(int overlay,int body)
    {
        if(overlay!=0x11000||body!=0x20C800)throw new InvalidDataException("Only the current actual Wizard Host11000/Body20C800 allocation is supported.");
        return new(body);
    }
    internal static NativeId65FreshWizardDiscGeometry CompileFamily(NativeFreshMobyFamilyCompositionPlan family)
    {
        NativeId65FreshWizardResearchExecutableSource.RequireFamilyComposition(family);
        var result=new NativeId65FreshWizardDiscGeometry(family.CopyLevelData().Length);
        if(result.BodyByteLength%2048!=0||result.FinalExecutableEndExclusive>60000)
            throw new InvalidDataException("Actual family body is not sector aligned or exceeds the separately checked original gap.");
        return result;
    }
    internal static NativeId65FreshWizardDiscGeometry CompileChest(NativeFreshLockedChestExperimentalExecutable executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        var placement=executable.PlacementAncestor;var host=executable.HostAncestor;
        executable.RequireAncestry(placement,host);executable.RequireExactExecutable(executable.CopyExecutable());
        var body=executable.BodyAncestor;var memory=body.RuntimeMemory;
        byte[] level=placement.CopyLevelData(),overlay=host.CopyOverlay();
        placement.RequireExactLevelData(level);
        host.RequireExactOutputs(overlay,host.CopyInitializer(),host.CopyExecutablePrototype());
        if(!ReferenceEquals(body,placement.BodyAncestor)||!ReferenceEquals(host,body.HostAncestor)||
            overlay.Length!=0x12000||level.Length!=0x20F000||executable.CopyExecutable().Length!=0x66000||
            host.AppendLayout.CopyAnchor!=0x8008CA38||memory.CopyBufferAddress!=0x8008CA38||
            memory.UsedLevelDataBytes!=0xA6C||memory.ModelBytesRead!=0x1B000||memory.SceneBytesRead!=0x3800||
            memory.ModelRamStart!=0x8008D4A4||memory.SceneRamStart!=0x800A84A4||memory.SceneReadEnd!=0x800ABCA4||
            memory.PlacedMobyCount!=9||memory.DynamicMobyCapacity!=32||memory.NormalReservedBufferStart!=0x80187BB0||
            body.ResourceAncestor.BaseBodyAncestor.ResidentPeteAddress!=0x801C5BB8||
            !memory.KnownPostLoadExtentsDisjoint||memory.SceneReadEnd>memory.NormalReservedBufferStart)
            throw new InvalidDataException("Chest physical geometry requires the actual placed Body, extended Host and bounded normal-load relation.");
        var result=new NativeId65FreshWizardDiscGeometry(level.Length,overlay.Length);
        if(result.BodyByteLength%2048!=0||result.OverlayByteLength%2048!=0||result.FinalExecutableEndExclusive>60000)
            throw new InvalidDataException("Actual chest allocations are not sector aligned or exceed the separately checked Original gap.");
        return result;
    }
    internal static NativeId65FreshWizardDiscGeometry CompileTiledFamily(NativeFreshTiledFamilyExecutablePlan executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        var body=executable.BodyAncestor;var host=executable.HostAncestor;
        executable.RequireAncestry(body,executable.ParentExecutableAncestor);
        byte[] level=executable.CopyLevelData(),exe=executable.CopyExecutable(),overlay=host.CopyOverlay();
        executable.RequireExactOutputs(level,exe);
        host.RequireExactOutputs(overlay,host.CopyInitializer(),host.CopyExecutablePrototype());
        // Decode the new complete Body here. Captured source-sector ownership
        // and equal physical lengths cannot carry the parent's old A6C proof.
        var memory=NativeLevelRuntimeMemoryReport.DecodeFixture(65,level,host.AppendLayout.CopyAnchor);
        if(!ReferenceEquals(host,body.HostAncestor)||!ReferenceEquals(body.PlacementAncestor,executable.ParentExecutableAncestor.PlacementAncestor)||
            !executable.SourceCertified||!executable.Experimental||executable.FullNativeLoaderProven||
            executable.KeyZeroContributionProven||executable.CompleteStaticCensusProven||executable.RuntimeDropGuarantee||
            executable.CompleteNativeDependencyClosureProven||executable.PeakRuntimeMemoryProven||executable.RuntimeVerified||executable.PublicationApproved||
            overlay.Length!=0x12000||level.Length!=0x20F000||exe.Length!=0x66000||
            memory.AnalyzedDataSha256!=executable.LevelDataSha256||host.AppendLayout.CopyAnchor!=0x8008CA38||
            memory.CopyBufferAddress!=0x8008CA38||memory.UsedLevelDataBytes!=0xCB8||memory.LevelDataSectorPaddingBytes!=0x348||
            memory.ModelBytesRead!=0x1B000||memory.ModelRamStart!=0x8008D6F0||memory.SceneBytesRead!=0x3800||
            memory.SceneRamStart!=0x800A86F0||memory.SceneReadEnd!=0x800ABEF0||
            memory.ParticleComponentDataOffset!=0xE650C||memory.SoundComponentDataOffset!=0xE6598||
            memory.PlacedMobyCount!=9||memory.DynamicMobyCapacity!=32||memory.NormalReservedBufferStart!=0x80187BB0||
            body.ResourceAncestor.BaseBodyAncestor.ResidentPeteAddress!=0x801C5BB8||
            !memory.KnownPostLoadExtentsDisjoint||memory.SceneReadEnd>memory.NormalReservedBufferStart||
            memory.NativeSourceCertified||memory.RuntimeFitProven||memory.PeakTransientFitProven)
            throw new InvalidDataException("Tiled physical geometry requires the actual CB8 Body/extended Host relation without inherited loading or runtime authority.");
        var result=new NativeId65FreshWizardDiscGeometry(level.Length,overlay.Length);
        if(result.BodyByteLength%2048!=0||result.OverlayByteLength%2048!=0||result.FinalExecutableEndExclusive>60000)
            throw new InvalidDataException("Actual tiled family allocations are not sector aligned or exceed the separately captured Original gap.");
        return result;
    }
    internal NativeSkyWadRelocationPlan CreateGrowthGeometry()=>new(true,37,0x6927000,FinalWadByteLength,GrowthByteLength,
        53875,FinalExecutableLba,0x66000,60000,checked((60000-54079)*2048),new Dictionary<int,int>(),new Dictionary<int,long>(),Array.Empty<NativeSkyRelocatedPatch>());
}
