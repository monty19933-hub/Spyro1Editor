using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class FamilySoundRetargetedPackageCases
{
    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshMobySoundResidencyBinding);
        const BindingFlags methods=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
        var copy=type.GetMethod("CopySoundRetargetedSourcePackage",methods);
        var revert=type.GetMethod("RevertExactSoundRetargetedPackage",methods);
        check(copy is not null&&copy.IsAssembly&&copy.ReturnType==typeof(byte[])&&copy.GetParameters().Length==0,
            "Detached sound retargeting is an internal copy from retained ancestors, not caller package/slot/destination authority.");
        check(revert is not null&&revert.IsAssembly&&revert.ReturnType==typeof(byte[])&&
            revert.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(ReadOnlySpan<byte>)}),
            "Detached inverse accepts only the complete expected package bytes, not a caller source, owner or relocation policy.");
        check(type.IsSealed&&!type.IsPublic&&type.GetConstructors(methods).All(c=>c.IsPrivate)&&
            type.GetProperties(methods).All(p=>p.PropertyType!=typeof(bool)&&p.PropertyType!=typeof(NativeByteExtent)&&
                !p.Name.StartsWith("FinalModel",StringComparison.Ordinal)),
            "Detached package copies add no constructor, final model extent, registration or capability flag to the closed sound binding.");
        Console.WriteLine("NATIVE_FRESH_FAMILY_SOUND_RETARGETED_PACKAGE_FREE_OK detached=true nativeAdmission=false");
    }

    // Source and binding are already captured by the sound harness. This adds
    // no ROM acquisition, old Town compiler, inventory or destination producer.
    internal static void Source(NativeLockedChestFamilySource source,NativeFreshMobySoundResidencyBinding binding,
        NativeFreshMobyFamilyCompositionPlan composition,NativeFreshMobyFamilyCompositionPlan equalComposition,
        Action<bool,string> check)
    {
        byte[] body=composition.CopyLevelData(),scene=composition.CopyScene(),inverse=composition.CopyInverseBaseBody();
        string planSha=binding.PlanSha256,graphSha=binding.Graph.Fingerprint;
        var graph=binding.Graph;
        binding.RequireAncestry(source,composition);

        // Derive the full registered AE interval independently of the new
        // methods. Its end is the next registered root, not a guessed size or
        // any insertion offset in the generated Fresh body.
        var domain=source.PackageRead.Extent.Space;
        var models=NativeMobyModelRegistrationSource.CaptureExactPeace(domain,source.ReadSource);
        var chest=models.Registrations.Single(r=>r.ActorId==0xAE&&r.Slot>0);
        long end=models.Registrations.Where(r=>r.RootDataOffset>chest.RootDataOffset)
            .Select(r=>(long)r.RootDataOffset).Append(models.ModelEnd).Min();
        NativeByteExtent complete=new(domain,chest.RootDataOffset,end-chest.RootDataOffset);
        byte[] original=source.ReadSource(complete);
        check(domain.Kind==NativeAddressKind.LevelData&&domain.Revision==NativeAddressRevision.Original&&
            ReferenceEquals(models.Original,domain)&&Same(complete,source.PackageRead.Extent)&&
            complete.ByteLength==0x1008&&Hash(original)==source.PackageRead.ExpectedSha256&&
            original.SequenceEqual(source.CopyPackage()),
            "The exact source-only AE read owns its complete registered interval through the next model boundary, including its tail.");
        var local=binding.LocalSelector;
        var selection=binding.Selections.Single(s=>s.GlobalSelector is null);
        check(ReferenceEquals(local.SourceField.Extent.Space,domain)&&local.SourceField.Extent.Offset-complete.Offset==4&&
            local.SourceField.Extent.ByteLength==4&&Hash(original.AsSpan(4,4))==local.SourceField.ExpectedSha256&&
            Word(original,4)==0xFFFFFF2F&&!original.AsSpan(5,15).ContainsAnyExcept((byte)0xFF)&&
            local.SourceDescriptorIndex==47&&local.TargetDescriptorIndex==54&&
            selection.SourceDescriptorIndex==47&&selection.TargetDescriptorIndex==54&&
            ReferenceEquals(selection.Descriptor.Space,composition.FinalBodyDomain),
            "Only source slot0 maps descriptor47 to the existing actual resident54; all fifteen other model slots remain FF-unbound.");

        byte[] expected=original.ToArray();expected[4]=54;
        byte[] retargeted=binding.CopySoundRetargetedSourcePackage();
        check(retargeted.SequenceEqual(expected)&&retargeted.Length==original.Length&&Word(retargeted,4)==0xFFFFFF36&&
            Enumerable.Range(0,original.Length).Where(i=>original[i]!=retargeted[i]).SequenceEqual(new[]{4}),
            "Detached retargeting changes exactly source byte4 from47 to54 and preserves every other header, model, texture and tail byte.");
        byte[] restored=binding.RevertExactSoundRetargetedPackage(retargeted);
        check(restored.SequenceEqual(original)&&retargeted.SequenceEqual(expected)&&
            !ReferenceEquals(restored,retargeted)&&!ReferenceEquals(restored,original),
            "The exact inverse returns the whole original package without editing its caller-owned retargeted input.");
        byte[] repeated=binding.CopySoundRetargetedSourcePackage();
        check(!ReferenceEquals(retargeted,repeated)&&repeated.SequenceEqual(expected)&&
            binding.RevertExactSoundRetargetedPackage(expected.ToArray()).SequenceEqual(original),
            "Repeated copies and equal byte snapshots are deterministic detached data, not new source or destination authority.");

        // Whole-complement equality is the sound-only contract. Sample the
        // source animation/renderer stream as well as header, unbound slots and
        // complete registration tail; do not make AE texture-codec admission a
        // prerequisite or infer a relocated GPU destination from these bytes.
        int modelData=checked((int)Word(original,0x34));
        int animation=checked((int)Word(original,0x38));
        check(modelData>=0&&modelData<original.Length&&animation>=0&&animation+0x24L<=original.Length,
            "The retained exact source exposes bounded model and animation header witnesses for complement refusals.");
        int faces=checked(modelData+(int)Word(original,animation+0x14));
        check(faces>=0&&faces+4L<=original.Length,"The source renderer-stream witness lies inside the complete registered package.");
        int faceLength=checked((int)Word(original,faces));
        check(faceLength>0&&faces+4L+faceLength<=original.Length,
            "The source renderer command payload is bounded; byte preservation does not prove texture relocation or model closure.");
        int[] corruptOffsets=[0,3,4,5,7,19,0x34,0x38,animation,modelData,faces,
            faces+4+faceLength/2,original.Length/2,original.Length-8,original.Length-1];
        foreach(int at in corruptOffsets.Distinct())
        {
            byte[] bad=expected.ToArray();bad[at]^=1;
            Reject<InvalidDataException>(()=>binding.RevertExactSoundRetargetedPackage(bad),check);
        }
        byte[] unbound=expected.ToArray();unbound[4]=0xFF;
        Reject<InvalidDataException>(()=>binding.RevertExactSoundRetargetedPackage(unbound),check);
        Reject<InvalidDataException>(()=>binding.RevertExactSoundRetargetedPackage(original),check);
        Reject<InvalidDataException>(()=>binding.RevertExactSoundRetargetedPackage([]),check);
        Reject<InvalidDataException>(()=>binding.RevertExactSoundRetargetedPackage(expected[..^1]),check);
        Reject<InvalidDataException>(()=>binding.RevertExactSoundRetargetedPackage([..expected,(byte)0]),check);

        check(!ReferenceEquals(composition,equalComposition)&&composition.PlanSha256==equalComposition.PlanSha256,
            "The equal-composition refusal uses another actual issuer instance, not different output bytes.");
        Reject<InvalidDataException>(()=>binding.RequireAncestry(source,equalComposition),check);
        var foreign=new NativeAddressSpace(domain.Kind,domain.OwnerIdentity,domain.Revision,domain.ByteLength);
        Reject<InvalidDataException>(()=>source.ReadSource(new(foreign,complete.Offset,complete.ByteLength)),check);

        // Adversarial in-memory owner checks only. Assert the unique private
        // field/type/owner before changing one byte, restore in finally, and
        // never touch a file or leave a corrupted owner for subsequent cases.
        var originalsField=PrivateField(typeof(NativeLockedChestFamilySource),"_originals",typeof(Dictionary<NativeAddressSpace,byte[]>),check);
        var originals=(Dictionary<NativeAddressSpace,byte[]>)originalsField.GetValue(source)!;
        check(originals.Count==5&&originals.ContainsKey(domain)&&originals[domain].Length==domain.ByteLength&&
            originals[domain].AsSpan((int)complete.Offset,(int)complete.ByteLength).SequenceEqual(original),
            "Reflection source corruption targets the unique retained in-memory Original package owner, never a file or detached copy.");
        RequireOwnerRefusal(originals[domain],checked((int)complete.Offset+4),binding,expected,original,check);
        var bodyField=PrivateField(typeof(NativeFreshMobyFamilyCompositionPlan),"_body",typeof(byte[]),check);
        var retainedBody=(byte[])bodyField.GetValue(composition)!;
        check(retainedBody.Length==composition.FinalBodyDomain.ByteLength&&retainedBody.SequenceEqual(body)&&
            ReferenceEquals(binding.CompositionAncestor,composition)&&selection.Descriptor.ByteLength==20,
            "Reflection body corruption targets the actual retained FinalBody and its complete selected descriptor owner.");
        RequireOwnerRefusal(retainedBody,checked((int)selection.Descriptor.Offset+10),binding,expected,original,check);

        retargeted[4]^=1;repeated[^1]^=1;restored[0]^=1;
        check(binding.CopySoundRetargetedSourcePackage().SequenceEqual(expected)&&
            binding.RevertExactSoundRetargetedPackage(expected).SequenceEqual(original),
            "Mutating all returned package copies cannot change later forward copies or the exact inverse.");
        binding.RequireRetainedOwners();binding.RequireExactLevelData(body);
        check(ReferenceEquals(binding.Graph,graph)&&binding.PlanSha256==planSha&&binding.Graph.Fingerprint==graphSha&&
            binding.Graph.Installs.Count==0&&binding.Graph.References.Count==0&&binding.Graph.Nodes.Sum(n=>n.Preserved.Count)==8&&
            binding.Graph.Nodes.All(n=>n.Kind is NativeMobyDependencyKind.Sound or NativeMobyDependencyKind.Bundle)&&
            Same(binding.LocalSelector.SourceField.Extent,local.SourceField.Extent)&&
            binding.LocalSelector.SourceField.Extent.Space.Revision==NativeAddressRevision.Original&&
            source.CopyPackage().SequenceEqual(original)&&composition.CopyLevelData().SequenceEqual(body)&&
            composition.CopyScene().SequenceEqual(scene)&&composition.CopyInverseBaseBody().SequenceEqual(inverse)&&
            inverse.SequenceEqual(composition.BaseBodyAncestor.CopyLevelData()),
            "Forward copies, inverses and every refusal preserve all source/body/scene/inverse bytes and the same sound-only graph with no installed model.");
        Console.WriteLine($"NATIVE_FRESH_LOCKED_CHEST_SOUND_RETARGETED_PACKAGE_SOURCE_OK mixed={composition.BaseBodyAncestor.MixedResourceAncestor is not null} body={composition.LevelDataSha256} detached=true installs=0 nativeAdmission=false");
    }

    private static void RequireOwnerRefusal(byte[] owner,int at,NativeFreshMobySoundResidencyBinding binding,
        byte[] expected,byte[] original,Action<bool,string> check)
    {
        byte before=owner[at];
        try
        {
            owner[at]^=1;
            Reject<InvalidDataException>(()=>binding.CopySoundRetargetedSourcePackage(),check);
            Reject<InvalidDataException>(()=>binding.RevertExactSoundRetargetedPackage(expected),check);
        }
        finally {owner[at]=before;}
        binding.RequireRetainedOwners();
        check(binding.CopySoundRetargetedSourcePackage().SequenceEqual(expected)&&
            binding.RevertExactSoundRetargetedPackage(expected).SequenceEqual(original),
            "Both detached methods recheck retained owners on every call and recover after exact in-memory owner restoration.");
    }
    private static FieldInfo PrivateField(Type type,string name,Type fieldType,Action<bool,string> check)
    {
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly)
            .Where(f=>f.Name==name&&f.FieldType==fieldType&&f.IsPrivate&&f.IsInitOnly).ToArray();
        check(fields.Length==1,"Adversarial owner test requires one exact named private readonly field of the expected type.");
        return fields.Single();
    }
    private static bool Same(NativeByteExtent a,NativeByteExtent b)=>ReferenceEquals(a.Space,b.Space)&&a.Offset==b.Offset&&a.ByteLength==b.ByteLength;
    private static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Expected detached sound-package refusal "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
