using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

internal static class HostedGrassCases
{
    private const uint Base=0x8007AA38,Start=0x8007DB34,Wrapper=0x8008B7A8,Decision=0x8007DC70,Exit=0x80087258;
    // Independent known 17-word Host recipe, not copied from a Core afterimage.
    private static readonly uint[] WrapperRecipe=[
        0x24020026,0x14820003,0,0x08022CFC,0,
        0x2402011B,0x14820003,0,0x08022D23,0,
        0x28820101,0x14400003,0x288200FF,0x0801F71C,0,0x0801F6D0,0];

    internal static void SourceFree(Action<bool,string> check)
    {
        var type=typeof(NativeMobyFamilyPassiveHostBinding);
        check(type.IsSealed&&!type.IsPublic&&type.GetConstructors().Length==0&&
            type.GetProperties().All(p=>p.SetMethod is null),"Passive Host proof is internal, sealed and immutable, with no caller constructor.");
        var issuers=type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
        check(issuers.Length==1&&issuers[0].Name=="Bind"&&issuers[0].GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
            typeof(NativeId65FreshWizardResearchHostPlan),typeof(NativeMobyFamilyAssetPlan)}),
            "Only actual captured Host and family objects issue passive dispatch evidence; no byte/hash/callback/approval issuer.");
        check(!type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Any(m=>
            new[]{"Publish","Write","CopyOverlay","CopyScene","CopyModel","CopyExecutable"}.Any(n=>m.Name.StartsWith(n,StringComparison.Ordinal))),
            "Dispatch proof has no destination payload or publication surface.");
        Reject<ArgumentNullException>(()=>NativeMobyFamilyPassiveHostBinding.Bind(null!,null!),check);

        // Neutral interpreter fixture: BEQ must decide before its delay slot clears r2.
        byte[] fixture=new byte[0x100];Put(fixture,0,0x24020001);Put(fixture,4,0x10400004);
        Put(fixture,8,0x24020000);Put(fixture,12,Jump(Base+0x40));Put(fixture,24,0x0C000000);
        var trace=NativeGrassFamilySourceDecoder.InspectPassiveDispatch(fixture,4,Base,Base+0x40,"neutral fixture");
        check(trace.ExecutedInstructionAddresses.SequenceEqual(new uint[]{Base,Base+4,Base+8,Base+12,Base+16})&&
            trace.CallInstructionCount==0&&trace.PropertiesReadCount==0&&trace.RuntimeActorWriteCount==0,
            "Shared interpreter follows pre-delay branch decision and records every executed delay word, without certifying fixture bytes.");
        foreach(var mutation in new[]{(0,0x0C000000u),(0,0x8E620000u),(0,0xAE620000u),(8,0x8E620000u),(0x40,0xAE620000u)})
        {
            byte[] bad=fixture.ToArray();Put(bad,mutation.Item1,mutation.Item2);
            Reject<InvalidDataException>(()=>NativeGrassFamilySourceDecoder.InspectPassiveDispatch(bad,4,Base,Base+0x40,"neutral refusal"),check);
        }
    }

    internal static async Task Source(string original,NativeId65FreshWizardResearchHostPlan host,
        NativeId65FreshWizardResearchHostPlan equalHost,NativeId65FreshWizardResearchHostPlan movedHost,
        NativeId65FreshWizardResearchHostPlan otherHost,byte[] originalTown,Action<bool,string> check)
    {
        check(Hash(originalTown)=="9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5",
            "Hosted Grass independent oracle begins with the retained exact original Town owner.");
        var assets=await NativeMobyFamilyAssetPlan.CaptureArtisansGrassAsync(original);
        var equalAssets=await NativeMobyFamilyAssetPlan.CaptureArtisansGrassAsync(original);
        string hostBefore=JsonSerializer.Serialize(host),assetsBefore=JsonSerializer.Serialize(assets);
        byte[] rowBefore=assets.CopyRow(),propertiesBefore=assets.CopyProperties(),packageBefore=assets.CopyPackage();
        var proof=NativeMobyFamilyPassiveHostBinding.Bind(host,assets);
        check(ReferenceEquals(proof.HostAncestor,host)&&ReferenceEquals(proof.FamilyAncestor,assets),
            "Passive evidence retains both actual object ancestors.");
        proof.RequireAncestry(host,assets);proof.RequireRetainedOwners();
        check(proof.HostPlanSha256==host.PlanSha256&&proof.FamilyAssetPlanSha256==assets.PlanSha256&&
            proof.OverlaySha256==host.OverlaySha256&&proof.SourceImageSha256==assets.SourceImageSha256&&
            proof.NativeClass==0x01F5&&proof.DonorTrueIndex==121&&proof.DonorLevelKey=="artisans",
            "Actual Host and original-source Grass identities, not a resident inventory row, bind the proof.");
        check(proof.SourceCertified&&proof.PassiveDispatchCompatible&&!proof.DestinationModelBound&&!proof.DestinationSceneBound&&
            !proof.NativeImportApproved&&!proof.CompleteNativeBehaviorProven&&!proof.RuntimeVerified&&!proof.PublicationApproved&&
            !proof.InstalledDispatchProven,"Only passive dispatch compatibility is proven; no destination, complete behavior, installed code or runtime approval.");
        check(proof.ClassLoadAddress==0x8007DB0C&&proof.ClassLoadWord==0x86640036&&proof.DispatchStartAddress==Start&&
            proof.WrapperAddress==Wrapper&&proof.WrapperByteLength==0x44&&proof.RetainedDecisionAddress==Decision&&
            proof.RetainedDecisionByteLength==0x128&&proof.DefaultLoopExitAddress==Exit,
            "The actual rewritten class entry, appended wrapper, retained high-class tree and default exit are explicit owners.");

        byte[] expectedOverlay=new byte[0x11000];originalTown.CopyTo(expectedOverlay,0);
        Put(expectedOverlay,0x30FC,0x08022DEA);Put(expectedOverlay,0x3100,0);
        for(int i=0;i<WrapperRecipe.Length;i++)Put(expectedOverlay,0x10D70+i*4,WrapperRecipe[i]);
        var expected=Oracle(expectedOverlay);
        check(proof.WrapperSha256=="f61a910589ba3dbdf971e588c30ea18c91493b47f7bd76283bd3cf943bb5bc80"&&
            proof.WrapperSha256==Hash(expectedOverlay.AsSpan(0x10D70,0x44))&&
            proof.RetainedDecisionSha256=="fc27e64af0f02e3617fd9b791759ea4b2663b9bfc6aeb19abb885f68d41e5b44"&&
            proof.DefaultLoopExitSha256=="edb2f325f44eafb8149e0f2b93912a9009e7aea08c77d17bdeb71bb6af20e372",
            "Independent wrapper recipe and retained original decision/default owners keep their exact pins.");
        check(proof.ExecutedInstructionAddresses.SequenceEqual(expected.Pcs)&&proof.ExecutedInstructionWords.SequenceEqual(expected.Words)&&
            proof.ExecutedTraceSha256==Hash(Bytes(expected.Words))&&proof.PlanSha256.Length==64,
            "Complete independently interpreted original-plus-wrapper path, words and digest equal actual Host evidence.");
        uint[] prefix=[Start,Start+4,Wrapper,Wrapper+4,Wrapper+8,Wrapper+20,Wrapper+24,Wrapper+28,
            Wrapper+40,Wrapper+44,Wrapper+48,Wrapper+52,Wrapper+56,Decision];
        check(expected.Pcs.Take(prefix.Length).SequenceEqual(prefix)&&
            !expected.Pcs.Contains(0x8008B3F0)&&!expected.Pcs.Contains(0x8008B48C)&&
            proof.CallInstructionCount==0&&proof.PropertiesReadCount==0&&proof.RuntimeActorWriteCount==0,
            "Grass excludes specialized 26/11B roles, executes wrapper delay slots and reaches default without calls, properties reads or actor writes.");
        check(expected.Pcs.All(pc=>pc>=Start&&pc<Start+8||pc>=Wrapper&&pc<Wrapper+0x44||pc>=Decision&&pc<Decision+0x128),
            "Every executed word belongs to patched entry, actual wrapper or retained high-class tree, never a Wizard callback.");

        check(equalHost.PlanSha256==host.PlanSha256&&otherHost.PlanSha256==host.PlanSha256&&
            movedHost.OverlaySha256==host.OverlaySha256&&movedHost.PlanSha256!=host.PlanSha256&&
            equalAssets.PlanSha256==assets.PlanSha256&&!ReferenceEquals(equalAssets,assets),
            "Ancestry negatives include genuinely equal Host and asset hashes plus a different scene with equal code.");
        foreach(var foreign in new[]{equalHost,movedHost,otherHost})
            Reject<InvalidDataException>(()=>proof.RequireAncestry(foreign,assets),check);
        Reject<InvalidDataException>(()=>proof.RequireAncestry(host,equalAssets),check);
        Reject<ArgumentNullException>(()=>NativeMobyFamilyPassiveHostBinding.Bind(host,null!),check);
        Reject<ArgumentNullException>(()=>NativeMobyFamilyPassiveHostBinding.Bind(null!,assets),check);
        var repeat=NativeMobyFamilyPassiveHostBinding.Bind(host,assets);
        var independentlyBound=NativeMobyFamilyPassiveHostBinding.Bind(equalHost,equalAssets);
        check(repeat.PlanSha256==proof.PlanSha256&&independentlyBound.PlanSha256==proof.PlanSha256&&
            NativeMobyFamilyPassiveHostBinding.Bind(movedHost,assets).PlanSha256!=proof.PlanSha256,
            "Equal ancestry facts can issue a separate deterministic proof without rebinding the earlier actual pair; scene changes retain identity.");
        independentlyBound.RequireAncestry(equalHost,equalAssets);
        string proofBefore=JsonSerializer.Serialize(proof);
        foreach(var list in new[]{proof.ExecutedInstructionAddresses,proof.ExecutedInstructionWords})
        {
            check(list is not IList<uint> writable||writable.IsReadOnly,"Proof instruction collections are read-only.");
            if(list is IList<uint> mutable)Reject<NotSupportedException>(()=>mutable[0]^=1,check);
        }
        byte[] hostCopy=host.CopyOverlay(),row=assets.CopyRow(),props=assets.CopyProperties(),package=assets.CopyPackage();
        hostCopy[0x30FC]^=1;row[0]^=1;props[0]^=1;package[0]^=1;
        proof.RequireRetainedOwners();proof.RequireAncestry(host,assets);
        check(JsonSerializer.Serialize(proof)==proofBefore&&JsonSerializer.Serialize(host)==hostBefore&&
            JsonSerializer.Serialize(assets)==assetsBefore&&assets.CopyRow().SequenceEqual(rowBefore)&&
            assets.CopyProperties().SequenceEqual(propertiesBefore)&&assets.CopyPackage().SequenceEqual(packageBefore),
            "Mutable copies, foreign ancestry and repeat checks leave proof and every captured owner unchanged.");
        Console.WriteLine($"HOSTED_GRASS_PASSIVE_DISPATCH_SOURCE_OK words={expected.Words.Length} trace={proof.ExecutedTraceSha256} destinationModel=0 destinationScene=0 import=0 installed=0 runtime=0 publication=0");
    }

    // Test-owned scalar oracle over original Town plus the explicit wrapper only.
    private static (uint[] Pcs,uint[] Words) Oracle(byte[] image)
    {
        uint[] registers=new uint[32];registers[4]=0x01F5;List<uint> pcs=[],words=[];uint pc=Start;
        for(int budget=0;budget<64&&pc!=Exit;budget++)
        {
            uint word=Read(pc);pcs.Add(pc);words.Add(word);uint op=word>>26;
            if(op is 2 or 4 or 5)
            {
                bool branch=op!=2;bool equal=registers[(word>>21)&31]==registers[(word>>16)&31];
                uint next=branch?((op==4?equal:!equal)?unchecked(pc+4+(uint)((short)word*4)):pc+8):
                    ((pc+4)&0xF0000000)|((word&0x03FFFFFF)<<2);
                uint delay=Read(pc+4);pcs.Add(pc+4);words.Add(delay);Scalar(delay);pc=next;
            }
            else{Scalar(word);pc+=4;}
        }
        if(pc!=Exit)throw new InvalidDataException("Independent Grass path did not reach the exact default exit.");
        foreach(uint word in words.Concat(Enumerable.Range(0,6).Select(i=>Read(Exit+(uint)i*4))))
        {
            uint op=word>>26,rs=(word>>21)&31;
            bool call=op==3||op==0&&(word&63)==9;
            bool load=op is >=0x20 and <=0x27 or 0x30 or 0x31 or 0x32 or 0x33;
            bool store=op is >=0x28 and <=0x2E or 0x38 or 0x39 or 0x3A or 0x3B;
            if(call||load&&rs==19&&(word&0xFFFF)==0||store&&rs==19)
                throw new InvalidDataException("Independent passive path/exit gained a call, properties read or actor write.");
        }
        return(pcs.ToArray(),words.ToArray());
        uint Read(uint address)=>BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(checked((int)(address-Base)),4));
        void Scalar(uint word)
        {
            if(word==0)return;uint op=word>>26,rs=(word>>21)&31,rt=(word>>16)&31;int value=(short)word;
            if(op==9)registers[rt]=unchecked(registers[rs]+(uint)value);
            else if(op==10)registers[rt]=(int)registers[rs]<value?1u:0u;
            else throw new InvalidDataException("Independent passive path reached a non-scalar instruction.");
            registers[0]=0;
        }
    }
    private static byte[] Bytes(uint[] words)
    {byte[] bytes=new byte[words.Length*4];for(int i=0;i<words.Length;i++)Put(bytes,i*4,words[i]);return bytes;}
    private static uint Jump(uint address)=>0x08000000|((address>>2)&0x03FFFFFF);
    private static void Put(Span<byte> bytes,int at,uint word)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..],word);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Hosted Grass rejected "+typeof(T).Name);return;}throw new InvalidDataException("Hosted Grass expected "+typeof(T).Name);}
}
