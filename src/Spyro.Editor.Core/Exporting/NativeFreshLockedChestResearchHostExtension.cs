using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeFreshChestRetainedCode(string Id,uint RuntimePc,int ByteLength,string Sha256);
internal sealed record NativeFreshChestComputedCall(uint SourcePc,uint FinalPc,uint Word,uint DelayWord);

/// <summary>
/// Detached extension of an actual mixed Wizard/Grass Host and executable.
/// This does not install rows, resources, a complete Body or a final executable.
/// The current census remains10. Full callback/lifetime/model closure and native
/// coexistence testing are separate gates; copying code does not grant them.
/// </summary>
internal sealed class NativeFreshLockedChestResearchHostExtension
{
    private const uint Base=0x8007AA38,ExeBase=0x8000F800,Loop=0x80087258,Reset=0x800858F8;
    private const int Prefix=0x11000,InitializerOffset=0x4B098;
    private readonly byte[] _overlay,_initializer,_executable;
    private readonly NativeLockedChestCodeRelocation _chest,_reward;
    private readonly NativeLockedChestBehaviorSource _behavior;

    private NativeFreshLockedChestResearchHostExtension(NativeId65FreshWizardResearchExecutablePlan current,
        NativeLockedChestFamilySource source)
    {
        CurrentExecutableAncestor=current;FamilySourceAncestor=source;
        RequireAncestry(current,source);
        byte[] old=HostAncestor.CopyOverlay(),oldInit=HostAncestor.CopyInitializerPatch(),exe=current.CopyExecutable();
        NativeId65FreshWizardHostLayout.Require(old,Prefix,"ad6d952cbf70ab8b1eab53f7637ffd0235271b7b20696956c9406f36f714fa7e");
        NativeId65FreshWizardHostLayout.Require(oldInit,0x98,"d7f0e7c9ed86467f0749db689b2bf1f1c4df27587eb357821ef8947d7f70466b");
        NativeId65FreshWizardHostLayout.Require(exe,0x66000,"a0eb46c2735355c05d66cc33492ed70c6c2174e77e8e8eb6c2ca8fae0f3fc8dc");
        if(!exe.AsSpan(InitializerOffset,0x98).SequenceEqual(oldInit)||Word(exe,0x1CA8)!=0x8005A898)
            throw new InvalidDataException("Chest Host requires the actual already-installed current Family initializer.");
        var exeDomain=source.ResourceInputs["key.special-renderer-and-global-routines"].Single().Extent.Space;
        byte[] originalExe=source.ReadSource(new(exeDomain,0,exeDomain.ByteLength));
        if(!current.RevertExact(exe).SequenceEqual(originalExe))
            throw new InvalidDataException("Current executable and chest source have different complete Original owners.");

        _behavior=NativeLockedChestBehaviorSource.Capture((at,length)=>
        {
            string role=at switch{0x188719C=>"setup",0x1887330=>"state",0x1881B64=>"reward",
                _=>throw new InvalidDataException("Unknown chest behavior source request.")};
            var read=source.HandlerReads.Single(r=>r.Id==role);
            if(read.Extent.ByteLength!=length)throw new InvalidDataException("Chest behavior source extent changed.");
            return source.ReadSource(read.Extent);
        });
        AppendLayout=NativeOwnedOverlayAppendLayout.Create("fresh-locked-chest-host-extension:"+current.PlanSha256,
            old,Base,0x12000,[new("chest",0x740),new("reward",0x438),new("reward-dispatch",0x14),
                new("chest-dispatch",0x14),new("delete-thunk",0x10),new("sound-thunk",0x10)],"padding");
        if(AppendLayout.RetainedByteLength!=Prefix||AppendLayout.UsedByteLength!=0x11BC0||AppendLayout.FinalByteLength!=0x12000)
            throw new InvalidDataException("Closed chest Host allocation geometry changed.");
        uint Pc(string id)=>Base+checked((uint)AppendLayout.Blocks.Single(b=>b.Id==id).Owner.Offset);
        uint chest=Pc("chest"),reward=Pc("reward"),delete=Pc("delete-thunk"),sound=Pc("sound-thunk");
        _chest=NativeLockedChestCodeRelocation.Compile([
            new("setup",0x80083BD4,chest+4,_behavior.CopySetup()),
            new("state",0x80083D68,chest+0x34,_behavior.CopyState())],[new(0x8008A20C,Loop)]);
        _reward=NativeLockedChestCodeRelocation.Compile([
            new("reward",0x8007E59C,reward+4,_behavior.CopyReward())],[new(0x8008A20C,Loop),
            new(0x80088CF4,Reset),new(0x8008A204,delete),new(0x800880FC,sound)]);
        Transfers=Array.AsReadOnly(_chest.Transfers.Concat(_reward.Transfers).ToArray());
        if(Transfers.Any(t=>t.DelayWord is null))throw new InvalidDataException("Chest transfer lost its complete source delay slot.");
        byte[][] payloads=[ [..Words(0x0260A021),.._chest.CopyBytes()], [..Words(0x0260A021),.._reward.CopyBytes()],
            Dispatch(0xD,Pc("reward-dispatch"),reward),Dispatch(0xAE,Pc("chest-dispatch"),chest),
            Words(0x0C01495A,0x02602021,Jump(Loop),0),Words(0x0C01569E,0x24A70054,Jump(Loop),0) ];
        var entries=new[]{("chest-adapter","chest",0),("setup","chest",4),("state","chest",0x34),
            ("reward-adapter","reward",0),("reward","reward",4),("reward-dispatch","reward-dispatch",0),
            ("chest-dispatch","chest-dispatch",0),("delete-thunk","delete-thunk",0),("sound-thunk","sound-thunk",0)}
            .Select(e=>new NativeOwnedOverlayEntry(e.Item1,e.Item2,new(AppendLayout.Domain,
                Pc(e.Item2)-Base+e.Item3,4))).ToArray();
        _overlay=AppendLayout.Materialize(AppendLayout.Blocks.Where(b=>!b.IsPadding).Select((b,i)=>
            new NativeOwnedOverlayBlock(b.Id,b.Owner,payloads[i])),entries,entries.Select(e=>e.Owner));
        List<NativeFreshWizardHostPatch> hooks=[];
        foreach(var hook in new[]{(At:0x3148,Target:Pc("reward-dispatch")),(At:0x3214,Target:Pc("chest-dispatch"))})
        {
            if(Word(old,hook.At)!=0x08021C96||Word(old,hook.At+4)!=0)
                throw new InvalidDataException("Chest Host default dispatch preimage changed.");
            byte[] after=Words(Jump(hook.Target),0);after.CopyTo(_overlay,hook.At);
            hooks.Add(new("chest-dispatch-"+hook.At,hook.At,Base+(uint)hook.At,old.AsSpan(hook.At,8),after));
        }
        TownPatches=hooks.AsReadOnly();
        _initializer=oldInit.ToArray();uint anchor=AppendLayout.CopyAnchor;
        if(Word(oldInit,0x18)!=0x3C028009||Word(oldInit,0x20)!=0x2442BA38||anchor!=0x8008CA38)
            throw new InvalidDataException("Closed current initializer copy-anchor preimage changed.");
        Put(_initializer,0x18,0x3C020000|((anchor+0x8000)>>16));Put(_initializer,0x20,0x24420000|(anchor&0xFFFF));
        InitializerPatch=new("extended-conditional-copy",InitializerOffset,0x8005A898,oldInit,_initializer);
        _executable=exe.ToArray();_initializer.CopyTo(_executable,InitializerOffset);

        // Bind concrete consumers against BOTH the source-issued current EXE
        // and the new prototype. Entry lookahead is explicitly not full closure.
        List<NativeFreshChestRetainedCode> consumers=[];
        void Consumer(string id,uint pc,int length)
        {
            int at=checked((int)(pc-ExeBase));
            if(at<0||at>originalExe.Length-length||!originalExe.AsSpan(at,length).SequenceEqual(exe.AsSpan(at,length))||
                !originalExe.AsSpan(at,length).SequenceEqual(_executable.AsSpan(at,length)))
                throw new InvalidDataException("Chest retained executable consumer changed: "+id);
            consumers.Add(new(id,pc,length,Hash(originalExe.AsSpan(at,length))));
        }
        foreach(uint pc in Transfers.Where(t=>t.TargetKind==NativeLockedChestTargetKind.RetainedUnprovenCall)
            .Select(t=>t.FinalTarget).Append(0x80052568u).Append(0x80055A78u).Distinct().Order())Consumer("call-entry-only",pc,0x10);
        foreach(var span in new[]{("delete",0x80052568u,0x64),("sound",0x80055A78u,0x82C),
            ("copy-leaf",0x80017700u,0x1C),("subtract-leaf",0x8001778Cu,0x34),
            ("initializer-prefix-before-GTE",0x800526A8u,0xB0),("initializer-tail-and-neighbor",0x8005B6E0u,0xA0)})
            Consumer(span.Item1,span.Item2,span.Item3);
        ExecutableConsumers=consumers.AsReadOnly();
        byte[] state=_behavior.CopyState();List<NativeFreshChestComputedCall> computed=[];
        var expected=new[]{(0x80084304u,0x26100001u),(0x8008431Cu,0x02802821u),(0x8008436Cu,0x26100001u),
            (0x80084394u,0x24070018u),(0x80084454u,0u)};
        for(int at=0;at<state.Length;at+=4)
        {
            uint w=Word(state,at);if(w>>26!=0||(w&63) is not 8 and not 9)continue;
            uint sourcePc=0x80083D68u+(uint)at;
            if(computed.Count>=expected.Length||sourcePc!=expected[computed.Count].Item1||w!=0x0040F809||
                at+8>state.Length||Word(state,at+4)!=expected[computed.Count].Item2)
                throw new InvalidDataException("The five retained unexecuted callback sites changed.");
            computed.Add(new(sourcePc,chest+0x34+(uint)at,w,Word(state,at+4)));
        }
        if(computed.Count!=5)throw new InvalidDataException("Incomplete computed callback inventory.");
        ComputedCalls= computed.AsReadOnly();
        var townDomain=source.ResourceInputs["key.native-handler"].Single().Extent.Space;
        foreach(var span in new[]{(0x80087258u,0x4C),(Reset,0x4C),(0x80083B4Cu,0x160),(0x8007DBD8u,8)})
        {
            int at=checked((int)(span.Item1-Base));byte[] before=source.ReadSource(new(townDomain,at,span.Item2));
            if(!before.AsSpan().SequenceEqual(old.AsSpan(at,span.Item2))||!before.AsSpan().SequenceEqual(_overlay.AsSpan(at,span.Item2)))
                throw new InvalidDataException("Retained Town continuation/key owner changed.");
        }
        RequireSourceInverse();
        if(!RevertExactOverlay(_overlay).SequenceEqual(old)||!RevertExactExecutable(_executable).SequenceEqual(exe))
            throw new InvalidDataException("Complete chest Host immediate-ancestor inverse changed.");
        OverlaySha256=Hash(_overlay);InitializerSha256=Hash(_initializer);ExecutablePrototypeSha256=Hash(_executable);
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{Profile="detached-mixed-family-chest-host-extension-v1",
            CurrentExecutablePlan=current.PlanSha256,HostPlan=HostAncestor.PlanSha256,OverlaySha256,InitializerSha256,
            ExecutablePrototypeSha256,Transfers,ComputedCalls,ExecutableConsumers,SourceCertified,RuntimeVerified,
            PublicationApproved,BodyIssued,PlacedFamilyIssued,FinalExecutableIssued,CompleteNativeDependencyClosureProven}));
    }

    internal NativeId65FreshWizardResearchExecutablePlan CurrentExecutableAncestor{get;}
    internal NativeLockedChestFamilySource FamilySourceAncestor{get;}
    internal NativeId65FreshWizardResearchHostPlan HostAncestor=>CurrentExecutableAncestor.BodyAncestor.HostAncestor;
    internal NativeOwnedOverlayAppendLayout AppendLayout{get;}
    internal IReadOnlyList<NativeFreshWizardHostPatch> TownPatches{get;}
    internal NativeFreshWizardHostPatch InitializerPatch{get;}
    internal IReadOnlyList<NativeLockedChestTransfer> Transfers{get;}
    internal IReadOnlyList<NativeFreshChestComputedCall> ComputedCalls{get;}
    internal IReadOnlyList<NativeFreshChestRetainedCode> ExecutableConsumers{get;}
    internal string OverlaySha256{get;}
    internal string InitializerSha256{get;}
    internal string ExecutablePrototypeSha256{get;}
    internal string PlanSha256{get;}
    internal bool SourceCertified=>true;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal bool BodyIssued=>false;
    internal bool PlacedFamilyIssued=>false;
    internal bool FinalExecutableIssued=>false;
    internal bool CompleteNativeDependencyClosureProven=>false;
    internal byte[] CopyOverlay()=>_overlay.ToArray();
    internal byte[] CopyInitializer()=>_initializer.ToArray();
    internal byte[] CopyExecutablePrototype()=>_executable.ToArray();
    internal void RequireAncestry(NativeId65FreshWizardResearchExecutablePlan current,NativeLockedChestFamilySource source)
    {
        ArgumentNullException.ThrowIfNull(current);ArgumentNullException.ThrowIfNull(source);
        if(!ReferenceEquals(current,CurrentExecutableAncestor)||!ReferenceEquals(source,FamilySourceAncestor))
            throw new InvalidDataException("Chest Host extension belongs to another actual executable/family source pair.");
        var family=current.FamilyCompositionAncestor??throw new InvalidDataException("Chest Host needs an actual admitted Family executable, not an equal-byte prior arm.");
        current.RequireFamilyAncestry(family);source.RequireRetainedOwners();
        HostAncestor.RequireAncestry(HostAncestor.SceneAncestor,HostAncestor.FallbackAncestor);
    }
    internal void RequireExactOutputs(ReadOnlySpan<byte> overlay,ReadOnlySpan<byte> initializer,ReadOnlySpan<byte> executable)
    {
        RequireAncestry(CurrentExecutableAncestor,FamilySourceAncestor);
        if(!overlay.SequenceEqual(_overlay)||!initializer.SequenceEqual(_initializer)||!executable.SequenceEqual(_executable))
            throw new InvalidDataException("Complete chest Host/initializer/executable prototype afterimage changed.");
    }
    internal byte[] RevertExactOverlay(ReadOnlySpan<byte> overlay)
    {
        if(!overlay.SequenceEqual(_overlay))throw new InvalidDataException("Complete chest Host inverse requires its exact afterimage.");
        byte[] inverse=overlay[..Prefix].ToArray();foreach(var patch in TownPatches)patch.CopyBeforeBytes().CopyTo(inverse,patch.Offset);
        if(!inverse.SequenceEqual(HostAncestor.CopyOverlay()))throw new InvalidDataException("Chest Host prefix inverse differs.");
        return inverse;
    }
    internal byte[] RevertExactExecutable(ReadOnlySpan<byte> executable)
    {
        if(!executable.SequenceEqual(_executable))throw new InvalidDataException("Complete chest executable inverse requires its exact prototype.");
        byte[] inverse=executable.ToArray();InitializerPatch.CopyBeforeBytes().CopyTo(inverse,InitializerOffset);
        CurrentExecutableAncestor.RequireExactExecutable(inverse);return inverse;
    }
    private void RequireSourceInverse()
    {
        var restored=_chest.RestoreSources(_overlay.AsSpan(Prefix+4,0x73C))
            .Concat(_reward.RestoreSources(_overlay.AsSpan(Prefix+0x744,0x434))).ToArray();
        if(restored.Length!=3||!restored[0].SequenceEqual(_behavior.CopySetup())||!restored[1].SequenceEqual(_behavior.CopyState())||
            !restored[2].SequenceEqual(_behavior.CopyReward()))throw new InvalidDataException("Chest complete donor-code inverse changed.");
    }
    internal static NativeFreshLockedChestResearchHostExtension Compose(NativeId65FreshWizardResearchExecutablePlan current,
        NativeLockedChestFamilySource source)
    {ArgumentNullException.ThrowIfNull(current);ArgumentNullException.ThrowIfNull(source);return new(current,source);}
    private static byte[] Dispatch(uint cls,uint pc,uint target)
    {
        long delta=(long)target-pc-8;
        if(delta%4!=0||delta/4 is <short.MinValue or >short.MaxValue)throw new InvalidDataException("Chest dispatch range exceeded.");
        return Words(0x24020000|cls,0x10820000u|(ushort)(short)(delta/4),0,Jump(Loop),0);
    }
    private static uint Jump(uint target)=>0x08000000|((target>>2)&0x03FFFFFF);
    private static byte[] Words(params uint[] words)
    {byte[] result=new byte[words.Length*4];for(int i=0;i<words.Length;i++)Put(result,i*4,words[i]);return result;}
    private static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static void Put(Span<byte> bytes,int at,uint word)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..],word);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
