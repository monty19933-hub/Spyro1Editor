using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class FamilyChestHostCases
{
    private const int OldOverlayLength=0x11000,OverlayLength=0x12000,UsedOverlayLength=0x11BC0;
    private const int InitializerOffset=0x4B098,InitializerLength=0x98;
    private const string CurrentExecutableHash="a0eb46c2735355c05d66cc33492ed70c6c2174e77e8e8eb6c2ca8fae0f3fc8dc";
    // Independently frozen source reference450, before the Core producer.
    private const string OverlayHash="9d828474ed59e411b570e011e4b4156037e628c4cc9596cb476e7e2abdb995b2",
        InitializerHash="4ba757dcac9e8a476c97a6767f82e5fe8bcdcd6511f5464ba1d87e981df16cf0",
        ExecutableHash="ce341af853579b6b17a18222c08d2c17dbf6a9a572be672cf39edd5a6699ad20";

    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshLockedChestResearchHostExtension);
        var constructors=type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        check(type.IsSealed&&!type.IsPublic&&constructors.Length>0&&constructors.All(c=>c.IsPrivate)&&
            type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly).Length==0,
            "Chest Host extension is internal and sealed, with private construction and no new public API.");
        foreach(var entry in new[]{(Name:"CurrentExecutableAncestor",Type:typeof(NativeId65FreshWizardResearchExecutablePlan)),
            (Name:"FamilySourceAncestor",Type:typeof(NativeLockedChestFamilySource)),
            (Name:"HostAncestor",Type:typeof(NativeId65FreshWizardResearchHostPlan))})
        {
            var property=type.GetProperty(entry.Name,BindingFlags.NonPublic|BindingFlags.Instance);
            check(property is not null&&property.PropertyType==entry.Type&&property.GetMethod?.IsAssembly==true&&
                property.GetSetMethod(true) is null,"Actual "+entry.Name+" is an internal read-only reference.");
        }
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestResearchHostExtension.Compose(null!,null!),check);
        Console.WriteLine("NATIVE_FRESH_CHEST_HOST_EXTENSION_FREE_OK nativeAdmission=false");
    }

    internal static void Source(NativeId65FreshWizardResearchExecutablePlan current,
        NativeLockedChestFamilySource source,Action<bool,string> check)
    {
        RequireOraclePins();
        var host=current.BodyAncestor.HostAncestor;
        var family=current.FamilyCompositionAncestor??throw new InvalidDataException("Chest Host fixture needs its actual P2 family executable.");
        check(current.MixedResourceAncestor is not null&&ReferenceEquals(family.BaseBodyAncestor,current.BodyAncestor),
            "Source fixture is the retained mixed P2 family executable, not an equal-byte substitute.");
        byte[] oldOverlay=host.CopyOverlay(),oldInitializer=host.CopyInitializerPatch(),oldExecutable=current.CopyExecutable();
        byte[] oldBody=family.CopyLevelData(),oldScene=family.CopyScene();
        check(oldOverlay.Length==OldOverlayLength&&oldInitializer.Length==InitializerLength&&
            oldExecutable.Length==0x66000&&Hash(oldExecutable)==CurrentExecutableHash&&
            oldExecutable.AsSpan(InitializerOffset,InitializerLength).SequenceEqual(oldInitializer),
            "Independent current-family executable pin and complete existing Host initializer are exact.");
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestResearchHostExtension.Compose(null!,source),check);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestResearchHostExtension.Compose(current,null!),check);

        var extension=NativeFreshLockedChestResearchHostExtension.Compose(current,source);
        byte[] overlay=extension.CopyOverlay(),initializer=extension.CopyInitializer(),executable=extension.CopyExecutablePrototype();
        check(ReferenceEquals(extension.CurrentExecutableAncestor,current)&&ReferenceEquals(extension.FamilySourceAncestor,source)&&
            ReferenceEquals(extension.HostAncestor,host),"Extension retains the exact executable, family source and Host objects.");
        check(extension.SourceCertified&&!extension.RuntimeVerified&&!extension.PublicationApproved&&!extension.BodyIssued&&
            !extension.PlacedFamilyIssued&&!extension.FinalExecutableIssued&&!extension.CompleteNativeDependencyClosureProven,
            "Source-owned detached Host/EXE prototype confers no placed family, Body, final EXE, runtime, publication or dependency closure.");
        extension.RequireAncestry(current,source);
        var reissued=NativeLockedChestFamilySource.DecodeOriginal(source.PrivateRows[0].Row.Extent.Space,
            source.HandlerReads[0].Extent.Space,source.ResourceInputs["sound.key.descriptor"].Single().Extent.Space,
            source.ResourceInputs["key.native-handler"].Single().Extent.Space,
            source.ResourceInputs["key.special-renderer-and-global-routines"].Single().Extent.Space,source.ReadSource);
        check(!ReferenceEquals(reissued,source)&&reissued.CopyPackage().SequenceEqual(source.CopyPackage()),
            "A genuinely reissued equal source snapshot is another family source owner.");
        Reject<InvalidDataException>(()=>extension.RequireAncestry(current,reissued),check);
        extension.RequireExactOutputs(overlay,initializer,executable);
        check(overlay.Length==OverlayLength&&initializer.Length==InitializerLength&&executable.Length==oldExecutable.Length&&
            Hash(overlay)==OverlayHash&&Hash(initializer)==InitializerHash&&Hash(executable)==ExecutableHash,
            "All three complete detached outputs match independently frozen source-oracle hashes and exact extents.");

        // These ownership bounds are independent source-oracle constants, not
        // ranges obtained from the implementation being tested.
        foreach(var hook in new[]{(Offset:0x3148,Target:0x8008C5B0u),(Offset:0x3214,Target:0x8008C5C4u)})
            check(Word(oldOverlay,hook.Offset)==0x08021C96&&Word(oldOverlay,hook.Offset+4)==0&&
                Word(overlay,hook.Offset)==(0x08000000u|((hook.Target>>2)&0x03FFFFFFu))&&Word(overlay,hook.Offset+4)==0,
                "D/AE native branch and delay slots are the exact owned jump replacements.");
        CheckComplement(oldOverlay,overlay.AsSpan(0,OldOverlayLength),[(0x3148,8),(0x3214,8)],check,
            "Complete retained Host complement outside the two explicit eight-byte hooks is unchanged.");
        check(!overlay.AsSpan(UsedOverlayLength,OverlayLength-UsedOverlayLength).ContainsAnyExcept((byte)0),
            "Every byte after the complete appended chest/key code owner remains zero allocation padding.");
        check(Word(oldInitializer,0x18)==0x3C028009&&Word(initializer,0x18)==0x3C028009&&
            Word(oldInitializer,0x20)==0x2442BA38&&Word(initializer,0x20)==0x2442CA38,
            "Initializer moves the copy anchor from8008BA38 to8008CA38 without changing the high-half instruction.");
        CheckComplement(oldInitializer,initializer,[(0x20,4)],check,"Complete initializer changes only the independently owned copy-anchor word.");
        CheckComplement(oldExecutable,executable,[(InitializerOffset+0x20,4)],check,
            "Complete executable preserves all census, music, dispatch, LAB and other bytes outside the copy-anchor word.");
        check(executable.AsSpan(InitializerOffset,InitializerLength).SequenceEqual(initializer),
            "Detached executable installs the complete exact extended initializer.");
        check(extension.ComputedCalls.Select(c=>(c.SourcePc,c.FinalPc,c.Word,c.DelayWord)).SequenceEqual(new[]{
            (0x80084304u,0x8008C008u,0x0040F809u,0x26100001u),(0x8008431Cu,0x8008C020u,0x0040F809u,0x02802821u),
            (0x8008436Cu,0x8008C070u,0x0040F809u,0x26100001u),(0x80084394u,0x8008C098u,0x0040F809u,0x24070018u),
            (0x80084454u,0x8008C158u,0x0040F809u,0u)}),
            "Exactly five independent450 computed call sites and complete source delays remain explicitly inventoried.");
        check(extension.ExecutableConsumers.Count==28&&extension.ExecutableConsumers.All(c=>
            Hash(executable.AsSpan(checked((int)(c.RuntimePc-0x8000F800u)),c.ByteLength))==c.Sha256)&&
            extension.ExecutableConsumers.Count(c=>c.ByteLength==0x10)==22,
            "All22 retained entry windows and six bounded fuller consumers bind actual prototype bytes, not full outgoing closure.");
        Reject<InvalidDataException>(()=>NativeId65FreshWizardHostLayout.RewriteInitializer(
            host.InitializerPatch.CopyBeforeBytes(),OverlayLength),check);
        check(extension.RevertExactOverlay(overlay).SequenceEqual(oldOverlay)&&
            extension.RevertExactExecutable(executable).SequenceEqual(oldExecutable),
            "Complete inverses recover the actual immediate Host and current family executable, not retail or an earlier arm.");

        // Existing issuers stay closed over their original complete owners.
        Reject<InvalidDataException>(()=>host.RequireExactOutputs(overlay,initializer),check);
        Reject<InvalidDataException>(()=>current.RequireExactExecutable(executable),check);
        Reject<InvalidDataException>(()=>extension.RequireExactOutputs(oldOverlay,initializer,executable),check);
        Reject<InvalidDataException>(()=>extension.RequireExactOutputs(overlay,oldInitializer,executable),check);
        Reject<InvalidDataException>(()=>extension.RequireExactOutputs(overlay,initializer,oldExecutable),check);
        Reject<InvalidDataException>(()=>extension.RevertExactOverlay(oldOverlay),check);
        Reject<InvalidDataException>(()=>extension.RevertExactExecutable(oldExecutable),check);

        foreach(int at in new[]{0,0x3147,0x3148,0x314F,0x3150,0x3213,0x3214,0x321B,0x321C,
            0xF800,OldOverlayLength-1,OldOverlayLength,0x11B78,0x11B8C,UsedOverlayLength-1,UsedOverlayLength,OverlayLength-1})
        {
            byte[] bad=overlay.ToArray();bad[at]^=1;
            Reject<InvalidDataException>(()=>extension.RequireExactOutputs(bad,initializer,executable),check);
            Reject<InvalidDataException>(()=>extension.RevertExactOverlay(bad),check);
        }
        foreach(int at in new[]{0,0x17,0x18,0x1B,0x1F,0x20,0x23,0x24,InitializerLength-1})
        {byte[] bad=initializer.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>extension.RequireExactOutputs(overlay,bad,executable),check);}
        foreach(int at in new[]{0,0x1CA8,InitializerOffset-1,InitializerOffset,InitializerOffset+0x20,
            InitializerOffset+InitializerLength-1,InitializerOffset+InitializerLength,0x5FC7E,executable.Length-1})
        {
            byte[] bad=executable.ToArray();bad[at]^=1;
            Reject<InvalidDataException>(()=>extension.RequireExactOutputs(overlay,initializer,bad),check);
            Reject<InvalidDataException>(()=>extension.RevertExactExecutable(bad),check);
        }
        foreach(byte[] bad in WrongLengths(overlay))
        {
            Reject<InvalidDataException>(()=>extension.RequireExactOutputs(bad,initializer,executable),check);
            Reject<InvalidDataException>(()=>extension.RevertExactOverlay(bad),check);
        }
        foreach(byte[] bad in WrongLengths(initializer))
            Reject<InvalidDataException>(()=>extension.RequireExactOutputs(overlay,bad,executable),check);
        foreach(byte[] bad in WrongLengths(executable))
        {
            Reject<InvalidDataException>(()=>extension.RequireExactOutputs(overlay,initializer,bad),check);
            Reject<InvalidDataException>(()=>extension.RevertExactExecutable(bad),check);
        }
        var repeat=NativeFreshLockedChestResearchHostExtension.Compose(current,source);
        check(!ReferenceEquals(repeat,extension)&&repeat.CopyOverlay().SequenceEqual(overlay)&&
            repeat.CopyInitializer().SequenceEqual(initializer)&&repeat.CopyExecutablePrototype().SequenceEqual(executable),
            "Repeated composition retains the same actual ancestors and produces deterministic complete outputs without accumulating extensions.");
        byte[] inverseOverlay=extension.RevertExactOverlay(overlay),inverseExecutable=extension.RevertExactExecutable(executable);
        overlay[0]^=1;initializer[0]^=1;executable[0]^=1;inverseOverlay[0]^=1;inverseExecutable[0]^=1;
        extension.RequireExactOutputs(extension.CopyOverlay(),extension.CopyInitializer(),extension.CopyExecutablePrototype());
        source.RequireRetainedOwners();host.RequireExactOutputs(oldOverlay,oldInitializer);current.RequireExactExecutable(oldExecutable);
        check(Hash(extension.CopyOverlay())==OverlayHash&&Hash(extension.CopyInitializer())==InitializerHash&&
            Hash(extension.CopyExecutablePrototype())==ExecutableHash&&
            extension.RevertExactOverlay(extension.CopyOverlay()).SequenceEqual(oldOverlay)&&
            extension.RevertExactExecutable(extension.CopyExecutablePrototype()).SequenceEqual(oldExecutable)&&
            family.CopyLevelData().SequenceEqual(oldBody)&&family.CopyScene().SequenceEqual(oldScene)&&
            host.CopyOverlay().SequenceEqual(oldOverlay)&&host.CopyInitializerPatch().SequenceEqual(oldInitializer)&&
            current.CopyExecutable().SequenceEqual(oldExecutable),
            "Copy mutation and every refusal leave retained extension, inverse, actual ancestors and untouched Body/Scene immutable.");
        Console.WriteLine($"NATIVE_FRESH_CHEST_HOST_EXTENSION_SOURCE_OK overlay={OverlayHash} initializer={InitializerHash} executable={ExecutableHash} nativeAdmission=false");
    }

    private static void RequireOraclePins()
    {
        foreach(string? pin in new[]{OverlayHash,InitializerHash,ExecutableHash})
            if(pin is null||pin.Length!=64||pin.Any(c=>!char.IsAsciiHexDigit(c)))
                throw new InvalidOperationException("Independent chest Host oracle pins are pending; do not derive them from production output.");
    }
    private static void CheckComplement(ReadOnlySpan<byte> before,ReadOnlySpan<byte> after,
        (int Offset,int Length)[] owners,Action<bool,string> check,string message)
    {
        check(before.Length==after.Length,"Complement comparison covers equal complete owner extents.");
        var owned=new bool[before.Length];
        foreach(var owner in owners)
        {
            if(owner.Offset<0||owner.Length<=0||owner.Offset>before.Length-owner.Length||
                owned.AsSpan(owner.Offset,owner.Length).Contains(true))
                throw new InvalidDataException("Independent test ownership ranges must be bounded and disjoint.");
            Array.Fill(owned,true,owner.Offset,owner.Length);
        }
        bool equal=true;for(int i=0;i<before.Length;i++)if(!owned[i]&&before[i]!=after[i])equal=false;
        check(equal,message);
    }
    private static IEnumerable<byte[]> WrongLengths(byte[] bytes)
    {yield return [];yield return bytes[..^1];yield return [..bytes,(byte)0];}
    private static uint Word(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Chest Host extension refused "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
