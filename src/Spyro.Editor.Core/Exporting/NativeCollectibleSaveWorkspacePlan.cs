using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Source-only explicit nonregistered model-tail insertion into the actual
/// admitted Artisans composition. No caller-provided payload or synthetic
/// composition can issue this plan. Complete old owners and registrations are
/// retained; the whole new descriptor is charged to the normal A00 geometry.
/// The unchanged ancestor guard/callables still contain OLD scene addresses:
/// this is deliberately not a publishable, installed or asynchronously leased
/// composition. Zero initialization does not issue a prepared/Ready state.
/// </summary>
internal sealed class NativeCollectibleSaveWorkspacePlan
{
    internal enum WorkspaceProfile { Preparation, Controller, ControllerScheduler }
    internal const int SchedulerOwnerBytes=0x1000;
    internal const int OrdinaryBytes=0x600, TailReadbackBytes=0x100, ProposedTransactionStateBytes=0x100;
    internal sealed record GeometryMeasurement(uint TailAddress,uint ArenaAddress,int LeadingBytes,int SemanticBytes,
        int GrowthBytes,uint ExpectedOrdinaryAddress,uint OrdinaryReadbackAddress,uint TailReadbackAddress,
        uint TransactionStateAddress,int TrailingBytes,uint EndAddress,long TailGapToFloor);
    internal sealed record MemoryProjection(uint CopyAnchor,int UsedLevelDataBytes,uint ModelStart,
        int OriginalModelBytes,int FinalModelBytes,int SceneBytes,uint OriginalSceneStart,uint FinalSceneStart,
        uint OriginalSceneEnd,uint FinalSceneEnd,uint NormalReservedStart,long OriginalNormalGap,long FinalNormalGap,
        uint TransferPeakEnd,uint OriginalMobyTable,uint FinalMobyTable);
    internal sealed record Owner(string Role,NativeByteExtent Extent,uint Address,bool Mutable,bool Padding,string InitialSha256);
    internal sealed record RowRebinding(string RowId,Guid InstanceId,uint BitIndex,ushort AuthoredClass,uint ModelRoot,
        uint OldRowAddress,uint NewRowAddress,uint OldPrivateAddress,uint NewPrivateAddress);
    internal sealed record GlobalRebinding(uint Address,uint OldValue,uint NewValue);
    internal sealed record SourceSpan(uint Pc,int ByteLength,string Sha256);
    internal sealed class CodeBlock
    {
        private readonly byte[] bytes;
        internal CodeBlock(string role,NativeByteExtent extent,uint address,byte[] value)
        {Role=role;Extent=extent;Address=address;bytes=value.ToArray();Sha256=Hash(bytes);}
        internal string Role{get;}
        internal NativeByteExtent Extent{get;}
        internal uint Address{get;}
        internal string Sha256{get;}
        internal byte[] Bytes=>bytes.ToArray();
    }
    internal static IReadOnlyList<SourceSpan> Evidence{get;}=Array.AsReadOnly(new[]{
        new SourceSpan(0x800159FC,0x48,"83ea25ec1c8183cfc9102d106ed5edad86807ce7c2f6e3606345bb367e7c7690"),
        new SourceSpan(0x80015AD8,0xA8,"4ef8e29a3058143b2905006924c846be8332409decdbe85f69582f4f846e7fa6"),
        new SourceSpan(0x80015B80,0x58,"edf30591c467d163e41cca4f58a8081e6ba68039326051bb1250c28d72bbc8f1"),
        new SourceSpan(0x8005B6F8,0xE0,"d0b4cf5a7cf9ee407ff413f7261bd5f92f4880d7cee8467852f4d757e6b84295")});
    private readonly byte[] before,after;
    private readonly NativeLevelDataGrowthLayout growth;
    // Privately minted after source/descriptor validation, before emission and
    // final hashing. No caller array, completed-plan mutation or lifecycle hash
    // participates in this geometry stage.
    private sealed record SchedulerGeometryReceipt(NativeJapaneseEggAdmittedLifecyclePlan Parent,
        NativeCollectibleSaveControllerKernel Controller,MemoryProjection Memory,IReadOnlyList<GlobalRebinding> Rebindings,uint Address);
    private readonly SchedulerGeometryReceipt? schedulerGeometry;
    private NativeCollectibleSaveWorkspacePlan(NativeRegionalSourceSession source,NativeJapaneseEggAdmittedLifecyclePlan ancestry,WorkspaceProfile profile)
    {
        // It rebinds the scene address (0x800785E4) from the 2 MB layout's.
        Spyro.Editor.Core.Universal.GameRam.RequirePlayStationSceneAddress("The collectible save workspace");
        ancestry.RequireSourceComposition();
        byte[] original=source.ReadExecutableRange(0,source.ExecutableByteLength),exe=ancestry.CopyExecutable();
        NativeJapaneseEggMemoryReport.RequireSourceExecutable(original);
        if(!ancestry.SourceCertified||ancestry.Lifecycle.LiveGuard is not{SourceBound:true}||
            !original.AsSpan().SequenceEqual(ancestry.CopyOriginalExecutable())||
            !source.ReadWadEntry(10).AsSpan().SequenceEqual(ancestry.Authored.Integration.CopyOriginalLevelData())||
            Word(exe,0x65DA4)!=0xA00||Word(exe,0x65DA8)!=0x200000)
            throw new InvalidDataException("Workspace requires the actual fixed admitted composition and same complete original USA source.");
        foreach(var span in Evidence)
        {
            int at=checked((int)(span.Pc-0x8000F800));
            if(Hash(original.AsSpan(at,span.ByteLength))!=span.Sha256||
                !original.AsSpan(at,span.ByteLength).SequenceEqual(exe.AsSpan(at,span.ByteLength)))
                throw new InvalidDataException("The admitted composition changed a normal model/scene loader owner.");
        }
        Profile=profile;Ancestry=ancestry;before=ancestry.CopyLevelData();OriginalDataSha256=Hash(before);
        OriginalDomain=new(NativeAddressKind.LevelData,"admitted-parent:"+OriginalDataSha256,NativeAddressRevision.Original,before.Length);
        var registration=NativeMobyModelRegistrationSource.DecodeFixture(OriginalDomain,before);
        Registrations=Array.AsReadOnly(registration.Registrations.ToArray());
        uint copy=ancestry.Lifecycle.Projection.CopyAnchor;
        var old=NativeLevelRuntimeMemoryReport.DecodeFixture(10,before,copy);
        if(old.SceneRamStart!=ancestry.Lifecycle.Projection.SceneAddress||old.ModelRamStart!=ancestry.Lifecycle.Projection.ModelAddress||
            old.SceneReadEnd!=ancestry.Startup.FinalMemory.SceneEnd||old.UsedLevelDataBytes!=ancestry.Authored.Integration.RuntimeMemory.UsedLevelDataBytes)
            throw new InvalidDataException("Actual admitted bytes disagree with their current-final normal projection.");
        Geometry=MeasureProfile(old.SceneRamStart,ancestry.Startup.FinalMemory.NormalReservedStart,profile);
        Controller=profile!=WorkspaceProfile.Preparation?NativeCollectibleSaveControllerKernel.Capture(source,Geometry.ArenaAddress):null;
        Preparation=Controller?.Preparation??NativeCollectibleSaveTailKernel.Capture(source,Geometry.ArenaAddress);Preparation.RequireSourceEvidence();
        if(Preparation.WorkspaceAddress+NativeSha256Kernel.RequiredWorkspaceByteLength>Geometry.ExpectedOrdinaryAddress||
            Preparation.Query.EntryPc+Preparation.Query.CopyBytes().Length>Preparation.ArenaAddress+NativeCollectibleSaveTailKernel.CodeCapacityBytes)
            throw new InvalidDataException("Measured preparation code/scratch exceeds its explicit complete arena.");
        byte[] insertion=new byte[Geometry.GrowthBytes];
        int prepareOffset=checked((int)(Preparation.ArenaAddress-Geometry.ArenaAddress));
        List<(string Role,int At,int Capacity,byte[] Bytes)> code=[];
        if(Controller is not null)code.Add(("controller",0,NativeCollectibleSaveControllerKernel.CodeCapacityBytes,Controller.CopyBytes()));
        code.AddRange(new (string,int,int,byte[])[]{
            ("prepare",prepareOffset,0x1000,Preparation.CopyBytes()),("sha256",prepareOffset+0x1000,0x800,Preparation.Sha256.CopyBytes()),
            ("envelope-validation",prepareOffset+0x1800,0x1000,Preparation.Loader.CopyBytes()),("state-query",prepareOffset+0x2800,0x800,Preparation.Query.CopyBytes())});
        foreach(var block in code)
        {
            if(block.Bytes.Length>block.Capacity||block.At+block.Capacity>prepareOffset+NativeCollectibleSaveTailKernel.CodeCapacityBytes)
                throw new InvalidDataException("Measured emitted code exceeds its closed workspace capacity.");
            block.Bytes.CopyTo(insertion,Geometry.LeadingBytes+block.At);
        }
        if(Controller is not null)
        {
            byte[] filename=Controller.CopyFilename();
            if(!original.AsSpan(0x15B8,filename.Length).SequenceEqual(filename)||
                Controller.ExpectedOrdinaryAddress!=Geometry.ExpectedOrdinaryAddress||Controller.OrdinaryReadbackAddress!=Geometry.OrdinaryReadbackAddress||
                Controller.TailReadbackAddress!=Geometry.TailReadbackAddress||Controller.StateAddress!=Geometry.TransactionStateAddress)
                throw new InvalidDataException("Actual controller filename or closed region geometry differs from its measured source profile.");
            filename.CopyTo(insertion,checked(Geometry.LeadingBytes+(int)(Controller.FilenameAddress-Geometry.ArenaAddress)));
        }
        growth=new(before.AsSpan(0,0x800),before.Length,
            [new(0,0,0,0,registration.ModelLength,0,insertion,2,false)]);
        if(growth.Growth!=Geometry.GrowthBytes||growth.Subfiles[2].Growth!=Geometry.GrowthBytes||
            growth.Subfiles[2].FinalOffset!=registration.ModelOffset)
            throw new InvalidDataException("Normal packed growth disagrees with the full explicit tail insertion.");
        after=growth.Expand(before);
        var newRegistration=NativeMobyModelRegistrationSource.DecodeFixture(
            new(NativeAddressKind.LevelData,"neutral-workspace-grammar",NativeAddressRevision.Original,after.Length),after);
        if(!newRegistration.Registrations.SequenceEqual(Registrations)||!after.AsSpan(0x50,0x180).SequenceEqual(before.AsSpan(0x50,0x180)))
            throw new InvalidDataException("A save workspace must not create or change any model/class registration.");
        var projected=NativeLevelRuntimeMemoryReport.DecodeFixture(10,after,copy);
        byte[] pete=ancestry.Authored.Integration.CopyPeteHeader();
        var measured=NativeJapaneseEggMemoryReport.Measure(copy,(uint)projected.UsedLevelDataBytes,Word(after,12),Word(after,20),Word(after,28),
            (uint)source.Entries[8].ByteLength,Word(pete,0),Word(exe,0x65DA8),Word(exe,0x65DA4),ancestry.Startup.AdapterStackByteLength);
        if(projected.UsedLevelDataBytes!=old.UsedLevelDataBytes||projected.ModelRamStart!=old.ModelRamStart||
            projected.ModelBytesRead!=old.ModelBytesRead+Geometry.GrowthBytes||projected.SceneBytesRead!=old.SceneBytesRead||
            projected.SceneRamStart!=Geometry.EndAddress||projected.SceneReadEnd!=old.SceneReadEnd+Geometry.GrowthBytes||
            projected.PlacedMobyCount!=old.PlacedMobyCount||projected.DynamicMobyCapacity!=old.DynamicMobyCapacity||
            measured.SceneEnd!=projected.SceneReadEnd||!measured.ModeledExtentsFit||
            measured.NormalReservedStart!=ancestry.Startup.FinalMemory.NormalReservedStart||
            measured.NormalGap!=ancestry.Startup.FinalMemory.NormalGap-Geometry.GrowthBytes)
            throw new InvalidDataException("Final complete model/scene/transfer geometry exceeds or disagrees with its actual A00 owners.");
        Memory=new(copy,old.UsedLevelDataBytes,old.ModelRamStart,old.ModelBytesRead,projected.ModelBytesRead,old.SceneBytesRead,
            old.SceneRamStart,projected.SceneRamStart,old.SceneReadEnd,projected.SceneReadEnd,measured.NormalReservedStart,
            ancestry.Startup.FinalMemory.NormalGap,measured.NormalGap,measured.TransferPeakEnd,old.MobyTableRamStart,projected.MobyTableRamStart);
        int tail=checked(registration.ModelOffset+registration.ModelLength);
        RequiredGlobalRebindings=Array.AsReadOnly(new[]{new GlobalRebinding(0x800785E4,Memory.OriginalSceneStart,Memory.FinalSceneStart),
            new GlobalRebinding(0x80075828,Memory.OriginalMobyTable,Memory.FinalMobyTable),
            new GlobalRebinding(0x80075890,ancestry.Lifecycle.Lifecycle.DynamicBase,checked(ancestry.Lifecycle.Lifecycle.DynamicBase+(uint)GrowthBytes))});
        if(profile==WorkspaceProfile.ControllerScheduler)
        {
            uint address=checked(Controller!.ArenaAddress+NativeCollectibleSaveControllerKernel.ArenaBytes);
            schedulerGeometry=new(ancestry,Controller,Memory,RequiredGlobalRebindings,address);
            Scheduler=NativeCollectibleSaveSchedulerKernel.CompileResidentGeometry(this);
            byte[] body=Scheduler.CopyResidentBytes();
            if(body.Length!=SchedulerOwnerBytes||Scheduler.ResidentAddress!=address||
                (ulong)address+SchedulerOwnerBytes>Memory.FinalSceneStart)
                throw new InvalidDataException("Regenerated scheduler exceeds its exact named model-tail owner.");
            body.CopyTo(after,checked(tail+Geometry.LeadingBytes+NativeCollectibleSaveControllerKernel.ArenaBytes));
            code.Add(("resident-scheduler",NativeCollectibleSaveControllerKernel.ArenaBytes,SchedulerOwnerBytes,
                body.AsSpan(0,Scheduler.ResidentCodeByteLength).ToArray()));
        }
        FinalDataSha256=Hash(after);
        FinalDomain=new(NativeAddressKind.LevelData,"detached-save-workspace:"+FinalDataSha256,NativeAddressRevision.Final,after.Length);
        TailExtent=new(FinalDomain,tail,Geometry.GrowthBytes);
        List<Owner> owners=[];int cursor=0;
        void Own(string role,int n,bool mutable=false,bool padding=false)
        {
            if(n==0)return;
            owners.Add(new(role,new(FinalDomain,tail+cursor,n),checked(Geometry.TailAddress+(uint)cursor),mutable,padding,
                Hash(after.AsSpan(tail+cursor,n))));cursor+=n;
        }
        Own("leading-alignment",Geometry.LeadingBytes,padding:true);
        void OwnPreparation()
        {
            Own("preparation-code",NativeCollectibleSaveTailKernel.CodeCapacityBytes);
            Own("preparation-scratch",NativeCollectibleSaveTailKernel.ArenaBytes-NativeCollectibleSaveTailKernel.CodeCapacityBytes,true);
        }
        if(Controller is null)
        {
            OwnPreparation();Own("expected-ordinary",OrdinaryBytes,true);Own("ordinary-readback",OrdinaryBytes,true);
            Own("tail-readback",TailReadbackBytes,true);Own("proposed-transaction-state",ProposedTransactionStateBytes,true);
        }
        else
        {
            foreach(var region in Controller.Regions)
            {
                if(region.Address!=Geometry.TailAddress+cursor)
                    throw new InvalidDataException("Actual controller regions are not one contiguous explicit arena.");
                if(region.Role=="preparation-arena")
                {
                    if(region.Address!=Preparation.ArenaAddress||region.ByteLength!=NativeCollectibleSaveTailKernel.ArenaBytes)
                        throw new InvalidDataException("Nested preparation region differs from its actual emitted owner.");
                    OwnPreparation();
                }
                else Own(region.Role,checked((int)region.ByteLength),region.Role is not("controller-code" or "fixed-filename" or "alignment"),region.Role=="alignment");
            }
            if(cursor!=Geometry.LeadingBytes+NativeCollectibleSaveControllerKernel.ArenaBytes)
                throw new InvalidDataException("Actual controller region partition exceeds its full measured arena.");
            if(Scheduler is not null)Own("resident-scheduler-code",SchedulerOwnerBytes);
        }
        Own("trailing-alignment",Geometry.TrailingBytes,padding:true);
        if(cursor!=Geometry.GrowthBytes||tail+cursor!=Word(after,0x18)||
            owners.Where(o=>o.Role is not("preparation-code" or "controller-code" or "fixed-filename" or "resident-scheduler-code"))
                .Any(o=>after.AsSpan((int)o.Extent.Offset,(int)o.Extent.ByteLength).ContainsAnyExcept((byte)0)))
            throw new InvalidDataException("All new code, zero-initialization and padding owners must exactly partition the inserted model tail.");
        Owners=owners.AsReadOnly();CodeBlocks=Array.AsReadOnly(code.Select(b=>new CodeBlock(b.Role,
            new(FinalDomain,tail+Geometry.LeadingBytes+b.At,b.Bytes.Length),checked(Geometry.ArenaAddress+(uint)b.At),b.Bytes)).ToArray());
        List<NativeRelocationRegion> regions=[new("header",new(OriginalDomain,0,0x800),new(FinalDomain,0,0x800),0x800)];
        foreach(var subfile in growth.Subfiles)
        {
            if(!after.AsSpan(subfile.FinalOffset,subfile.OriginalLength).SequenceEqual(before.AsSpan(subfile.OriginalOffset,subfile.OriginalLength)))
                throw new InvalidDataException("Workspace insertion changed a retained complete descriptor-owned payload.");
            regions.Add(new("retained-subfile-"+subfile.Index,new(OriginalDomain,subfile.OriginalOffset,subfile.OriginalLength),
                new(FinalDomain,subfile.FinalOffset,subfile.OriginalLength),0x800));
        }
        regions.AddRange(Owners.Select(o=>new NativeRelocationRegion(o.Role,null,o.Extent,4)));
        Layout=new(OriginalDomain,FinalDomain,regions);
        RequiredRowRebindings=Array.AsReadOnly(ancestry.Lifecycle.Binding.Rows.Select(row=>new RowRebinding(row.RowId,row.InstanceId,row.BitIndex,row.AuthoredClass,
            ancestry.Lifecycle.Projection.Models.Single(m=>m.RowId==row.RowId).RootAddress,row.RowAddress,checked(row.RowAddress+(uint)GrowthBytes),
            row.PrivateAddress,checked(row.PrivateAddress+(uint)GrowthBytes))).ToArray());
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{profile=profile==WorkspaceProfile.Preparation?
            "detached-admitted-artisans-save-workspace-v1":profile==WorkspaceProfile.Controller?
            "detached-admitted-artisans-save-controller-workspace-v1":"detached-admitted-artisans-save-controller-scheduler-workspace-v1",
            ancestry.ExecutableSha256,ancestor=ancestry.Lifecycle.PlanSha256,OriginalDataSha256,FinalDataSha256,Geometry,Memory,
            owners=Owners.Select(o=>new{o.Role,o.Extent.Offset,o.Extent.ByteLength,o.Address,o.Mutable,o.Padding,o.InitialSha256}),
            code=CodeBlocks.Select(c=>new{c.Role,c.Address,c.Sha256}),RequiredRowRebindings,RequiredGlobalRebindings}));
        _=RevertExact(after);_=RevertToOriginalSourceExact(after);
    }
    internal WorkspaceProfile Profile{get;}
    internal NativeJapaneseEggAdmittedLifecyclePlan Ancestry{get;}
    internal NativeCollectibleSaveTailKernel Preparation{get;}
    internal NativeCollectibleSaveControllerKernel? Controller{get;}
    internal NativeCollectibleSaveSchedulerKernel? Scheduler{get;}
    internal GeometryMeasurement Geometry{get;}
    internal MemoryProjection Memory{get;}
    internal int GrowthBytes=>Geometry.GrowthBytes;
    internal string OriginalDataSha256{get;}
    internal string FinalDataSha256{get;}
    internal string PlanSha256{get;}
    internal NativeAddressSpace OriginalDomain{get;}
    internal NativeAddressSpace FinalDomain{get;}
    internal NativeByteExtent TailExtent{get;}
    internal NativeRelocationMap Layout{get;}
    internal IReadOnlyList<NativeMobySourceRegistration> Registrations{get;}
    internal IReadOnlyList<Owner> Owners{get;}
    internal IReadOnlyList<CodeBlock> CodeBlocks{get;}
    internal IReadOnlyList<RowRebinding> RequiredRowRebindings{get;}
    internal IReadOnlyList<GlobalRebinding> RequiredGlobalRebindings{get;}
    internal bool SourceCertified=>true;
    // Constructor checks complete normal transfer geometry and actual emitted
    // capacities. This does not certify stack, lifetime, hooks or I/O execution.
    internal bool MeasuredAllocationFits=>Memory.FinalNormalGap>=0&&Memory.TransferPeakEnd<=Memory.NormalReservedStart;
    internal bool CurrentGuardMatchesProjection=>false;
    internal bool NativeResidenceIssued=>false;
    internal bool HooksInstalled=>false;
    internal bool InstructionCacheProven=>false;
    internal bool InterruptSafetyProven=>false;
    internal bool LifetimeProven=>false;
    internal bool OriginalModelReferenceClosureProven=>false;
    internal bool FullRuntimePeakProven=>false;
    internal bool RuntimeVerified=>false;
    internal bool AsynchronousCardLeaseProven=>false;
    internal bool FutureControllerFitProven=>false;
    internal bool CardWrites=>false;
    internal bool PublicationApproved=>false;
    internal byte[] CopyLevelData()=>after.ToArray();
    internal byte[] CopyOriginalLevelData()=>before.ToArray();
    internal byte[] CopyOwnerBytes(string role)
    {
        var owner=Owners.SingleOrDefault(o=>o.Role==role)??throw new InvalidDataException("Unknown closed workspace owner.");
        return after.AsSpan((int)owner.Extent.Offset,(int)owner.Extent.ByteLength).ToArray();
    }
    internal void RequireCurrent(NativeJapaneseEggAdmittedLifecyclePlan current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if(!ReferenceEquals(current,Ancestry)||!current.CopyLevelData().AsSpan().SequenceEqual(before))
            throw new InvalidDataException("Workspace is bound to its exact actual admitted composition instance.");
        current.RequireSourceComposition();
    }
    internal byte[] RevertExact(ReadOnlySpan<byte> bytes)
    {
        if(!bytes.SequenceEqual(after))throw new InvalidDataException("Workspace inverse requires its entire exact detached afterimage, not runtime-mutated scratch.");
        byte[] inverse=new byte[before.Length];bytes[..0x800].CopyTo(inverse);
        foreach(var s in growth.Subfiles)
        {bytes.Slice(s.FinalOffset,s.OriginalLength).CopyTo(inverse.AsSpan(s.OriginalOffset));Put(inverse,s.Index*8,(uint)s.OriginalOffset);Put(inverse,s.Index*8+4,(uint)s.OriginalLength);}
        if(!inverse.AsSpan().SequenceEqual(before))throw new InvalidDataException("Workspace inverse changed an admitted-parent header or payload complement.");
        return inverse;
    }
    internal byte[] RevertToOriginalSourceExact(ReadOnlySpan<byte> bytes)=>Ancestry.RevertLevelDataExact(RevertExact(bytes));
    internal void RequireNativeInstallation()=>throw new InvalidDataException(
        "Detached workspace only: row/callable/live-guard rebinding and save hooks are absent; code cache, model-reference closure, stack/IRQ, transitions and asynchronous card lease are unproved. Measured code/buffer allocation is not native integration fit.");
    internal static NativeCollectibleSaveWorkspacePlan Capture(NativeRegionalSourceSession source,NativeJapaneseEggAdmittedLifecyclePlan ancestry)=>
        CaptureProfile(source,ancestry,WorkspaceProfile.Preparation);
    internal static NativeCollectibleSaveWorkspacePlan CaptureController(NativeRegionalSourceSession source,NativeJapaneseEggAdmittedLifecyclePlan ancestry)=>
        CaptureProfile(source,ancestry,WorkspaceProfile.Controller);
    internal static NativeCollectibleSaveWorkspacePlan CaptureControllerScheduler(NativeRegionalSourceSession source,NativeJapaneseEggAdmittedLifecyclePlan ancestry)=>
        CaptureProfile(source,ancestry,WorkspaceProfile.ControllerScheduler);
    internal uint SchedulerEmissionAddress=>RequireSchedulerGeometry().Address;
    internal IReadOnlyList<(uint Address,uint Value)> ReadSchedulerEmissionGlobals()
    {
        var receipt=RequireSchedulerGeometry();
        return Array.AsReadOnly(receipt.Parent.Lifecycle.LiveGuard!.ExpectedSceneGlobals.Where(g=>g.Address!=0x800757D8)
            .Select(g=>receipt.Rebindings.SingleOrDefault(r=>r.Address==g.Address) is{} r?(g.Address,r.NewValue):g)
            .Concat(new(uint,uint)[]{(0x80078D00,1),(0x800755A4,0xA00),(0x80078D14,1),(0x80078D1C,0),(0x800756BC,0x80086134)}).ToArray());
    }
    private SchedulerGeometryReceipt RequireSchedulerGeometry()
    {
        if(Profile!=WorkspaceProfile.ControllerScheduler||schedulerGeometry is not{} r||!ReferenceEquals(r.Parent,Ancestry)||
            !ReferenceEquals(r.Controller,Controller)||!ReferenceEquals(r.Memory,Memory)||!ReferenceEquals(r.Rebindings,RequiredGlobalRebindings))
            throw new InvalidDataException("Source-validated privately minted scheduler geometry required.");
        RequireCurrent(Ancestry);r.Controller.RequireSourceEvidence();return r;
    }
    private static NativeCollectibleSaveWorkspacePlan CaptureProfile(NativeRegionalSourceSession source,NativeJapaneseEggAdmittedLifecyclePlan ancestry,WorkspaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(ancestry);
        try
        {
            if(source.Revision!=NativeDiscRevision.UsaScus94228)throw new InvalidDataException("Exact original USA required.");
            return new(source,ancestry,profile);
        }
        finally{source.VerifyUnchanged();}
    }
    // Neutral range arithmetic only: TailGapToFloor excludes the following
    // scene; only the source constructor measures the complete resource. This
    // method cannot create bytes, a source plan, lease or installed composition.
    internal static GeometryMeasurement Measure(uint tailAddress,uint normalFloor)=>MeasureProfile(tailAddress,normalFloor,WorkspaceProfile.Preparation);
    internal static GeometryMeasurement MeasureController(uint tailAddress,uint normalFloor)=>MeasureProfile(tailAddress,normalFloor,WorkspaceProfile.Controller);
    internal static GeometryMeasurement MeasureControllerScheduler(uint tailAddress,uint normalFloor)=>MeasureProfile(tailAddress,normalFloor,WorkspaceProfile.ControllerScheduler);
    private static GeometryMeasurement MeasureProfile(uint tailAddress,uint normalFloor,WorkspaceProfile profile)
    {
        if(tailAddress<0x80090000||tailAddress%4!=0||normalFloor>0x801FF600||normalFloor<=tailAddress)
            throw new InvalidDataException("Bounded aligned cached-RAM model tail and explicit normal floor required.");
        int leading=(int)((16-tailAddress%16)%16);
        int semantic=profile!=WorkspaceProfile.Preparation?NativeCollectibleSaveControllerKernel.ArenaBytes+(profile==WorkspaceProfile.ControllerScheduler?SchedulerOwnerBytes:0):
            checked(NativeCollectibleSaveTailKernel.ArenaBytes+2*OrdinaryBytes+TailReadbackBytes+ProposedTransactionStateBytes);
        int growth=checked((leading+semantic+0x7FF)/0x800*0x800);
        ulong end=(ulong)tailAddress+(uint)growth,arena=(ulong)tailAddress+(uint)leading;
        int kernelArena=profile!=WorkspaceProfile.Preparation?semantic:NativeCollectibleSaveTailKernel.ArenaBytes;
        if(end>normalFloor||arena+(uint)kernelArena>0x801F0000)
            throw new InvalidDataException("Complete workspace owner exceeds its bounded arena or declared normal floor.");
        uint expected=checked((uint)arena+NativeCollectibleSaveTailKernel.ArenaBytes),readback=checked(expected+OrdinaryBytes),
            tailReadback=checked(readback+OrdinaryBytes),state=checked(tailReadback+TailReadbackBytes);
        if(profile!=WorkspaceProfile.Preparation)
        {
            // Neutral emitter geometry only; this fixture cannot issue source
            // ownership, bytes for installation, or an asynchronous lease.
            var controller=NativeCollectibleSaveControllerKernel.CreateFixture((uint)arena);
            expected=controller.ExpectedOrdinaryAddress;readback=controller.OrdinaryReadbackAddress;
            tailReadback=controller.TailReadbackAddress;state=controller.StateAddress;
            if(controller.Regions.Sum(r=>(long)r.ByteLength)+(profile==WorkspaceProfile.ControllerScheduler?SchedulerOwnerBytes:0)!=semantic)
                throw new InvalidDataException("Actual controller regions disagree with the declared complete arena.");
        }
        return new(tailAddress,(uint)arena,leading,semantic,growth,expected,readback,tailReadback,state,growth-leading-semantic,(uint)end,(long)normalFloor-(long)end);
    }
    private static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static void Put(Span<byte> bytes,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..],value);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
