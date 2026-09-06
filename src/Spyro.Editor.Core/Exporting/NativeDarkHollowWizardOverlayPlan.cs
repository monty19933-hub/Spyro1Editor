using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeWizardOverlayBlock(string Id,NativeByteExtent Owner,byte[] Bytes);
internal sealed record NativeWizardOverlayEntry(string Id,string BlockId,NativeByteExtent Owner);
public sealed record NativeWizardOverlayCodeOwner(string Id,int OverlayOffset,uint RuntimePc,int ByteLength,string Sha256);

/// <summary>Exact native research overlay installation. No publication or runtime acceptance.</summary>
public sealed class NativeDarkHollowWizardOverlayPlan
{
    private const uint OverlayBase=0x8007AA38;
    private const int OriginalLength=0xB000;
    private readonly byte[] _overlay,_executable,_data,_originalOverlay,_originalExecutable;
    private readonly NativeDarkHollowWizardResourcePlan _resources;
    private NativeDarkHollowWizardOverlayPlan(NativeDarkHollowWizardBehaviorSource source,
        NativeDarkHollowWizardResourcePlan resources,NativeDarkHollowWizardParticleBehaviorSource particles)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(resources);ArgumentNullException.ThrowIfNull(particles);
        if(!resources.IncludesSceneAppend)throw new InvalidDataException("Native overlay requires the source-owned private scene append.");
        if(!resources.ParticleDescriptorInstalled)throw new InvalidDataException("Native overlay requires its native particle descriptor.");
        if(source.PackageSha256!="a9a8227c74a9c479f7f94c82314813809bd5caa0a5a137b0c18d614b80b3e3df"||
            resources.TargetLevelId!=12||resources.SourceImageSha256!=NativeMobySourceInventory.OriginalImageSha256||
            resources.SourceDataSha256!="a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97"||
            resources.DonorDataSha256!="9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8"||
            !resources.Resources.Select(r=>r.ActorClassId).Order().SequenceEqual(new ushort[]{0x26,0x11B})||
            resources.RuntimeMemory.PlacedMobyCount!=129||resources.RuntimeMemory.DynamicMobyCapacity!=90||
            resources.RuntimeMemory.PlacedMobyCount+resources.RuntimeMemory.DynamicMobyCapacity>256||
            resources.RuntimeMemory.CopyBufferAddress!=OriginalCopyBufferAddress)
            throw new InvalidDataException("Native overlay resource/source or byte-sized dynamic owner binding changed.");
        _resources=resources;_data=resources.CopyFinalLevelData();resources.RequireFinal(_data);
        _originalOverlay=source.CopyOriginalDarkOverlay();_originalExecutable=source.CopyOriginalExecutable();
        particles.RequireOriginalDark(_originalOverlay);
        RequireHash(_originalExecutable,"a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998","executable preimage");
        RequireHash(_originalExecutable.AsSpan(0x4B048,16),"8949add56d6960cd6ab04432154419c31b596758b14ba41f99a63cb962e922aa","copy-buffer preimage");
        var specialization=NativeDarkHollowWizardSpawn26Specialization.Create(source);
        var main=source.MainPackage;var spawn=specialization.Package;var particle=particles.Package;
        int mainLength=EncodedLength(main),spawnLength=EncodedLength(spawn),particleLength=EncodedLength(particle);
        var sizing=NativeMipsOwnedFrameAdapter.Create(0x80082818,source.DestinationContinuationPc);
        int entryLength=sizing.CopyEntry().Length,exitLength=sizing.CopyExit().Length;
        int exactLength=checked(mainLength+spawnLength+particleLength+4*8+8+exitLength+2*entryLength+0x44);
        int finalLength=checked((OriginalLength+exactLength+0x7FF)&~0x7FF);
        var domain=new NativeAddressSpace(NativeAddressKind.LevelData,"dark-native-wizard-overlay:"+resources.FinalDataSha256,
            NativeAddressRevision.Final,finalLength);
        var arena=new NativeByteExtent(domain,OriginalLength,finalLength-OriginalLength);
        int cursor=OriginalLength;
        NativeByteExtent Allocate(int length){var extent=new NativeByteExtent(domain,cursor,length);cursor=checked(cursor+length);return extent;}
        var mainOwner=Allocate(mainLength);var spawnOwner=Allocate(spawnLength);var particleOwner=Allocate(particleLength);
        uint[] continuationPcs=[0x8008442C,0x80084448,0x80084450,0x8008553C];
        var bridges=continuationPcs.Select(pc=>new NativeParticleBehaviorBridge(pc,Allocate(8))).ToArray();
        var effectOwner=Allocate(8);var exitOwner=Allocate(exitLength);var lightningOwner=Allocate(entryLength);var wizardOwner=Allocate(entryLength);
        var dispatchOwner=Allocate(0x44);
        NativeByteExtent Entry(NativeByteExtent owner)=>new(domain,owner.Offset,4);
        var spawnEntry=SourceEntry(spawn,spawnOwner,0x80086DD8);
        var exitEntry=Entry(exitOwner);var effectEntry=new NativeByteExtent(domain,effectOwner.Offset,8);
        var lightningEntry=Entry(lightningOwner);var wizardEntry=Entry(wizardOwner);
        var mainLightning=SourceEntry(main,mainOwner,0x8007EAD0);var mainWizard=SourceEntry(main,mainOwner,0x80082818);
        NativeByteExtent LinkCapacity(NativeByteExtent prefix)=>new(domain,prefix.Offset,arena.EndExclusive-prefix.Offset);
        NativeMipsBehaviorExternalBinding Bind(NativeMipsBehaviorTransfer t,NativeByteExtent target)=>new(t.SourcePc,t.TargetPc,Pc(target),target);
        List<NativeMipsBehaviorExternalBinding> mainBindings=[];
        foreach(var t in main.Transfers)
        {
            if(t.Kind==NativeMipsBehaviorTransferKind.ReturnRegister||t.TargetPc is uint pc&&main.OwnsPc(pc))continue;
            if(t.Kind==NativeMipsBehaviorTransferKind.Call26){mainBindings.Add(source.BindExactExecutableCall(t.SourcePc));continue;}
            if(t.Kind==NativeMipsBehaviorTransferKind.CallbackRegister)
            {
                if(specialization.Callers.Any(c=>c.CallPc==t.SourcePc))mainBindings.Add(Bind(t,spawnEntry));
                else if(t.SourcePc is 0x8007EC60 or 0x80082840)mainBindings.Add(Bind(t,effectEntry));
                else throw new InvalidDataException("Uncertified native main callback binding.");
            }
            else if(t.TargetPc==source.InterceptedExitPc)mainBindings.Add(Bind(t,exitEntry));
            else throw new InvalidDataException("Uncertified native main continuation binding.");
        }
        List<NativeMipsBehaviorExternalBinding> spawnBindings=[];
        foreach(var t in spawn.Transfers)
        {
            if(t.Kind==NativeMipsBehaviorTransferKind.ReturnRegister||t.TargetPc is uint pc&&spawn.OwnsPc(pc))continue;
            if(t.Kind==NativeMipsBehaviorTransferKind.Call26)spawnBindings.Add(source.BindExactExecutableCall(t.SourcePc));
            else if(t.Kind==NativeMipsBehaviorTransferKind.CallbackRegister&&t.SourcePc==0x8008728C)spawnBindings.Add(Bind(t,effectEntry));
            else throw new InvalidDataException("Uncertified private class26 continuation binding.");
        }
        var linkedMain=main.Link(LinkCapacity(mainOwner),Pc(mainOwner),mainBindings);
        var linkedSpawn=spawn.Link(LinkCapacity(spawnOwner),Pc(spawnOwner),spawnBindings);
        var linkedParticle=particles.LinkInto(LinkCapacity(particleOwner),Pc(particleOwner),bridges);
        if(linkedMain.SourceToFinalPc[0x8007EAD0]!=Pc(mainLightning)||linkedMain.SourceToFinalPc[0x80082818]!=Pc(mainWizard)||
            linkedSpawn.SourceToFinalPc[0x80086DD8]!=Pc(spawnEntry))
            throw new InvalidDataException("Native linked entry layout changed.");
        var lightningAdapter=NativeMipsOwnedFrameAdapter.Create(Pc(mainLightning),source.DestinationContinuationPc);
        var wizardAdapter=NativeMipsOwnedFrameAdapter.Create(Pc(mainWizard),source.DestinationContinuationPc);
        if(!lightningAdapter.CopyExit().AsSpan().SequenceEqual(wizardAdapter.CopyExit()))
            throw new InvalidDataException("Owned frame adapters disagree on exact native context restoration.");
        List<NativeWizardOverlayBlock> blocks=[new("native-main",mainOwner,linkedMain.CopyBytes()),new("private-spawn26",spawnOwner,linkedSpawn.CopyBytes()),
            new("native-particles",particleOwner,linkedParticle.CopyBytes())];
        List<NativeWizardOverlayEntry> entries=[new("spawn26","private-spawn26",spawnEntry),new("main-exit","main-exit",exitEntry),
            new("effect-call","effect-call",effectEntry),new("lightning-adapter","lightning-adapter",lightningEntry),
            new("wizard-adapter","wizard-adapter",wizardEntry),new("main-lightning","native-main",mainLightning),new("main-wizard","native-main",mainWizard)];
        foreach(var bridge in bridges)
        {
            string id=$"particle-return-{bridge.DestinationPc:X8}";
            blocks.Add(new(id,bridge.Owner,particles.CopyBridgeBytes(bridge.DestinationPc)));entries.Add(new(id,id,bridge.Owner));
        }
        blocks.Add(new("effect-call",effectOwner,JumpBytes(0x800844A0)));
        blocks.Add(new("main-exit",exitOwner,lightningAdapter.CopyExit()));
        blocks.Add(new("lightning-adapter",lightningOwner,lightningAdapter.CopyEntry()));
        blocks.Add(new("wizard-adapter",wizardOwner,wizardAdapter.CopyEntry()));
        blocks.Add(new("class-dispatch",dispatchOwner,BuildDispatch(_originalOverlay.AsSpan(0x4EC,16),dispatchOwner,lightningEntry,wizardEntry)));
        entries.Add(new("class-dispatch","class-dispatch",Entry(dispatchOwner)));
        foreach(uint entryPc in particle.EntryPcs)
        {
            var entry=SourceEntry(particle,particleOwner,entryPc);
            if(linkedParticle.SourceToFinalPc[entryPc]!=Pc(entry))throw new InvalidDataException("Native particle entry layout changed.");
            entries.Add(new($"particle-{entryPc:X8}","native-particles",entry));
        }
        if(cursor<finalLength)
        {
            int paddingLength=finalLength-cursor;
            blocks.Add(new("owned-sector-padding",Allocate(paddingLength),new byte[paddingLength]));
        }
        var reservations=linkedMain.UnmaterializedExternalTargets.Concat(linkedSpawn.UnmaterializedExternalTargets)
            .Concat(linkedParticle.UnmaterializedExternalTargets).Concat(new[]{mainLightning,mainWizard,lightningEntry,wizardEntry,Entry(dispatchOwner)})
            .Concat(particle.EntryPcs.Select(pc=>SourceEntry(particle,particleOwner,pc))).ToArray();
        byte[] appended=Materialize(arena,blocks,entries,reservations);
        _overlay=new byte[finalLength];_originalOverlay.CopyTo(_overlay,0);appended.CopyTo(_overlay,OriginalLength);
        // Only this exact eight-byte compare-chain entry and four default particle
        // table words are owned original mutations. The old delay word stays intact.
        JumpBytes(Pc(dispatchOwner)).CopyTo(_overlay,0x4EC);
        int[] expectedFields=[0x158,0x240,0x29C,0x384];
        if(!particles.Hooks.Select(h=>(int)h.Read.Extent.Offset).Order().SequenceEqual(expectedFields.Order()))
            throw new InvalidDataException("Native particle dispatch ownership changed.");
        foreach(var hook in particles.Hooks)
        {
            int offset=checked((int)hook.Read.Extent.Offset);
            if(hook.Read.Extent.ByteLength!=4||Word(_originalOverlay,offset)!=hook.BeforeWord||Hash(_originalOverlay.AsSpan(offset,4))!=hook.Read.ExpectedSha256)
                throw new InvalidDataException("Native particle hook source preimage changed.");
            Write(_overlay,offset,linkedParticle.SourceToFinalPc[hook.SourceEntryPc]);
        }
        _executable=_originalExecutable.ToArray();
        short low=unchecked((short)FinalCopyBufferAddress);uint high=(uint)(((long)FinalCopyBufferAddress-low)>>16);
        Write(_executable,0x4B048,0x3C020000|high);Write(_executable,0x4B04C,0x24420000u|(ushort)low);
        OverlaySha256=Hash(_overlay);ExecutableSha256=Hash(_executable);
        CodeOwners=Array.AsReadOnly(blocks.OrderBy(b=>b.Owner.Offset).Select(b=>new NativeWizardOverlayCodeOwner(b.Id,(int)b.Owner.Offset,
            Pc(b.Owner),(int)b.Owner.ByteLength,Hash(b.Bytes))).ToArray());
        RequireFinal(_overlay,_executable,_data);
        RuntimeMemory=resources.RuntimeMemory.AnalyzeDarkWizardOverlay(this);
        if(!RuntimeMemory.KnownPostLoadExtentsDisjoint||RuntimeMemory.DynamicMobyCapacity!=90)
            throw new InvalidDataException("Native appended code exceeds known resident-memory ownership.");
    }
    public static NativeDarkHollowWizardOverlayPlan Compile(NativeDarkHollowWizardBehaviorSource source,
        NativeDarkHollowWizardResourcePlan resources,NativeDarkHollowWizardParticleBehaviorSource particles)=>new(source,resources,particles);
    public byte[] CopyOverlay()=>_overlay.ToArray();
    public byte[] CopyExecutable()=>_executable.ToArray();
    public byte[] CopyFinalLevelData()=>_data.ToArray();
    public byte[] CopyOriginalOverlay()=>_originalOverlay.ToArray();
    public byte[] CopyOriginalExecutable()=>_originalExecutable.ToArray();
    public void RequireFinal(ReadOnlySpan<byte> overlay,ReadOnlySpan<byte> executable,ReadOnlySpan<byte> data)
    {
        if(!overlay.SequenceEqual(_overlay))throw new InvalidDataException("Native overlay final readback differs from its complete owned/preserved bytes.");
        if(!executable.SequenceEqual(_executable))throw new InvalidDataException("Native executable final readback differs from its copy-buffer-only plan.");
        if(!data.SequenceEqual(_data))throw new InvalidDataException("Native level data final readback differs from the composed resource plan.");
        _resources.RequireFinal(data);
    }
    public bool AllCodeTargetsMaterialized=>true;
    public bool ActiveDispatchEncoded=>true;
    public bool CompleteNativeClosureProven=>false;
    public bool GteStateClosureProven=>false;
    public bool TransitiveGlobalClosureProven=>false;
    public bool RuntimeFitProven=>false;
    public bool PlacementApproved=>false;
    public bool PublicationApproved=>false;
    public uint OriginalCopyBufferAddress=>0x80085594;
    public uint FinalCopyBufferAddress=>checked(OverlayBase+(uint)_overlay.Length);
    public NativeLevelRuntimeMemoryReport RuntimeMemory { get; }
    public string OverlaySha256 { get; }
    public string ExecutableSha256 { get; }
    public IReadOnlyList<NativeWizardOverlayCodeOwner> CodeOwners { get; }
    internal static byte[] BuildDispatch(ReadOnlySpan<byte> originalWindow,NativeByteExtent owner,
        NativeByteExtent lightningEntry,NativeByteExtent wizardEntry)
    {
        RequireHash(originalWindow,"c54d1a48c5c696468592a372640fe186f2d30071f210a6adec39783e9a013e0b","dispatch preimage");
        RequireFinalExtent(owner);RequireFinalExtent(lightningEntry);RequireFinalExtent(wizardEntry);
        if(owner.ByteLength!=0x44||lightningEntry.Space!=owner.Space||wizardEntry.Space!=owner.Space)
            throw new InvalidDataException("Native dispatch requires exact length and one canonical final domain.");
        uint[] words=[0x24020026,0x14620003,0,Jump(Pc(lightningEntry)),0,0x2402011B,0x14620003,0,Jump(Pc(wizardEntry)),0,
            0x240200FB,0x10620003,0x286200FC,Jump(0x8007AF30),0,Jump(0x800812FC),0];
        var bytes=new byte[words.Length*4];for(int i=0;i<words.Length;i++)Write(bytes,i*4,words[i]);return bytes;
    }
    internal static byte[] Materialize(NativeByteExtent arena,IEnumerable<NativeWizardOverlayBlock> blocks,
        IEnumerable<NativeWizardOverlayEntry> entries,IEnumerable<NativeByteExtent> reservations)
    {
        RequireFinalExtent(arena);
        if(arena.ByteLength>0x20000)throw new InvalidDataException("Native code materialization byte budget exceeded.");
        var blockSnapshot=NativeMipsBehaviorFragmentCodec.BoundedSnapshot(blocks,64,"code block");
        var entrySnapshot=NativeMipsBehaviorFragmentCodec.BoundedSnapshot(entries,256,"code entry");
        var reservationSnapshot=NativeMipsBehaviorFragmentCodec.BoundedSnapshot(reservations,1024,"code reservation");
        if(blockSnapshot.Any(b=>b==null)||entrySnapshot.Any(e=>e==null))throw new InvalidDataException("Native code ownership is absent.");
        long cursor=arena.Offset;HashSet<string> blockIds=new(StringComparer.Ordinal);
        foreach(var block in blockSnapshot.OrderBy(b=>b.Owner?.Offset??-1))
        {
            Within(block.Owner);if(string.IsNullOrWhiteSpace(block.Id)||!blockIds.Add(block.Id)||block.Owner.Offset<cursor)
                throw new InvalidDataException("Native code owners overlap or duplicate their identity.");
            if(block.Owner.Offset!=cursor)throw new InvalidDataException("Native code coverage contains an unowned hole.");
            if(block.Bytes==null||block.Bytes.LongLength!=block.Owner.ByteLength)throw new InvalidDataException("Native code owner payload length differs.");
            cursor=block.Owner.EndExclusive;
        }
        if(cursor!=arena.EndExclusive)throw new InvalidDataException("Native code coverage does not fill its exact arena.");
        HashSet<string> entryIds=new(StringComparer.Ordinal);
        foreach(var entry in entrySnapshot)
        {
            Within(entry.Owner);
            var block=blockSnapshot.SingleOrDefault(b=>b.Id==entry.BlockId);
            if(string.IsNullOrWhiteSpace(entry.Id)||!entryIds.Add(entry.Id)||block==null||entry.Owner.Offset<block.Owner.Offset||entry.Owner.EndExclusive>block.Owner.EndExclusive)
                throw new InvalidDataException("Native code entry has no unique owning materialized block.");
        }
        foreach(var target in reservationSnapshot)
        {
            Within(target);
            if(!entrySnapshot.Any(e=>e.Owner.Offset==target.Offset&&e.Owner.EndExclusive>=target.EndExclusive))
                throw new InvalidDataException("Native code reservation lacks a declared materialized entry.");
        }
        var result=new byte[(int)arena.ByteLength];
        foreach(var block in blockSnapshot)block.Bytes.CopyTo(result,(int)(block.Owner.Offset-arena.Offset));
        return result;
        void Within(NativeByteExtent? extent)
        {
            if(extent==null)throw new InvalidDataException("Native code domain owner is absent.");
            RequireFinalExtent(extent);
            if(extent.Space!=arena.Space||extent.Offset<arena.Offset||extent.EndExclusive>arena.EndExclusive)
                throw new InvalidDataException("Native code owner has another final domain or lies outside its arena.");
        }
    }
    private static NativeByteExtent SourceEntry(NativeMipsBehaviorFragmentPackage package,NativeByteExtent prefix,uint sourcePc)
    {
        long offset=prefix.Offset;
        foreach(var fragment in package.Fragments)
        {
            if(sourcePc>=fragment.Source.SourcePc&&(ulong)sourcePc<fragment.Source.SourcePc+(ulong)fragment.ByteLength)
                return new(prefix.Space,offset+sourcePc-fragment.Source.SourcePc,4);
            offset+=fragment.ByteLength+(fragment.HasFallthrough?8:0);
        }
        throw new InvalidDataException("Unknown native source code entry.");
    }
    private static int EncodedLength(NativeMipsBehaviorFragmentPackage package)=>package.Fragments.Sum(f=>f.ByteLength+(f.HasFallthrough?8:0));
    private static void RequireFinalExtent(NativeByteExtent extent)
    {
        ArgumentNullException.ThrowIfNull(extent);
        if(extent.Space.Kind!=NativeAddressKind.LevelData||extent.Space.Revision!=NativeAddressRevision.Final||extent.Offset<OriginalLength||
            extent.ByteLength<4||extent.Offset%4!=0||extent.ByteLength%4!=0||OverlayBase+extent.EndExclusive>0x80200000L)
            throw new InvalidDataException("Native code requires a bounded aligned appended final overlay domain.");
    }
    private static uint Pc(NativeByteExtent extent)=>checked(OverlayBase+(uint)extent.Offset);
    private static uint Jump(uint pc)=>0x08000000|((pc>>2)&0x3FFFFFF);
    private static byte[] JumpBytes(uint pc){var bytes=new byte[8];Write(bytes,0,Jump(pc));return bytes;}
    private static void Write(Span<byte> bytes,int offset,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..],value);
    private static uint Word(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void RequireHash(ReadOnlySpan<byte> bytes,string hash,string name)
    {if(Hash(bytes)!=hash)throw new InvalidDataException("Native "+name+" certificate changed.");}
}
