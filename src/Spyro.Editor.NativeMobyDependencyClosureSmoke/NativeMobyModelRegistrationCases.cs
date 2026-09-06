using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

internal static class NativeMobyModelRegistrationCases
{
    internal static void Run(Action<bool,string> check, Action<Action,string> reject)
    {
        byte[] Fixture(params ushort[] classes)
        {
            byte[] b=new byte[0x2800];
            for(int i=0;i<4;i++){Put(b,i*8,(uint)(0x800+i*0x800));Put(b,i*8+4,0x800);}
            ushort[] all=[0,..classes];
            for(int i=0;i<all.Length;i++)
            {Put(b,0x50+i*4,(uint)(0x1800+i*4));BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(0x150+i*2),all[i]);Put(b,0x1800+i*4,0xFFFFFFFF);}
            return b;
        }
        var loader=NativeMobyModelLoaderCertificate.DecodeFixture([9]);
        byte[] dst=Fixture(10),donor=Fixture(200,201,202);
        NativeAddressSpace original=new(NativeAddressKind.LevelData,"registration-destination",NativeAddressRevision.Original,dst.Length);
        NativeAddressSpace donorSpace=new(NativeAddressKind.LevelData,"registration-donor",NativeAddressRevision.Original,donor.Length);
        NativeAddressSpace final=new(NativeAddressKind.LevelData,"registration-final",NativeAddressRevision.Final,dst.Length+0x800);
        var sources=new Dictionary<NativeAddressSpace,byte[]>{{original,dst},{donorSpace,donor}};
        byte[] Read(NativeByteExtent extent)=>sources.TryGetValue(extent.Space,out var bytes)
            ?bytes.AsSpan((int)extent.Offset,(int)extent.ByteLength).ToArray():throw new InvalidDataException("Unbound source address domain.");
        var target=NativeMobyModelRegistrationSource.DecodeFixture(original,dst);
        var from=NativeMobyModelRegistrationSource.DecodeFixture(donorSpace,donor);
        var nodes=new Dictionary<string,NativeMobyDependencyNode>();
        NativeMobyModelPackageBinding Package(string id,ushort cls,int offset,NativeAddressSpace? space=null,string policy="fixture-relative-package")
        {
            var root=from.Registrations.Single(r=>r.ActorId==cls);
            var read=new NativeMobyDependencyRead("native",new(donorSpace,root.RootDataOffset,4),Hash(donor.AsSpan((int)root.RootDataOffset,4)));
            var node=new NativeMobyDependencyNode(id,NativeMobyDependencyKind.ActorPackage,NativeMobyDependencyResolution.ImportFromSource,
                NativeMobyDependencySharing.Immutable,Hash([1]),[],[read],c=>new(c.Read("native"),[new("install",new(space??final,offset,4))],[]));
            nodes[id]=node;
            return NativeMobyModelPackageBinding.Capture(from,cls,NativeMobyDependencyClosureCompiler.Compile([node],[id],Read),id,"native",policy,Read);
        }
        var a=Package("model.a",200,0x2000);var b=Package("model.b",201,0x2004);
        NativeMobyModelRegistrationPlan Build(params NativeMobyModelPackageBinding[] packages)=>NativeMobyModelRegistrationPlan.Build(target,loader,final,packages,Read);
        var plan=Build(a,b);
        check(plan.GenericCountBefore==1&&plan.GenericCountAfter==3&&plan.FreeSlotsAfter==60,"Generic registration capacity includes the special Spyro slot.");
        check(plan.ModelGrowthBytes==0x800&&plan.FinalModelOffset==0x1800&&plan.FinalModelLength==0x1000,"Model append did not derive exact sector-rounded owner growth.");
        check(!plan.RuntimeMemoryCertified&&!plan.NativeImportApproved,"Registration geometry granted native runtime/export authority.");
        check(plan.Packages.Select(p=>p.Slot).SequenceEqual(new[]{2,3}),"Missing packages were not appended consecutively.");
        check(plan.CopyHeader().AsSpan(0x50,8).SequenceEqual(dst.AsSpan(0x50,8)),"Append replaced the player stream or an existing root.");
        check(plan.CopyHeader().AsSpan(0x150,4).SequenceEqual(dst.AsSpan(0x150,4)),"Append replaced an existing class identity.");
        check(plan.Fingerprint==Build(b,a,a).Fingerprint,"Request order or an exact retry changed immutable registration allocation.");
        IEnumerable<NativeMobyModelPackageBinding> Endless(){while(true)yield return a;}
        reject(()=>NativeMobyModelRegistrationPlan.Build(target,loader,final,Endless(),Read),"declaration budget");
        check(plan.Fingerprint==Build(a,b).Fingerprint,"Bounded hostile request refusal changed a prior plan.");
        var first=plan.Packages[0];
        var register=new NativeMobyDependencyNode("root.a",NativeMobyDependencyKind.ActorRegistration,NativeMobyDependencyResolution.ImportFromSource,
            NativeMobyDependencySharing.Immutable,Hash([1]),[a.NodeId],[new("native",new(original,first.RootField.Offset,4),Hash(new byte[4]))],
            c=>new(c.Read("native"),[new("install",first.RootField)],[first.Reference]));
        var complete=NativeMobyDependencyClosureCompiler.Compile([nodes[a.NodeId],nodes[b.NodeId],register],[register.Id,b.NodeId],Read);
        check(complete.References.Single().After==0x2000&&complete.References.Single().Target.Space==final,"Typed root reference did not bind the actual package domain/offset.");
        byte[] output=new byte[final.ByteLength];dst.AsSpan(0,0x2000).CopyTo(output);dst.AsSpan(0x2000).CopyTo(output.AsSpan(0x2800));
        plan.CopyHeader().CopyTo(output,0);a.CopyPayload().CopyTo(output,0x2000);b.CopyPayload().CopyTo(output,0x2004);
        plan.RequireFinal(output,complete);check(true,"Positive registration plan did not validate its full output.");
        foreach(var corruption in new[]{(0x50,"header"),(0x1804,"existing"),(0x2000,"payload"),(0x2100,"padding")})
        {var copy=output.ToArray();copy[corruption.Item1]^=1;reject(()=>plan.RequireFinal(copy,complete),corruption.Item2);}
        byte[] detached=plan.CopyHeader();detached[0x50]^=1;
        check(plan.CopyHeader()[0x50]==dst[0x50],"Returned registration header aliases plan state.");
        var oldHash=Hash(dst);var donorHash=Hash(donor);
        reject(()=>Build(Package("wrong-position",200,0x2004)),"append position");
        reject(()=>Build(a,Package("same-class",200,0x2004)),"class conflict");
        reject(()=>Build(a,Package("model.a",200,0x2000,policy:"different-context")),"identity");
        var otherFinal=new NativeAddressSpace(NativeAddressKind.LevelData,final.OwnerIdentity,NativeAddressRevision.Final,final.ByteLength);
        reject(()=>Build(Package("other-domain",200,0x2000,otherFinal)),"address domain");
        reject(()=>NativeMobyModelRegistrationPlan.Build(target,NativeMobyModelLoaderCertificate.DecodeFixture([200]),final,[a],Read),"shared");
        byte[] changed=dst.ToArray();changed[0x1000]=1;sources[original]=changed;
        reject(()=>Build(a),"complete source");sources[original]=dst;
        check(Build(a,b).Fingerprint==plan.Fingerprint&&Hash(dst)==oldHash&&Hash(donor)==donorHash,"Failed transaction modified native input or deterministic retry.");
        void Bad(byte[] bytes,string reason)=>reject(()=>NativeMobyModelRegistrationSource.DecodeFixture(original,bytes),reason);
        var bad=dst.ToArray();Put(bad,0x50,0);Bad(bad,"slot 0");
        bad=dst.ToArray();BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(0x150),1);Bad(bad,"slot 0");
        bad=dst.ToArray();Put(bad,0x58,0x1810);Bad(bad,"class 0");
        bad=dst.ToArray();Put(bad,0x5C,0x1810);BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(0x156),12);Bad(bad,"prefix");
        bad=dst.ToArray();BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(0x152),512);Bad(bad,"512");
        bad=dst.ToArray();BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(0x152),0xFFFF);Bad(bad,"512");
        bad=dst.ToArray();Put(bad,0x54,0xFFFFFFFF);Bad(bad,"positive");
        bad=dst.ToArray();Put(bad,0x54,0x17FC);Bad(bad,"model owner");
        bad=dst.ToArray();Put(bad,0x54,0x1800);Bad(bad,"ascending");
        bad=dst.ToArray();BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(0x154),11);Bad(bad,"terminator");
        var fullBytes=Fixture(Enumerable.Range(10,63).Select(i=>(ushort)i).ToArray());sources[original]=fullBytes;
        var full=NativeMobyModelRegistrationSource.DecodeFixture(original,fullBytes);
        reject(()=>NativeMobyModelRegistrationPlan.Build(full,loader,final,[a],Read),"63");sources[original]=dst;
        var shortFinal=new NativeAddressSpace(NativeAddressKind.LevelData,"short",NativeAddressRevision.Final,dst.Length);
        reject(()=>NativeMobyModelRegistrationPlan.Build(target,loader,shortFinal,[],Read),"package");
        reject(()=>NativeMobyModelLoaderCertificate.DecodeFixture([512]),"512");
        reject(()=>NativeMobyModelLoaderCertificate.DecodeFixture([9,9]),"repeats");
        reject(()=>NativeMobyModelLoaderCertificate.CaptureRetail([],[],[]),"source certificate");
        check(Hash(dst)==oldHash&&Hash(donor)==donorHash,"Registration negative tests modified retained fixture source.");
        Console.WriteLine("NATIVE_MOBY_APPEND_ONLY_REGISTRATION_PREFIX_CLASS_DOMAIN_AND_CAPACITY_OK");
    }

    internal static void AuditSource(string imagePath,Action<bool,string> check)
    {
        // Native source Capture binds the complete original ROM before and after enumeration.
        string workspace=Directory.GetCurrentDirectory();
        var inventory=NativeMobySourceInventory.Capture(imagePath,LevelCatalog.Load(workspace));
        using var image=File.OpenRead(imagePath);var disc=DiscImage.DetectLayout(imagePath);
        List<int> counts=[];
        foreach(var level in inventory.Levels)
        {
            byte[] bytes=DiscImage.ReadFileBytes(image,disc,37,level.DataWadOffset,level.SourceDataByteLength);
            var domain=new NativeAddressSpace(NativeAddressKind.LevelData,level.SourceDataSha256,NativeAddressRevision.Original,bytes.Length);
            var source=NativeMobyModelRegistrationSource.CaptureInventory(level,domain,e=>e.Space==domain?bytes.AsSpan((int)e.Offset,(int)e.ByteLength).ToArray():throw new InvalidDataException("Unbound native audit domain."));
            check(source.Registrations.SequenceEqual(level.Registrations),"Native registration producer disagrees with independently captured inventory.");
            counts.Add(source.Registrations.Count-1);
        }
        check(counts.Count==35&&counts.Min()==21&&counts.Max()==52,"Native 35-level generic slot extremes changed.");
        Console.WriteLine("NATIVE_MOBY_REGISTRATION_ALL_35_NATIVE_HEADERS_AND_SPECIAL_PLAYER_SLOT_OK");
    }

    internal static void Source(NativeTownSquareLockedChestClosure native,Action<bool,string> check)
    {
        var p=native.ModelRegistration;
        check(p.GenericCountBefore==36&&p.GenericCountAfter==37&&p.FreeSlotsAfter==26,"Town native registration accounting changed.");
        check(p.Packages.Count==1&&p.Packages[0].ActorId==0xAE&&p.Packages[0].Slot==37&&p.Packages[0].RootField.Offset==0xE4&&p.Packages[0].ClassField.Offset==0x19A,"Town AE did not use the source-derived first tail registration.");
        check(p.ModelGrowthBytes==0x1800&&p.FinalModelLength==0x5E800,"Town exact model-subfile growth changed.");
        check(p.Loader.SharedClasses.Count==54&&p.Loader.IsRetailCertified&&!p.RuntimeMemoryCertified&&!p.NativeImportApproved,"PETE source evidence became a runtime memory or export approval.");
        check(native.Data.AsSpan(0,0x800).SequenceEqual(p.CopyHeader()),"Production graph did not consume the registration producer header.");
        p.RequireFinal(native.Data,native.Graph);check(true,"Town model registration proof failed.");
    }
    private static void Put(byte[] b,int o,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o),v);
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}
