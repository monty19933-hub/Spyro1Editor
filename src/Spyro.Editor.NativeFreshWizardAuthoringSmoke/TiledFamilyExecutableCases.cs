using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

// Frozen source-only composition contract. EXE R07 and the independent tiled
// Body oracle predate this issuer; neither an exporter nor runtime proof enters.
internal static class TiledFamilyExecutableCases
{
    private const string ExecutableHash="81501f07ec9ce336089ec5a92f81bf51569aed723ac4f8ba1cf85ba0ef69a431";
    private const string BodyHash="c8c85e71b633e29ce573dddccb04a4a0adcd6696d292658304328e435dfaa9ca";
    private const string PriorBodyHash="2d8c1bf1af35c8e5522b239c564d36c79c828539d133278c461b6178b53bb504";
    private const string OriginalHash="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private const int DataAt=0xE6000,DataLength=0x1000,SceneAt=0x102000,SceneLength=0x3800;
    private static readonly (int At,int Length,string Hash)[] Consumers=[
        (0x3558,0x4D8,"82ceb2a2a0ae9ddcc86caca26ceed4b24e2c6e1187c061750fec4e186f6f7ea3"),
        (0x6088,0x1CC,"8fcb62ff217d641423a6255ea234011e8f4b5fe7769c53ae78277ad8373609ca"),
        (0x6254,0x180,"a51ae4c014934f0a456520247ebcb1333c77a92670ebf18bbcada0162e8a7fb0"),
        (0x64CC,0xC4,"1acbe741fd6f43ba61514452be82e85ae0cdfaad94e693b6462781e938aada49")];

    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshTiledFamilyExecutablePlan);
        check(!type.IsPublic&&type.IsSealed&&
            type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).All(c=>c.IsPrivate)&&
            type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Length==0,
            "Tiled family executable is a closed internal composition with no public exporter or arbitrary-byte ingress.");
        var compose=type.GetMethods(BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.DeclaredOnly).Single(m=>m.Name=="Compose");
        check(compose.IsAssembly&&compose.ReturnType==type&&compose.GetParameters().Select(p=>p.ParameterType).SequenceEqual(
            new[]{typeof(NativeFreshTiledFamilyBodyPlan),typeof(NativeFreshLockedChestExperimentalExecutable)}),
            "Only the actual new Body and actual previous experimental executable can issue this source composition.");
        foreach(var item in new[]{("BodyAncestor",typeof(NativeFreshTiledFamilyBodyPlan)),
            ("ParentExecutableAncestor",typeof(NativeFreshLockedChestExperimentalExecutable)),
            ("TerrainAncestor",typeof(NativeFreshTiledFamilyTerrainRevision)),
            ("PlacementAncestor",typeof(NativeFreshLockedChestPlacementPlan)),
            ("HostAncestor",typeof(NativeFreshLockedChestResearchHostExtension)),
            ("DocumentAncestor",typeof(NativeFreshLevelDocumentIntent))})
        {
            var property=type.GetProperty(item.Item1,BindingFlags.NonPublic|BindingFlags.Instance);
            check(property is not null&&property.PropertyType==item.Item2&&property.GetMethod?.IsAssembly==true&&
                property.GetSetMethod(true) is null,"Read-only actual ancestor "+item.Item1);
        }
        Reject<ArgumentNullException>(()=>NativeFreshTiledFamilyExecutablePlan.Compose(null!,null!),check);
        Console.WriteLine("TILED_FAMILY_EXECUTABLE_FREE_OK sourceOpened=false runtime=false publication=false");
    }

    internal static NativeFreshTiledFamilyExecutablePlan Source(NativeFreshTiledFamilyBodyPlan body,
        NativeFreshLockedChestExperimentalExecutable parent,Action<bool,string> check)
    {
        var terrain=body.TerrainAncestor;var placement=body.PlacementAncestor;var host=body.HostAncestor;
        var document=terrain.IntentAncestor.DocumentAncestor;var oldBody=placement.BodyAncestor;
        byte[] level=body.CopyLevelData(),prior=placement.CopyLevelData(),scene=placement.CopyScene();
        byte[] executable=parent.CopyExecutable(),overlay=host.CopyOverlay(),initializer=host.CopyInitializer();
        byte[] prototype=host.CopyExecutablePrototype();
        string parentPlan=parent.PlanSha256,bodyPlan=body.PlanSha256,oldBodyPlan=oldBody.PlanSha256;
        var source=host.FamilySourceAncestor;
        var domain=source.ResourceInputs["key.special-renderer-and-global-routines"].Single().Extent.Space;
        byte[] original=source.ReadSource(new(domain,0,domain.ByteLength));
        check(Hash(level)==BodyHash&&Hash(prior)==PriorBodyHash&&Hash(executable)==ExecutableHash&&Hash(original)==OriginalHash&&
            level.Length==0x20F000&&executable.Length==0x66000&&ReferenceEquals(parent.PlacementAncestor,placement)&&
            ReferenceEquals(parent.HostAncestor,host),"Actual incoming Body, prior child, parent executable and Original match independent complete pins.");

        var plan=NativeFreshTiledFamilyExecutablePlan.Compose(body,parent);
        plan.RequireAncestry(body,parent);plan.RequireExactOutputs(level,executable);
        check(ReferenceEquals(plan.BodyAncestor,body)&&ReferenceEquals(plan.ParentExecutableAncestor,parent)&&
            ReferenceEquals(plan.TerrainAncestor,terrain)&&ReferenceEquals(plan.PlacementAncestor,placement)&&
            ReferenceEquals(plan.HostAncestor,host)&&ReferenceEquals(plan.DocumentAncestor,document)&&
            plan.CanonicalDocumentSha256==terrain.CanonicalDocumentSha256&&
            plan.CanonicalDocumentSha256==Hash(NativeFreshLevelDocumentCodec.SerializeCanonical(document))&&
            plan.LevelDataSha256==BodyHash&&plan.ExecutableSha256==ExecutableHash&&IsHash(plan.PlanSha256),
            "Composition binds the complete actual document/terrain/new Body/parent executable/Host tuple.");
        check(plan.CopyLevelData().SequenceEqual(level)&&plan.CopyExecutable().SequenceEqual(executable)&&
            plan.RevertExactLevelData(level).SequenceEqual(prior)&&plan.RevertExactExecutable(executable).SequenceEqual(executable)&&
            plan.RevertExactOriginal(executable).SequenceEqual(original)&&parent.RevertExactOriginal(executable).SequenceEqual(original),
            "Complete issued outputs and immediate child/parent plus Original inverses remain exact without a second byte writer.");
        check(level.AsSpan(SceneAt,SceneLength).SequenceEqual(scene)&&body.CopyScene().SequenceEqual(scene)&&
            level.AsSpan(0,DataAt).SequenceEqual(prior.AsSpan(0,DataAt))&&
            level.AsSpan(DataAt+DataLength).SequenceEqual(prior.AsSpan(DataAt+DataLength)),
            "Entire authored Scene, header and non-data complement match the same actual placed ancestor.");
        for(int i=0;i<8;i++)
        {
            int at=Word(prior,i*8),length=Word(prior,i*8+4);
            check(Word(level,i*8)==at&&Word(level,i*8+4)==length&&
                (i==1||level.AsSpan(at,length).SequenceEqual(prior.AsSpan(at,length))),
                "Every retained descriptor and complete non-data subfile remains exact "+i);
        }
        check(body.RuntimeMemory.UsedLevelDataBytes==0xCB8&&body.RuntimeMemory.LevelDataSectorPaddingBytes==0x348&&
            body.RuntimeMemory.ModelRamStart==0x8008D6F0u&&body.RuntimeMemory.SceneRamStart==0x800A86F0u&&
            body.RuntimeMemory.SceneReadEnd==0x800ABEF0u&&oldBody.RuntimeMemory.UsedLevelDataBytes==0xA6C&&
            !body.RuntimeMemory.NativeSourceCertified&&!body.RuntimeMemory.RuntimeFitProven&&!body.RuntimeMemory.PeakTransientFitProven,
            "Revised CB8 data shifts model/Scene accounting; the older A6C certificate and neutral report retain their separate scope.");
        check(plan.Evidence.Count==Consumers.Length,"Only the four retained parser/model/Scene consumer windows accompany this composition.");
        foreach(var consumer in Consumers)
        {
            var evidence=plan.Evidence.Single(e=>e.Offset==consumer.At&&e.ByteLength==consumer.Length);
            check(evidence.Domain=="EXE"&&evidence.Sha256==consumer.Hash&&Hash(original.AsSpan(consumer.At,consumer.Length))==consumer.Hash&&
                executable.AsSpan(consumer.At,consumer.Length).SequenceEqual(original.AsSpan(consumer.At,consumer.Length)),
                "Actual complete Original/current native consumer window remains exact "+consumer.At.ToString("X"));
        }
        check(parent.KnownSelectorCount==7&&parent.KnownDeterministicSelectorSubtotal==20&&parent.ExperimentalTreasureTarget==20&&
            Half(executable,0x5FC7E)==20&&Half(prototype,0x5FC7E)==10&&parent.Rows.Count==9&&
            parent.Rows[1].Premise=="retained-passive-dispatch-zero"&&parent.Rows[2].Premise=="experimental-key-zero-not-certified"&&
            parent.Rows[1].AssumedContribution==0&&parent.Rows[2].AssumedContribution==0&&
            parent.GrassPassiveDispatchPreserved&&!parent.KeyZeroContributionProven&&!parent.CompleteStaticCensusProven,
            "Unchanged parent carries selector20, retained passive Grass and explicit unproven key-zero/whole-census assumptions.");
        check(plan.SourceCertified&&plan.Experimental&&!plan.FullNativeLoaderProven&&!plan.KeyZeroContributionProven&&
            !plan.CompleteStaticCensusProven&&!plan.RuntimeDropGuarantee&&!plan.CompleteNativeDependencyClosureProven&&
            !plan.PeakRuntimeMemoryProven&&!plan.RuntimeVerified&&!plan.PublicationApproved&&!body.FinalExecutableIssued&&
            !terrain.BodyIssued&&!parent.RuntimeVerified&&!parent.PublicationApproved,
            "Source composition does not grant full loading, census, drops, peak memory, closure, runtime or publication claims.");

        var equalBody=NativeFreshTiledFamilyBodyPlan.Compose(terrain);
        var equalParent=NativeFreshLockedChestExperimentalExecutable.Compose(placement,host);
        check(!ReferenceEquals(equalBody,body)&&equalBody.PlanSha256==body.PlanSha256&&equalBody.CopyLevelData().SequenceEqual(level)&&
            !ReferenceEquals(equalParent,parent)&&equalParent.PlanSha256==parent.PlanSha256&&equalParent.CopyExecutable().SequenceEqual(executable),
            "Foreign Body/parent controls are actual independent equal-byte, equal-fingerprint issuances.");
        Reject<InvalidDataException>(()=>plan.RequireAncestry(equalBody,parent),check);
        Reject<InvalidDataException>(()=>plan.RequireAncestry(body,equalParent),check);
        var repeat=NativeFreshTiledFamilyExecutablePlan.Compose(body,parent);
        var equalOwn=NativeFreshTiledFamilyExecutablePlan.Compose(equalBody,equalParent);
        check(repeat.PlanSha256==plan.PlanSha256&&equalOwn.PlanSha256==plan.PlanSha256&&
            ReferenceEquals(equalOwn.BodyAncestor,equalBody)&&ReferenceEquals(equalOwn.ParentExecutableAncestor,equalParent),
            "Equal reissuance is deterministic but each result retains its own actual tuple.");
        Reject<InvalidDataException>(()=>equalOwn.RequireAncestry(body,parent),check);
        var foreignPlacement=NativeFreshLockedChestPlacementPlan.ComposeWithRewardDestinations(oldBody,placement.Intent,
            placement.RewardDestinationsAncestor!);
        var foreignParent=NativeFreshLockedChestExperimentalExecutable.Compose(foreignPlacement,host);
        check(!ReferenceEquals(foreignPlacement,placement)&&foreignPlacement.CopyLevelData().SequenceEqual(prior)&&
            foreignParent.CopyExecutable().SequenceEqual(executable),"Mismatched incoming parent preserves every byte but owns a foreign placement instance.");
        Reject<InvalidDataException>(()=>NativeFreshTiledFamilyExecutablePlan.Compose(body,foreignParent),check);
        Reject<ArgumentNullException>(()=>NativeFreshTiledFamilyExecutablePlan.Compose(null!,parent),check);
        Reject<ArgumentNullException>(()=>NativeFreshTiledFamilyExecutablePlan.Compose(body,null!),check);
        Reject<ArgumentNullException>(()=>plan.RequireAncestry(null!,parent),check);
        Reject<ArgumentNullException>(()=>plan.RequireAncestry(body,null!),check);
        Reject<InvalidDataException>(()=>plan.RequireExactOutputs(prior,executable),check);
        Reject<InvalidDataException>(()=>plan.RequireExactOutputs(level,prototype),check);
        foreach(int at in new[]{0,8,0x7FF,DataAt,DataAt+0xCB7,DataAt+0xCB8,DataAt+0xFFF,
            SceneAt,SceneAt+0x12E8,SceneAt+0x3394,SceneAt+SceneLength-1,level.Length-1})
        {
            byte[] corrupt=level.ToArray();corrupt[at]^=1;
            Reject<InvalidDataException>(()=>plan.RequireExactOutputs(corrupt,executable),check);
            Reject<InvalidDataException>(()=>plan.RevertExactLevelData(corrupt),check);
        }
        foreach(int at in new[]{0,0x3558,0x6088,0x6254,0x64CC,0x4B098,0x5FC7E,0x65FFF})
        {
            byte[] corrupt=executable.ToArray();corrupt[at]^=1;
            Reject<InvalidDataException>(()=>plan.RequireExactOutputs(level,corrupt),check);
            Reject<InvalidDataException>(()=>plan.RevertExactExecutable(corrupt),check);
            Reject<InvalidDataException>(()=>plan.RevertExactOriginal(corrupt),check);
        }
        foreach(byte[] bad in new byte[][]{[],level[..^1],[..level,(byte)0]})
            Reject<InvalidDataException>(()=>plan.RevertExactLevelData(bad),check);
        foreach(byte[] bad in new byte[][]{[],executable[..^1],[..executable,(byte)0]})
            Reject<InvalidDataException>(()=>plan.RevertExactOriginal(bad),check);
        plan.CopyLevelData()[0]^=1;plan.CopyExecutable()[0]^=1;plan.RevertExactLevelData(level)[0]^=1;
        plan.RevertExactExecutable(executable)[0]^=1;plan.RevertExactOriginal(executable)[0]^=1;
        check(plan.CopyLevelData().SequenceEqual(level)&&plan.CopyExecutable().SequenceEqual(executable)&&
            plan.RevertExactLevelData(level).SequenceEqual(prior)&&plan.RevertExactExecutable(executable).SequenceEqual(executable)&&
            plan.RevertExactOriginal(executable).SequenceEqual(original)&&body.PlanSha256==bodyPlan&&parent.PlanSha256==parentPlan&&
            oldBody.PlanSha256==oldBodyPlan&&body.CopyLevelData().SequenceEqual(level)&&parent.CopyExecutable().SequenceEqual(executable)&&
            placement.CopyLevelData().SequenceEqual(prior)&&host.CopyOverlay().SequenceEqual(overlay)&&
            host.CopyInitializer().SequenceEqual(initializer)&&host.CopyExecutablePrototype().SequenceEqual(prototype),
            "Detached output/inverse mutations and refusals preserve the complete new/old Body, parent executable and Host owners.");
        Console.WriteLine($"TILED_FAMILY_EXECUTABLE_SOURCE_OK body={plan.LevelDataSha256} exe={plan.ExecutableSha256} immediateInverse=true fullNativeLoader=false runtime=false publication=false");
        return plan;
    }

    private static int Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
    private static ushort Half(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static bool IsHash(string value)=>value.Length==64&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Tiled family executable refused "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
