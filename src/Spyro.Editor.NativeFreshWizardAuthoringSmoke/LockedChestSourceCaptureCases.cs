using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

// Compares the new closed production capture with the retained, independently
// exercised smoke acquisition. Equal bytes never substitute for actual owners.
internal static class LockedChestSourceCaptureCases
{
    private static readonly int[] Lengths=[0x29E800,0x12800,0x2E2000,0xF800,0x66000];
    private static readonly string[] Hashes=[
        "c140bd422471c66cd57bd85b29912819f798f5b080779153674714889306c9d3",
        "299f4822489c48736c9f46bb9556e82952fc1693f5f9f42bc2e6415c1908aebc",
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0",
        "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5",
        "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"];

    internal static async Task Free(Action<bool,string> check)
    {
        var type=typeof(NativeLockedChestFamilySource);
        check(type.IsSealed&&!type.IsPublic&&type.GetConstructors(BindingFlags.Instance|BindingFlags.Public).Length==0&&
            type.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.DeclaredOnly).Length==0,
            "Chest capture remains a closed internal source, without public construction or source-byte authority.");
        var capture=type.GetMethod("CaptureOriginalAsync",BindingFlags.Static|BindingFlags.NonPublic);
        check(capture is not null&&capture.ReturnType==typeof(Task<NativeLockedChestFamilySource>)&&
            capture.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(string),typeof(NativeWizardFamilyAssetPlan),typeof(CancellationToken)}),
            "Production capture accepts only a path and actual Wizard owner, not streams, readers, hashes or domains.");
        using(var cancel=new CancellationTokenSource())
        {
            cancel.Cancel();
            await Reject<OperationCanceledException>(()=>NativeLockedChestFamilySource.CaptureOriginalAsync(null!,null!,cancel.Token),check);
            await Reject<OperationCanceledException>(()=>NativeLockedChestFamilySource.CaptureOriginalAsync("\0",null!,cancel.Token),check);
        }
        await Reject<ArgumentNullException>(()=>NativeLockedChestFamilySource.CaptureOriginalAsync("\0",null!),check);

        // Existing neutral decoder controls execute no source acquisition and
        // cannot manufacture a pinned complete Original owner.
        var domains=Enumerable.Range(0,5).Select(i=>new NativeAddressSpace(i==4?NativeAddressKind.ExecutableFile:NativeAddressKind.LevelData,
            "neutral-chest-source-"+i,NativeAddressRevision.Original,Lengths[i])).ToArray();
        foreach(int i in Enumerable.Range(0,5))
        foreach(string damage in new[]{"null","kind","revision","length"})
        {
            var bad=domains.ToArray();
            bad[i]=damage=="null"?null!:new NativeAddressSpace(damage=="kind"?NativeAddressKind.Scene:domains[i].Kind,
                "neutral-invalid-source",damage=="revision"?NativeAddressRevision.Final:NativeAddressRevision.Original,
                damage=="length"?Lengths[i]+1:Lengths[i]);
            bool read=false;
            if(damage=="null")Reject<ArgumentNullException>(()=>Decode(bad,e=>{read=true;return [];}),check);
            else Reject<InvalidDataException>(()=>Decode(bad,e=>{read=true;return [];}),check);
            check(!read,"Invalid complete source-domain shape refuses before a reader is invoked.");
        }
        Reject<ArgumentNullException>(()=>Decode(domains,null!),check);
        foreach(byte[]? bytes in new byte[]?[]{null,[],new byte[Lengths[0]-1],new byte[Lengths[0]+1],new byte[Lengths[0]]})
            Reject<InvalidDataException>(()=>Decode(domains,_=>bytes!),check);
        Console.WriteLine("FRESH_CHEST_PRODUCTION_SOURCE_FREE_OK sourceIo=0 issuerCreated=false");
    }

    internal static async Task Source(string image,NativeWizardFamilyAssetPlan wizard,
        NativeId65FreshWizardResearchExecutablePlan current,NativeLockedChestFamilySource previous,
        NativeFreshMobyFamilyCompositionPlan previousChild,NativeId65FreshWizardResearchFallbackResourceSource fallback,
        Action<bool,string> check)
    {
        var captured=await NativeLockedChestFamilySource.CaptureOriginalAsync(image,wizard);
        captured.RequireWizardOriginalOwner(wizard);previous.RequireWizardOriginalOwner(wizard);
        var oldDomains=Domains(previous);var domains=Domains(captured);
        check(!ReferenceEquals(captured,previous)&&ReferenceEquals(domains[4],wizard.OriginalExecutableDomain)&&
            ReferenceEquals(oldDomains[4],domains[4]),"Production source uses the actual existing Wizard Original executable domain.");
        for(int i=0;i<5;i++)
        {
            byte[] before=previous.ReadSource(new(oldDomains[i],0,Lengths[i]));
            byte[] after=captured.ReadSource(new(domains[i],0,Lengths[i]));
            check(domains[i].ByteLength==Lengths[i]&&domains[i].Revision==NativeAddressRevision.Original&&
                domains[i].OwnerIdentity==oldDomains[i].OwnerIdentity&&Hash(after)==Hashes[i]&&after.SequenceEqual(before),
                "Complete production source role "+i+" retains its independent literal identity and old source bytes/label.");
            var foreign=new NativeAddressSpace(domains[i].Kind,domains[i].OwnerIdentity,domains[i].Revision,domains[i].ByteLength);
            Reject<InvalidDataException>(()=>captured.ReadSource(new(foreign,0,Lengths[i])),check);
            foreach(int at in new[]{0,Lengths[i]/2,Lengths[i]-1})
            {
                byte[] damaged=after.ToArray();damaged[at]^=1;
                Reject<InvalidDataException>(()=>Decode(domains,e=>ReferenceEquals(e.Space,domains[i])?
                    damaged.AsSpan((int)e.Offset,(int)e.ByteLength).ToArray():captured.ReadSource(e)),check);
            }
            after[0]^=1;before[^1]^=1;
            check(Hash(captured.ReadSource(new(domains[i],0,Lengths[i])))==Hashes[i],"Source read copies are detached.");
        }
        var oldReads=Reads(previous).ToArray();var reads=Reads(captured).ToArray();
        check(reads.Length==oldReads.Length,"No declared source read is dropped during production capture.");
        for(int i=0;i<reads.Length;i++)
        {
            var before=oldReads[i];var after=reads[i];
            check(before.Id==after.Id&&before.ExpectedSha256==after.ExpectedSha256&&
                Array.IndexOf(oldDomains,before.Extent.Space)==Array.IndexOf(domains,after.Extent.Space)&&
                before.Extent.Offset==after.Extent.Offset&&before.Extent.ByteLength==after.Extent.ByteLength&&
                previous.ReadSource(before.Extent).SequenceEqual(captured.ReadSource(after.Extent)),
                "Each retained private/schema/handler/resource/global read has the same exact complete source extent and payload.");
        }
        Reject<ArgumentNullException>(()=>captured.RequireWizardOriginalOwner(null!),check);
        var equalWizard=await NativeWizardFamilyAssetPlan.CaptureAsync(image);
        check(equalWizard.PlanSha256==wizard.PlanSha256&&equalWizard.CopySourceAssetBlock().SequenceEqual(wizard.CopySourceAssetBlock())&&
            !ReferenceEquals(equalWizard.OriginalExecutableDomain,wizard.OriginalExecutableDomain),
            "Foreign Wizard is genuinely equal but has another actual Original executable owner.");
        Reject<InvalidDataException>(()=>captured.RequireWizardOriginalOwner(equalWizard),check);
        var foreignDomains=domains.ToArray();foreignDomains[4]=equalWizard.OriginalExecutableDomain;
        var foreignSource=Decode(foreignDomains,e=>ReferenceEquals(e.Space,foreignDomains[4])?
            equalWizard.ReadPrivateSource(e):captured.ReadSource(e));
        foreignSource.RequireWizardOriginalOwner(equalWizard);
        Reject<InvalidDataException>(()=>foreignSource.RequireWizardOriginalOwner(wizard),check);
        using(var cancel=new CancellationTokenSource())
        {
            cancel.Cancel();
            await Reject<OperationCanceledException>(()=>NativeLockedChestFamilySource.CaptureOriginalAsync("\0",wizard,cancel.Token),check);
        }
        await Reject<ArgumentException>(()=>NativeLockedChestFamilySource.CaptureOriginalAsync("\0",wizard),check);
        await Reject<ArgumentException>(()=>NativeLockedChestFamilySource.CaptureOriginalAsync("",wizard),check);

        var parent=current.FamilyCompositionAncestor!;
        var oldHost=NativeFreshLockedChestResearchHostExtension.Compose(current,previous);
        var host=NativeFreshLockedChestResearchHostExtension.Compose(current,captured);
        var oldSound=NativeFreshMobySoundResidencyBinding.BindLockedChest(previous,parent);
        var sound=NativeFreshMobySoundResidencyBinding.BindLockedChest(captured,parent);
        var child=NativeFreshMobyFamilyCompositionPlan.ComposeWithLockedChestResources(parent,sound,fallback);
        check(host.PlanSha256==oldHost.PlanSha256&&host.CopyOverlay().SequenceEqual(oldHost.CopyOverlay())&&
            host.CopyInitializer().SequenceEqual(oldHost.CopyInitializer())&&host.CopyExecutablePrototype().SequenceEqual(oldHost.CopyExecutablePrototype())&&
            sound.PlanSha256==oldSound.PlanSha256&&sound.Graph.Fingerprint==oldSound.Graph.Fingerprint&&
            child.PlanSha256==previousChild.PlanSha256&&child.CopyLevelData().SequenceEqual(previousChild.CopyLevelData())&&
            child.CopyScene().SequenceEqual(previousChild.CopyScene())&&
            child.CopyInverseResourceAncestorBody().SequenceEqual(parent.CopyLevelData()),
            "Production capture preserves old complete Host/resource payloads, plan identities and immediate resource inverse.");
        Reject<InvalidDataException>(()=>oldHost.RequireAncestry(current,captured),check);
        Reject<InvalidDataException>(()=>oldSound.RequireAncestry(captured,parent),check);
        Reject<InvalidDataException>(()=>previousChild.RequireLockedChestResourceAncestry(parent,captured),check);
        child.RequireLockedChestResourceAncestry(parent,captured);captured.RequireWizardOriginalOwner(wizard);
        check(!host.RuntimeVerified&&!host.PublicationApproved&&!host.CompleteNativeDependencyClosureProven,
            "Source capture and identity parity grant no generalized native/runtime/publication proof.");
        Console.WriteLine($"FRESH_CHEST_PRODUCTION_SOURCE_OK package={captured.PackageRead.ExpectedSha256} oldHost={oldHost.PlanSha256} newHost={host.PlanSha256} oldResources={previousChild.PlanSha256} newResources={child.PlanSha256} body={child.LevelDataSha256} runtime=false outputWritten=false");
    }

    private static NativeAddressSpace[] Domains(NativeLockedChestFamilySource source)=>[
        source.PrivateRows[0].Row.Extent.Space,source.HandlerReads[0].Extent.Space,
        source.ResourceInputs["key.particle-descriptor"].Single().Extent.Space,
        source.ResourceInputs["key.native-handler"].Single().Extent.Space,source.MutableGlobals[0].ExecutableDomain];
    private static IEnumerable<NativeMobyDependencyRead> Reads(NativeLockedChestFamilySource source)=>
        source.PrivateRows.SelectMany(r=>new[]{r.Row,r.Properties}).Append(source.PackageRead).Concat(source.HandlerReads)
            .Concat(source.ResourceInputs.Values.SelectMany(r=>r)).Concat(source.MutableGlobals.SelectMany(g=>g.Consumers));
    private static NativeLockedChestFamilySource Decode(NativeAddressSpace[] domains,Func<NativeByteExtent,byte[]> reader)=>
        NativeLockedChestFamilySource.DecodeOriginal(domains[0],domains[1],domains[2],domains[3],domains[4],reader);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Expected production source refusal "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    private static async Task Reject<T>(Func<Task> action,Action<bool,string> check)where T:Exception
    {try{await action();}catch(T){check(true,"Expected production source refusal "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
