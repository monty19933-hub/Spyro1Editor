using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeFreshMobySoundResidentSelection(string Id,int? GlobalSelector,
    int SourceDescriptorIndex,int TargetDescriptorIndex,NativeByteExtent Descriptor,NativeByteExtent Sample);

// An Original source-field obligation only. This binding does not own an
// installed model extent; the separate family model materializer must bind it.
internal sealed record NativeFreshMobySoundLocalSelectorObligation(NativeMobyDependencyRead SourceField,
    int SourceDescriptorIndex,int TargetDescriptorIndex);

/// <summary>
/// Three extracted key/chest sound resources already resident in one actual
/// Fresh family composition. This does not install anything or close native
/// routines, playback, handlers, model fields, globals or family admission.
/// </summary>
internal sealed class NativeFreshMobySoundResidencyBinding
{
    private const string Profile="native-fresh-locked-chest-resident-sound-bytes-v1";
    private readonly string _compositionPlanSha256,_levelDataSha256;

    private NativeFreshMobySoundResidencyBinding(NativeLockedChestFamilySource source,
        NativeFreshMobyFamilyCompositionPlan composition)
    {
        SourceAncestor=source;CompositionAncestor=composition;
        var inspected=Inspect(source,composition);
        Graph=inspected.Graph;Selections=Array.AsReadOnly(inspected.Selections);
        LocalSelector=inspected.LocalSelector;
        _compositionPlanSha256=composition.PlanSha256;_levelDataSha256=composition.LevelDataSha256;
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            Profile,CompositionPlanSha256=_compositionPlanSha256,LevelDataSha256=_levelDataSha256,
            SourcePackageSha256=source.PackageRead.ExpectedSha256,Graph.Fingerprint,Selections,LocalSelector}));
    }

    internal NativeLockedChestFamilySource SourceAncestor{get;}
    internal NativeFreshMobyFamilyCompositionPlan CompositionAncestor{get;}
    internal NativeMobyDependencyClosurePlan Graph{get;}
    internal IReadOnlyList<NativeFreshMobySoundResidentSelection> Selections{get;}
    internal NativeFreshMobySoundLocalSelectorObligation LocalSelector{get;}
    internal string PlanSha256{get;}

    internal static NativeFreshMobySoundResidencyBinding BindLockedChest(NativeLockedChestFamilySource source,
        NativeFreshMobyFamilyCompositionPlan composition)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(composition);
        return new(source,composition);
    }

    internal void RequireAncestry(NativeLockedChestFamilySource source,NativeFreshMobyFamilyCompositionPlan composition)
    {
        if(!ReferenceEquals(source,SourceAncestor)||!ReferenceEquals(composition,CompositionAncestor))
            throw new InvalidDataException("Sound residency requires its actual source/composition pair, not equal reissued owners.");
        RequireRetainedOwners();
    }

    internal void RequireRetainedOwners()
    {
        if(CompositionAncestor.PlanSha256!=_compositionPlanSha256||CompositionAncestor.LevelDataSha256!=_levelDataSha256)
            throw new InvalidDataException("Sound residency lost its complete retained composition identity.");
        var actual=Inspect(SourceAncestor,CompositionAncestor);
        if(actual.Graph.Fingerprint!=Graph.Fingerprint||!actual.Selections.SequenceEqual(Selections)||actual.LocalSelector!=LocalSelector)
            throw new InvalidDataException("Retained sound selectors, complete resources or final preservation bindings changed.");
        RequireFinalPreservations(Graph,SourceAncestor,CompositionAncestor.FinalBodyDomain,CompositionAncestor.CopyLevelData());
    }

    internal void RequireExactLevelData(ReadOnlySpan<byte> bytes)
    {
        // The complete existing issuer rejects changes to untouched complement
        // as well as selected sound fields. Selected final bytes are then checked
        // explicitly; successful graph compilation alone never reads them.
        CompositionAncestor.RequireExactLevelData(bytes);
        RequireRetainedOwners();
        RequireFinalPreservations(Graph,SourceAncestor,CompositionAncestor.FinalBodyDomain,bytes);
    }

    // Detached sound-only package bytes, not an installed model, a Final model
    // extent or complete package dependency closure. The residency plan identity
    // remains a source/composition identity, not an output-package certificate.
    internal byte[] CopySoundRetargetedSourcePackage() => PrepareSoundRetargetedSourcePackage().After;

    internal byte[] RevertExactSoundRetargetedPackage(ReadOnlySpan<byte> bytes)
    {
        var prepared=PrepareSoundRetargetedSourcePackage();
        if(!bytes.SequenceEqual(prepared.After))
            throw new InvalidDataException("Sound-only package inverse requires the complete exact detached afterimage, including its unchanged complement.");
        var slot=prepared.Binding;
        byte[] original=NativeModelSoundPackageSource.ApplyBindings(bytes,
            [slot with {Before=slot.After,After=slot.Before}]);
        if(!original.AsSpan().SequenceEqual(prepared.Before))
            throw new InvalidDataException("Sound-only package inverse did not recover its complete original source package.");
        return original;
    }

    private (byte[] Before,byte[] After,NativeModelSoundSlotBinding Binding) PrepareSoundRetargetedSourcePackage()
    {
        RequireRetainedOwners();
        var source=SourceAncestor;var read=source.PackageRead;var original=read.Extent.Space;
        var registration=NativeMobyModelRegistrationSource.CaptureExactPeace(original,source.ReadSource);
        var root=registration.Registrations.SingleOrDefault(r=>r.ActorId==0xAE&&r.Slot>0)
            ??throw new InvalidDataException("The chest sound package has no exact generic source registration.");
        long end=root.Slot+1<registration.Registrations.Count?
            registration.Registrations[root.Slot+1].RootDataOffset:registration.ModelEnd;
        NativeByteExtent extent=new(original,root.RootDataOffset,end-root.RootDataOffset);
        byte[] package=source.ReadSource(extent);
        if(!ReferenceEquals(registration.Original,original)||extent!=read.Extent||
            package.LongLength!=read.Extent.ByteLength||Hash(package)!=read.ExpectedSha256||
            !package.AsSpan().SequenceEqual(source.CopyPackage()))
            throw new InvalidDataException("Sound-only retargeting requires the complete registered Original chest package interval.");
        var field=LocalSelector.SourceField;var selection=Selections.Single(s=>s.GlobalSelector is null);
        long relative=field.Extent.Offset-extent.Offset;
        if(!ReferenceEquals(field.Extent.Space,original)||relative!=4||field.Extent.ByteLength!=4||
            Hash(package.AsSpan(4,4))!=field.ExpectedSha256||
            !package.AsSpan(4,4).SequenceEqual(source.ReadSource(field.Extent))||
            BinaryPrimitives.ReadUInt32LittleEndian(package.AsSpan(4,4))!=0xFFFFFF2F||
            LocalSelector.SourceDescriptorIndex!=47||LocalSelector.TargetDescriptorIndex!=54||
            selection.SourceDescriptorIndex!=LocalSelector.SourceDescriptorIndex||
            selection.TargetDescriptorIndex!=LocalSelector.TargetDescriptorIndex)
            throw new InvalidDataException("Sound-only retargeting lost its original four-byte local selector and actual resident sound obligation.");
        var binding=new NativeModelSoundSlotBinding(0,checked((int)relative),
            checked((byte)LocalSelector.SourceDescriptorIndex),checked((byte)LocalSelector.TargetDescriptorIndex));
        byte[] after=NativeModelSoundPackageSource.ApplyBindings(package,[binding]);
        if(after.Length!=package.Length||after[4]!=54||!after.AsSpan(0,4).SequenceEqual(package.AsSpan(0,4))||
            !after.AsSpan(5).SequenceEqual(package.AsSpan(5)))
            throw new InvalidDataException("Sound-only retargeting changed bytes outside its single owned local selector.");
        return(package,after,binding);
    }

    private static (NativeMobyDependencyClosurePlan Graph,NativeFreshMobySoundResidentSelection[] Selections,
        NativeFreshMobySoundLocalSelectorObligation LocalSelector) Inspect(NativeLockedChestFamilySource source,
        NativeFreshMobyFamilyCompositionPlan composition)
    {
        source.RequireRetainedOwners();
        byte[] body=composition.CopyLevelData();composition.RequireExactLevelData(body);
        var finalDomain=composition.FinalBodyDomain;
        if(finalDomain.Kind!=NativeAddressKind.LevelData||finalDomain.Revision!=NativeAddressRevision.Final||finalDomain.ByteLength!=body.Length)
            throw new InvalidDataException("Sound residency requires the actual complete FinalBody address owner.");
        var final=new NativeLevelSoundLayout(body);
        var donorDomain=source.PrivateRows[0].Row.Extent.Space;
        var residentDomain=source.ResourceInputs["sound.key.descriptor"].Single().Extent.Space;
        var donor=new NativeLevelSoundLayout(source.ReadSource(new(donorDomain,0,donorDomain.ByteLength)));
        var resident=new NativeLevelSoundLayout(source.ReadSource(new(residentDomain,0,residentDomain.ByteLength)));
        List<NativeMobyDependencyNode> nodes=[];
        List<NativeFreshMobySoundResidentSelection> selections=[];
        (string Id,int? Global,int SourceIndex,int ResidentIndex)[] requirements=[
            ("sound.key",0,17,17),("sound.reward",0x37,35,35),("sound.chest-local54",null,47,54)];
        foreach(var requirement in requirements)
        {
            var equivalence=source.ResourceInputs[requirement.Id+".equivalence"];
            var incomingRead=equivalence.Single(r=>r.Id=="donor-descriptor");
            var incomingSample=equivalence.Single(r=>r.Id=="donor-sample");
            var targetRead=source.ResourceInputs[requirement.Id+".descriptor"].Single();
            var targetSample=source.ResourceInputs[requirement.Id+".sample"].Single();
            int sourceIndex=DescriptorIndex(donor,donorDomain,incomingRead);
            int residentIndex=DescriptorIndex(resident,residentDomain,targetRead);
            if(sourceIndex!=requirement.SourceIndex||residentIndex!=requirement.ResidentIndex||residentIndex>=final.Sound.Descriptors.Count)
                throw new InvalidDataException("Extracted sound descriptor namespaces escaped their source-owned requirements.");
            var incoming=donor.Sound.Descriptors[sourceIndex];var originalResident=resident.Sound.Descriptors[residentIndex];
            var installed=final.Sound.Descriptors[residentIndex];
            RequireSourceResource(source,donor,donorDomain,incomingRead,incomingSample,incoming);
            RequireSourceResource(source,resident,residentDomain,targetRead,targetSample,originalResident);
            var equivalentDescriptor=equivalence.Single(r=>r.Id=="target-descriptor");
            var equivalentSample=equivalence.Single(r=>r.Id=="target-sample");
            if(equivalentDescriptor.Extent!=targetRead.Extent||equivalentDescriptor.ExpectedSha256!=targetRead.ExpectedSha256||
                equivalentSample.Extent!=targetSample.Extent||equivalentSample.ExpectedSha256!=targetSample.ExpectedSha256||
                !NativeGlobalSoundMergeLayout.Equivalent(incoming,originalResident)||
                !NativeGlobalSoundMergeLayout.Equivalent(incoming,installed)||
                !installed.CopyDescriptor().AsSpan().SequenceEqual(originalResident.CopyDescriptor()))
                throw new InvalidDataException("Sound residency requires equivalent controls/samples and the complete existing target descriptor.");
            NativeByteExtent descriptor=new(finalDomain,checked(final.SoundOffset+0x108+residentIndex*20),20);
            NativeByteExtent sample=new(finalDomain,checked(final.BankOffset+(long)installed.SampleOffset),installed.Sample.ByteLength);
            selections.Add(new(requirement.Id,requirement.Global,sourceIndex,residentIndex,descriptor,sample));
            Resident(requirement.Id+".descriptor",targetRead,descriptor);
            Resident(requirement.Id+".sample",targetSample,sample);
            List<string> dependencies=[requirement.Id+".descriptor",requirement.Id+".sample"];
            if(requirement.Global is int global)
            {
                var map=source.ResourceInputs["sound.map."+global].Single();
                if(!ReferenceEquals(map.Extent.Space,residentDomain)||map.Extent.Offset!=resident.SoundOffset+4+global||
                    map.Extent.ByteLength!=1||donor.Sound.Map[global]!=sourceIndex||resident.Sound.Map[global]!=residentIndex||
                    final.Sound.Map[global]!=residentIndex||source.ReadSource(map.Extent)[0]!=residentIndex)
                    throw new InvalidDataException("The actual global sound selector no longer retains its mapped resource.");
                string id="sound.map."+global;
                Resident(id,map,new(finalDomain,checked(final.SoundOffset+4+global),1));dependencies.Add(id);
            }
            nodes.Add(new(requirement.Id,NativeMobyDependencyKind.Sound,NativeMobyDependencyResolution.Aggregate,
                NativeMobyDependencySharing.Immutable,source.PackageRead.ExpectedSha256,dependencies.AsReadOnly(),equivalence));
        }

        // Only the Original four-byte model word is owned here. Any installed
        // field belongs to the separate family model materializer, not this binding.
        NativeByteExtent field=new(source.PackageRead.Extent.Space,checked(source.PackageRead.Extent.Offset+4),4);
        byte[] fieldBytes=source.ReadSource(field);
        var localSelection=selections.Single(s=>s.GlobalSelector is null);
        if(BinaryPrimitives.ReadUInt32LittleEndian(fieldBytes)!=0xFFFFFF2F||localSelection.SourceDescriptorIndex!=fieldBytes[0])
            throw new InvalidDataException("The chest source-local descriptor field changed before any model installation.");
        NativeFreshMobySoundLocalSelectorObligation local=new(new("source-local-selector",field,Hash(fieldBytes)),
            localSelection.SourceDescriptorIndex,localSelection.TargetDescriptorIndex);
        nodes.Add(new("sound.chest-local-selector",NativeMobyDependencyKind.Sound,NativeMobyDependencyResolution.Aggregate,
            NativeMobyDependencySharing.Immutable,local.SourceField.ExpectedSha256,[localSelection.Id],[local.SourceField]));
        const string root="sound.locked-chest-residency";
        nodes.Add(new(root,NativeMobyDependencyKind.Bundle,NativeMobyDependencyResolution.Aggregate,
            NativeMobyDependencySharing.Immutable,source.PackageRead.ExpectedSha256,
            [..selections.Select(s=>s.Id),"sound.chest-local-selector"],[]));
        var graph=NativeMobyDependencyClosureCompiler.Compile(nodes,[root],source.ReadSource);
        RequireFinalPreservations(graph,source,finalDomain,body);
        return(graph,selections.ToArray(),local);

        void Resident(string id,NativeMobyDependencyRead read,NativeByteExtent destination)=>nodes.Add(new(id,
            NativeMobyDependencyKind.Sound,NativeMobyDependencyResolution.DestinationResident,
            NativeMobyDependencySharing.Immutable,read.ExpectedSha256,[],[read],Preserved:[new(read.Id,0,destination)]));
    }

    private static int DescriptorIndex(NativeLevelSoundLayout layout,NativeAddressSpace domain,NativeMobyDependencyRead read)
    {
        long relative=read.Extent.Offset-layout.SoundOffset-0x108;
        if(!ReferenceEquals(read.Extent.Space,domain)||read.Extent.ByteLength!=20||relative<0||relative%20!=0||
            relative/20>=layout.Sound.Descriptors.Count)
            throw new InvalidDataException("A source sound descriptor does not occupy its complete indexed component record.");
        return checked((int)(relative/20));
    }

    private static void RequireSourceResource(NativeLockedChestFamilySource source,NativeLevelSoundLayout layout,
        NativeAddressSpace domain,NativeMobyDependencyRead descriptor,NativeMobyDependencyRead sample,NativeSoundDescriptor decoded)
    {
        if(!ReferenceEquals(sample.Extent.Space,domain)||sample.Extent.Offset!=layout.BankOffset+(long)decoded.SampleOffset||
            sample.Extent.ByteLength!=decoded.Sample.ByteLength||
            !source.ReadSource(descriptor.Extent).AsSpan().SequenceEqual(decoded.CopyDescriptor())||
            !source.ReadSource(sample.Extent).AsSpan().SequenceEqual(decoded.Sample.CopyBytes()))
            throw new InvalidDataException("An extracted sound read does not own its complete decoded descriptor and ADPCM sample.");
    }

    private static void RequireFinalPreservations(NativeMobyDependencyClosurePlan graph,NativeLockedChestFamilySource source,
        NativeAddressSpace finalDomain,ReadOnlySpan<byte> body)
    {
        if(graph.Installs.Count!=0||graph.References.Count!=0||graph.Nodes.Sum(n=>n.Preserved.Count)!=8||body.Length!=finalDomain.ByteLength)
            throw new InvalidDataException("The sound binding must preserve exactly eight owners without installs or resolved native references.");
        graph.RequirePreservedFinalBytes(finalDomain,body,source.ReadSource);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
