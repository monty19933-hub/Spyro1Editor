using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class PrivateStorageCases
{
    internal static void Free(Action<bool,string> check)
    {
        Type family=typeof(NativeWizardFamilyAssetPlan);
        foreach(string name in new[]{nameof(NativeWizardFamilyAssetPlan.OriginalDataDomain),nameof(NativeWizardFamilyAssetPlan.OriginalExecutableDomain)})
        {
            var property=family.GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic);
            check(property is not null&&property.PropertyType==typeof(NativeAddressSpace)&&property.SetMethod is null&&
                family.GetProperty(name,BindingFlags.Instance|BindingFlags.Public) is null,
                "Private source domains stay internal, immutable and outside old public JSON.");
        }
        var capture=family.GetMethod(nameof(NativeWizardFamilyAssetPlan.CapturePrivateStorageTemplate),BindingFlags.Instance|BindingFlags.NonPublic);
        check(capture is not null&&capture.ReturnType==typeof(NativeMobyDependencyInstanceTemplate)&&
            capture.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(NativeMobyDependencyClosurePlan)}),
            "Internal Wizard private template consumes an already closed shared graph, not bytes, flags or an invented source certificate.");
        var reader=family.GetMethod(nameof(NativeWizardFamilyAssetPlan.ReadPrivateSource),BindingFlags.Instance|BindingFlags.NonPublic);
        check(reader is not null&&reader.ReturnType==typeof(byte[])&&reader.GetParameters().Single().ParameterType==typeof(NativeByteExtent),
            "Private reader takes a typed extent of a retained actual source.");
        var collision=family.GetMethod(nameof(NativeWizardFamilyAssetPlan.RequireNonSharedModelClass),BindingFlags.Instance|BindingFlags.NonPublic);
        check(collision is not null&&collision.ReturnType==typeof(void)&&collision.GetParameters().Single().ParameterType==typeof(ushort),
            "Actual loader class ownership stays an internal check, not a new public import factory.");
        check(family.GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Any(f=>f.FieldType==typeof(NativeWizardFamilyAssetSource)&&f.IsInitOnly),
            "Existing source issuer is retained privately instead of rebuilt from detached bytes.");
    }

    internal static void Source(NativeWizardFamilyAssetPlan family,Action<bool,string> check,Action<Action,string> reject)
    {
        string json=JsonSerializer.Serialize(family),planSha=family.PlanSha256;
        byte[] block=family.CopySourceAssetBlock();
        NativeAddressSpace original=family.OriginalDataDomain,exe=family.OriginalExecutableDomain;
        check(original.Kind==NativeAddressKind.LevelData&&original.Revision==NativeAddressRevision.Original&&original.ByteLength==0x28A000&&
            exe.Kind==NativeAddressKind.ExecutableFile&&exe.Revision==NativeAddressRevision.Original&&exe.ByteLength==0x66000,
            "Private template retains real complete original donor and original executable domains.");
        check(ReferenceEquals(original,family.OriginalDataDomain)&&ReferenceEquals(exe,family.OriginalExecutableDomain),
            "Repeated domain access returns actual identity, not an equal regenerated domain.");
        family.RequireNonSharedModelClass(0x1F5);check(true,"Grass class is absent from the retained actual PETE shared table.");
        foreach(ushort nativeClass in new ushort[]{0x56,0,512,ushort.MaxValue})
            reject(()=>family.RequireNonSharedModelClass(nativeClass),"actual shared reward class and non-generic/out-of-range classes are refused");
        byte[] data=family.ReadPrivateSource(new(original,0,original.ByteLength));
        byte[] executable=family.ReadPrivateSource(new(exe,0,exe.ByteLength));
        check(Hash(data)=="9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8"&&
            Hash(executable)=="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998",
            "Whole private reads remain exact original snapshots, never emitted Body/Scene/EXE bytes.");
        var modelRead=new NativeMobyDependencyRead("source-package",new(original,0x19B518,0x35B4),Hash(family.CopyOwner("wizard.model")));
        var finalModels=new NativeAddressSpace(NativeAddressKind.LevelData,"source-storage-fixture-only",NativeAddressRevision.Final,0x35B4);
        NativeMobyDependencyNode node=new("fixture.wizard-package",NativeMobyDependencyKind.ActorPackage,
            NativeMobyDependencyResolution.ImportFromSource,NativeMobyDependencySharing.Immutable,modelRead.ExpectedSha256,[],[modelRead],
            c=>new(c.Read("source-package"),[new("detached-model",new(finalModels,0,0x35B4))],[]));
        var shared=NativeMobyDependencyClosureCompiler.Compile([node],[node.Id],family.ReadPrivateSource);
        var template=family.CapturePrivateStorageTemplate(shared);
        check(ReferenceEquals(template.Shared,shared)&&template.Rows.Count==1&&template.Globals.Count==0,
            "Template retains exactly the supplied storage graph; it does not infer complete behavior/global closure.");
        var schema=template.Rows.Single();
        check(schema.Role=="wizard"&&schema.SourceInstanceIndex==6&&schema.NativeClass==0x11B&&
            ReferenceEquals(schema.Row.Extent.Space,original)&&ReferenceEquals(schema.Properties.Extent.Space,original)&&
            schema.Row.Extent.Offset==0x1D4B88&&schema.Row.Extent.ByteLength==0x58&&
            schema.Properties.Extent.Offset==0x1DAB50&&schema.Properties.Extent.ByteLength==0x50&&
            schema.PropertiesPointerBaseOffset==0x1CE800&&schema.OwnerFields.Count==0&&!schema.RequiresBytePersistentOwner,
            "Exact source-level row/private extents and original scene pointer base, without synthetic original Scene storage.");
        check(family.ReadPrivateSource(schema.Row.Extent).SequenceEqual(family.CopyOwner("wizard.row"))&&
            family.ReadPrivateSource(schema.Properties.Extent).SequenceEqual(family.CopyOwner("wizard.private"))&&
            schema.Row.ExpectedSha256==Hash(family.CopyOwner("wizard.row"))&&schema.Properties.ExpectedSha256==Hash(family.CopyOwner("wizard.private")),
            "Captured private template reproduces complete already-issued source owners and their hashes.");
        check(family.CapturePrivateStorageTemplate(shared).SameBinding(template),"Same actual source and shared graph reproduce the same internal template binding.");
        var foreignOriginal=new NativeAddressSpace(original.Kind,original.OwnerIdentity,original.Revision,original.ByteLength);
        var foreignExe=new NativeAddressSpace(exe.Kind,exe.OwnerIdentity,exe.Revision,exe.ByteLength);
        reject(()=>family.ReadPrivateSource(new(foreignOriginal,0x1D4B88,0x58)),"equal donor domain cannot replace actual retained source identity");
        reject(()=>family.ReadPrivateSource(new(foreignExe,0,4)),"equal executable domain cannot replace actual retained source identity");
        reject(()=>family.ReadPrivateSource(new(finalModels,0,4)),"emitted final models cannot masquerade as an original source");
        reject(()=>family.CapturePrivateStorageTemplate(null!),"missing closed shared graph");
        var empty=new NativeMobyDependencyClosurePlan([],[],[],new Dictionary<string,byte[]>(),Hash([]));
        reject(()=>family.CapturePrivateStorageTemplate(empty),"empty graph cannot supply even storage dependency closure");
        var privateGraph=NativeMobyDependencyClosureCompiler.Compile([node with{Kind=NativeMobyDependencyKind.InstanceProperties}],[node.Id],family.ReadPrivateSource);
        reject(()=>family.CapturePrivateStorageTemplate(privateGraph),"shared graph cannot contain mutable per-instance categories");
        var scene=new NativeAddressSpace(NativeAddressKind.Scene,"neutral-private-allocation",NativeAddressRevision.Final,0x3000);
        var allocation=NativeMobyDependencyInstantiationPlan.Create(new(scene,0xD4,0x58),new(scene,0xF88,0x50),0xD4,0,[scene,finalModels],exe)
            .Allocate([new("retained-wizard",template)]);
        var allocated=allocation.Rows.Single();
        check(ReferenceEquals(allocated.Source,schema)&&allocated.Index==0&&allocated.PropertiesReference.ExpectedBefore==0xC350&&
            allocated.Row.Offset==0xD4&&allocated.Properties.Offset==0xF88&&
            allocated.RemapPrivateOwnerIndices(family.CopyOwner("wizard.private")).SequenceEqual(family.CopyOwner("wizard.private")),
            "Existing allocator can consume the real source template without relabeling generated bytes as Original.");
        data[0]^=1;executable[0]^=1;
        check(family.ReadPrivateSource(new(original,0,1))[0]!=data[0]&&family.ReadPrivateSource(new(exe,0,1))[0]!=executable[0],
            "Private source reads are defensive copies.");
        check(JsonSerializer.Serialize(family)==json&&family.PlanSha256==planSha&&family.CopySourceAssetBlock().SequenceEqual(block),
            "Internal source/template use and refusals leave complete old public JSON, plan digest and source block unchanged.");
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
