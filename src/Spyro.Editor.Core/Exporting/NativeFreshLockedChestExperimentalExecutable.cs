using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeFreshChestExperimentalRewardRow(int PlacedOrdinal,ushort ActorClassId,byte ActorKind,
    byte RewardSelector,int? DeterministicSelectorValue,int? AssumedContribution,string Premise,
    int SceneRowOffset,string SceneRowSha256,string SourceRowSha256);

/// <summary>
/// Exact placed-Body/Host executable transaction with an experimental target20.
/// Source certification covers retained inputs, selector values, passive Grass
/// dispatch and the one target patch. Key-zero is an explicit assumption, not
/// a complete census, transitive non-award, collection or runtime certificate.
/// </summary>
internal sealed class NativeFreshLockedChestExperimentalExecutable
{
    private readonly byte[] _executable,_prototype,_original;
    private NativeFreshLockedChestExperimentalExecutable(NativeFreshLockedChestPlacementPlan placement,
        NativeFreshLockedChestResearchHostExtension host)
    {
        PlacementAncestor=placement;HostAncestor=host;RequireAncestry(placement,host);
        var source=host.FamilySourceAncestor;
        var domain=source.ResourceInputs["key.special-renderer-and-global-routines"].Single().Extent.Space;
        if(domain.Kind!=NativeAddressKind.ExecutableFile||domain.Revision!=NativeAddressRevision.Original||domain.ByteLength!=0x66000)
            throw new InvalidDataException("Experimental chest target requires the complete actual Original executable domain.");
        _original=source.ReadSource(new(domain,0,domain.ByteLength));
        NativeId65FreshExecutablePlan.RequireOriginal(_original);
        _prototype=host.CopyExecutablePrototype();
        if(_prototype.Length!=0x66000||Half(_prototype,TargetFileOffset)!=10||Half(_original,TargetFileOffset)!=0||
            !host.CurrentExecutableAncestor.RevertExact(host.RevertExactExecutable(_prototype)).SequenceEqual(_original))
            throw new InvalidDataException("Experimental target lost its exact target10 Host and full Original inverse.");

        var evidence=new[]{
            new NativeId65EngineEvidence("retained-native-selector-value-chain","EXE",0x2C264,0x58,
                "5a7677129481343250dfaa32337c82b805d64902f154ea5c0af991a460947e48"),
            NativeFreshFamilyStaticRewardCensus.SelectorEvidence,
            new("retained-native-treasure-target-consumer","EXE",0xC8CC,0x34,
                "f9504fc22bd7daca311dafd8843738ccda340eac012b575f115fe1e26a930b58")};
        foreach(var item in evidence)
        {
            int at=checked((int)item.Offset);
            if(Hash(_original.AsSpan(at,item.ByteLength))!=item.Sha256||
                !_prototype.AsSpan(at,item.ByteLength).SequenceEqual(_original.AsSpan(at,item.ByteLength)))
                throw new InvalidDataException("Experimental target retained native value/emitter/consumer owner changed.");
        }
        if(Hash(_original.AsSpan(0x5FC38,0x48))!="641867a8c3d0c8af833f4ce678cd27ff7520e7ddafa4cecfd19c7a388f625ee6"||
            Word(_original,0x2C274)!=0x0800EEAD||Word(_original,0x2C278)!=0x24040001||
            Word(_original,0x2C284)!=0x0800EEAD||Word(_original,0x2C288)!=0x24040002||
            Word(_original,0x2C2A4)!=0x0800EEAD||Word(_original,0x2C2A8)!=0x2404000A||
            Word(_original,0x2C2B4)!=0x0C00EE15||Word(_original,0x2C2B8)!=0x02002821)
            throw new InvalidDataException("Native value branch/delay/award-argument evidence or original target table changed.");
        Evidence=Array.AsReadOnly(evidence);

        var family=host.CurrentExecutableAncestor.FamilyCompositionAncestor!;
        var passive=family.Placements[1];var priorTrace=passive.Dispatch!;
        priorTrace.RequireAncestry(host.HostAncestor,passive.PassiveAsset!);
        byte[] overlay=host.CopyOverlay();
        var actualTrace=NativeGrassFamilySourceDecoder.InspectPassiveDispatch(overlay,4,0x8007DB34,0x80087258,
            "actual extended chest Host Grass");
        if(actualTrace.ExecutedTraceSha256!=priorTrace.ExecutedTraceSha256||
            !actualTrace.ExecutedInstructionAddresses.SequenceEqual(priorTrace.ExecutedInstructionAddresses)||
            !actualTrace.ExecutedInstructionWords.SequenceEqual(priorTrace.ExecutedInstructionWords)||
            actualTrace.CallInstructionCount!=0||actualTrace.PropertiesReadCount!=0||actualTrace.RuntimeActorWriteCount!=0)
            throw new InvalidDataException("Actual extended Host changed the retained passive Grass dispatch premise.");
        GrassDispatchTraceSha256=actualTrace.ExecutedTraceSha256;

        byte[] scene=placement.CopyScene(),placedBody=placement.CopyLevelData();
        if(scene.Length!=0x3800||placedBody.Length!=0x20F000||Word(scene,0xD0)!=9||source.PrivateRows.Count!=7||
            BodyAncestor.SceneAncestor.Allocation.Rows.Count!=9||Word(placedBody,0x18)!=0x102000||
            Word(placedBody,0x1C)!=scene.Length||!placedBody.AsSpan(0x102000,scene.Length).SequenceEqual(scene))
            throw new InvalidDataException("Experimental target needs the exact complete actual placed P9 scene and Body.");
        ushort[] classes=[0x11B,0x1F5,0xAD,0xAE,0xD,0xD,0xD,0xD,0xD];
        byte[] selectors=[0x56,0xFF,0xFF,0x54,0x54,0x53,0x54,0x53,0x54];
        List<NativeFreshChestExperimentalRewardRow> rows=[];
        for(int i=0;i<classes.Length;i++)
        {
            int at=0xD4+i*0x58;var allocation=BodyAncestor.SceneAncestor.Allocation.Rows[i];
            ReadOnlySpan<byte> row=scene.AsSpan(at,0x58);
            byte[] donor=i==0?family.Placements[0].WizardAsset!.CopyOwner("wizard.row"):
                i==1?passive.PassiveAsset!.CopyRow():source.ReadSource(source.PrivateRows[i-2].Row.Extent);
            if(allocation.Row.Offset!=at||Word(row,0)!=allocation.Properties.Offset||donor.Length!=0x58||
                Half(row,0x36)!=classes[i]||Half(donor,0x36)!=classes[i]||row[0x53]!=selectors[i]||donor[0x53]!=selectors[i]||
                row[0x3A]!=donor[0x3A])
                throw new InvalidDataException("Experimental target lost an actual ordered source class, kind, selector or private owner.");
            int? known=selectors[i] switch{0x53=>(short)Word(_original,0x2C278),0x54=>(short)Word(_original,0x2C288),
                0x56=>(short)Word(_original,0x2C2A8),_=>null};
            bool assumed=i is 1 or 2;
            if(assumed!=(known is null))throw new InvalidDataException("Only explicit Grass/key premises may lack a known selector value.");
            string premise=i==1?"retained-passive-dispatch-zero":i==2?"experimental-key-zero-not-certified":"source-deterministic-selector-value";
            rows.Add(new(i,classes[i],row[0x3A],selectors[i],known,assumed?0:null,premise,at,Hash(row),Hash(donor)));
        }
        Rows=rows.AsReadOnly();KnownSelectorCount=rows.Count(r=>r.DeterministicSelectorValue.HasValue);
        KnownDeterministicSelectorSubtotal=checked(rows.Sum(r=>r.DeterministicSelectorValue??0));
        ExperimentalTreasureTarget=checked(KnownDeterministicSelectorSubtotal+rows.Sum(r=>r.AssumedContribution??0));
        if(KnownSelectorCount!=7||KnownDeterministicSelectorSubtotal!=20||ExperimentalTreasureTarget!=20||ExperimentalTreasureTarget>short.MaxValue)
            throw new InvalidDataException("Experimental source-selector subtotal escaped the fixed seven-known-value target20 premise.");

        TargetPatch=new("experimental-chest-slot35-target-key-zero-assumption",TargetFileOffset,"0A00","1400");
        _executable=_prototype.ToArray();
        NativeId65FreshWizardExecutableLayout.ApplySequential(_executable,TargetFileOffset,
            Convert.FromHexString(TargetPatch.BeforeHex),Convert.FromHexString(TargetPatch.AfterHex));
        if(!_executable.AsSpan(0,TargetFileOffset).SequenceEqual(_prototype.AsSpan(0,TargetFileOffset))||
            !_executable.AsSpan(TargetFileOffset+2).SequenceEqual(_prototype.AsSpan(TargetFileOffset+2)))
            throw new InvalidDataException("Experimental executable changed an owner outside its two-byte target field.");
        PriorExecutableSha256=Hash(_prototype);ExecutableSha256=Hash(_executable);
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            Profile="placed-chest-experimental-selector20-executable-v1",SourceImageSha256,
            PlacementPlan=placement.PlanSha256,placement.LevelDataSha256,placement.SceneSha256,
            BodyPlan=BodyAncestor.PlanSha256,HostPlan=host.PlanSha256,PriorExecutableSha256,ExecutableSha256,
            Rows,KnownSelectorCount,KnownDeterministicSelectorSubtotal,ExperimentalTreasureTarget,TargetSlot,TargetFileOffset,TargetPatch,
            GrassDispatchTraceSha256,Evidence,SourceCertified,Experimental,GrassPassiveDispatchPreserved,KeyZeroContributionProven,
            CompleteStaticCensusProven,RuntimeDropGuarantee,CompleteNativeDependencyClosureProven,RuntimeVerified,PublicationApproved}));
        if(!RevertExactExecutable(_executable).SequenceEqual(_prototype)||!RevertExactOriginal(_executable).SequenceEqual(_original))
            throw new InvalidDataException("Experimental executable lost a complete immediate or Original inverse.");
    }

    internal NativeFreshLockedChestPlacementPlan PlacementAncestor{get;}
    internal NativeFreshLockedChestResearchBody BodyAncestor=>PlacementAncestor.BodyAncestor;
    internal NativeFreshLockedChestResearchHostExtension HostAncestor{get;}
    internal IReadOnlyList<NativeFreshChestExperimentalRewardRow> Rows{get;}
    internal IReadOnlyList<NativeId65EngineEvidence> Evidence{get;}
    internal NativeId65FreshExecutableWindow TargetPatch{get;}
    internal int KnownSelectorCount{get;}
    internal int KnownDeterministicSelectorSubtotal{get;}
    internal int ExperimentalTreasureTarget{get;}
    internal int TargetSlot=>35;
    internal int TargetFileOffset=>0x5FC7E;
    internal string SourceImageSha256=>NativeMobySourceInventory.OriginalImageSha256;
    internal string PriorExecutableSha256{get;}
    internal string ExecutableSha256{get;}
    internal string GrassDispatchTraceSha256{get;}
    internal string PlanSha256{get;}
    internal bool SourceCertified=>true;
    internal bool Experimental=>true;
    internal bool GrassPassiveDispatchPreserved=>true;
    internal bool KeyZeroContributionProven=>false;
    internal bool CompleteStaticCensusProven=>false;
    internal bool RuntimeDropGuarantee=>false;
    internal bool CompleteNativeDependencyClosureProven=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal byte[] CopyExecutable()=>_executable.ToArray();
    internal void RequireAncestry(NativeFreshLockedChestPlacementPlan placement,NativeFreshLockedChestResearchHostExtension host)
    {
        ArgumentNullException.ThrowIfNull(placement);ArgumentNullException.ThrowIfNull(host);
        if(!ReferenceEquals(placement,PlacementAncestor)||!ReferenceEquals(host,HostAncestor)||
            !ReferenceEquals(placement.BodyAncestor.HostAncestor,host))
            throw new InvalidDataException("Experimental executable belongs to another actual placement/Body/Host chain.");
        placement.RequireAncestry(placement.BodyAncestor,placement.Intent);
        placement.RequireExactScene(placement.CopyScene());placement.RequireExactLevelData(placement.CopyLevelData());
        host.RequireAncestry(host.CurrentExecutableAncestor,host.FamilySourceAncestor);
        host.RequireExactOutputs(host.CopyOverlay(),host.CopyInitializer(),host.CopyExecutablePrototype());
    }
    internal void RequireExactExecutable(ReadOnlySpan<byte> executable)
    {
        RequireAncestry(PlacementAncestor,HostAncestor);
        if(!executable.SequenceEqual(_executable))throw new InvalidDataException("Complete experimental executable afterimage or complement changed.");
    }
    internal byte[] RevertExactExecutable(ReadOnlySpan<byte> executable)
    {
        RequireExactExecutable(executable);byte[] inverse=executable.ToArray();
        NativeId65FreshWizardExecutableLayout.ApplySequential(inverse,TargetFileOffset,
            Convert.FromHexString(TargetPatch.AfterHex),Convert.FromHexString(TargetPatch.BeforeHex));
        if(!inverse.SequenceEqual(_prototype))throw new InvalidDataException("Experimental inverse lost its actual Host prototype.");
        HostAncestor.RequireExactOutputs(HostAncestor.CopyOverlay(),HostAncestor.CopyInitializer(),inverse);return inverse;
    }
    internal byte[] RevertExactOriginal(ReadOnlySpan<byte> executable)
    {
        byte[] prototype=RevertExactExecutable(executable);
        byte[] original=HostAncestor.CurrentExecutableAncestor.RevertExact(HostAncestor.RevertExactExecutable(prototype));
        if(!original.SequenceEqual(_original))throw new InvalidDataException("Experimental inverse does not recover its actual complete Original owner.");
        return original;
    }
    internal static NativeFreshLockedChestExperimentalExecutable Compose(NativeFreshLockedChestPlacementPlan placement,
        NativeFreshLockedChestResearchHostExtension host)
    {ArgumentNullException.ThrowIfNull(placement);ArgumentNullException.ThrowIfNull(host);return new(placement,host);}
    private static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static ushort Half(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
