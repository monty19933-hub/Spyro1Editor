using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

internal static class FamilyChestExecutableCases
{
    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshLockedChestExperimentalExecutable);
        check(!type.IsPublic&&type.IsSealed&&type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .All(c=>c.IsPrivate)&&type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Length==0,
            "Chest experimental executable is a closed internal issuer, not a public export or arbitrary-byte compiler.");
        var compose=type.GetMethods(BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.DeclaredOnly).Single(m=>m.Name=="Compose");
        check(compose.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(NativeFreshLockedChestPlacementPlan),
            typeof(NativeFreshLockedChestResearchHostExtension)}),"Only actual placed Body and actual Host can issue the experimental executable.");
        foreach(var item in new[]{("PlacementAncestor",typeof(NativeFreshLockedChestPlacementPlan)),
            ("BodyAncestor",typeof(NativeFreshLockedChestResearchBody)),("HostAncestor",typeof(NativeFreshLockedChestResearchHostExtension))})
        {
            var property=type.GetProperty(item.Item1,BindingFlags.NonPublic|BindingFlags.Instance);
            check(property is not null&&property.PropertyType==item.Item2&&property.GetMethod?.IsAssembly==true&&property.GetSetMethod(true) is null,
                "Read-only actual ancestor "+item.Item1);
        }
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestExperimentalExecutable.Compose(null!,null!),check);
        Console.WriteLine("NATIVE_FRESH_CHEST_EXECUTABLE_FREE_OK completeCensus=false runtime=false");
    }

    internal static void Source(NativeFreshLockedChestPlacementPlan placement,Action<bool,string> check)
    {
        var body=placement.BodyAncestor;var host=body.HostAncestor;
        byte[] scene=placement.CopyScene(),placedBody=placement.CopyLevelData(),prototype=host.CopyExecutablePrototype();
        byte[] overlay=host.CopyOverlay(),initializer=host.CopyInitializer();
        placement.RequireExactScene(scene);placement.RequireExactLevelData(placedBody);
        host.RequireExactOutputs(overlay,initializer,prototype);
        check(prototype.Length==0x66000&&Hash(prototype)=="ce341af853579b6b17a18222c08d2c17dbf6a9a572be672cf39edd5a6699ad20"&&
            Half(prototype,0x5FC7E)==10,"Frozen450 complete Host prototype retains the provisional target10 preimage.");
        byte[] expected=prototype.ToArray();expected[0x5FC7E]=20;expected[0x5FC7F]=0;
        var plan=NativeFreshLockedChestExperimentalExecutable.Compose(placement,host);
        byte[] actual=plan.CopyExecutable();
        check(ReferenceEquals(plan.PlacementAncestor,placement)&&ReferenceEquals(plan.BodyAncestor,body)&&ReferenceEquals(plan.HostAncestor,host),
            "Experimental executable retains the actual placement, source-coordinate Body and Host, not equal metadata.");
        plan.RequireAncestry(placement,host);plan.RequireExactExecutable(actual);
        check(actual.SequenceEqual(expected)&&plan.ExecutableSha256==Hash(expected)&&IsHash(plan.PlanSha256)&&
            plan.PriorExecutableSha256==Hash(prototype)&&plan.SourceImageSha256=="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37",
            "Complete executable equals an independent two-byte child-relative patch with complete content/source identities.");
        check(plan.TargetSlot==35&&plan.TargetFileOffset==0x5FC7E&&plan.TargetPatch.FileOffset==0x5FC7E&&
            plan.TargetPatch.BeforeHex=="0A00"&&plan.TargetPatch.AfterHex=="1400"&&Half(actual,0x5FC7E)==20&&
            actual.AsSpan(0,0x5FC7E).SequenceEqual(prototype.AsSpan(0,0x5FC7E))&&
            actual.AsSpan(0x5FC80).SequenceEqual(prototype.AsSpan(0x5FC80)),
            "Only the existing signed native slot35 target changes10 to20; complete initializer/dispatch/music/other-target complement remains exact.");
        check(plan.SourceCertified&&plan.Experimental&&plan.GrassPassiveDispatchPreserved&&!plan.KeyZeroContributionProven&&
            !plan.CompleteStaticCensusProven&&!plan.RuntimeDropGuarantee&&!plan.CompleteNativeDependencyClosureProven&&
            !plan.RuntimeVerified&&!plan.PublicationApproved,
            "Source-owned experimental target retains explicit unresolved key and whole-census/runtime boundaries.");
        check(plan.Rows.Count==9&&plan.KnownSelectorCount==7&&plan.KnownDeterministicSelectorSubtotal==20&&plan.ExperimentalTreasureTarget==20,
            "Seven known deterministic selectors sum20; two FF rows are separate assumptions, not inferred zeros.");
        ushort[] classes=[0x11B,0x1F5,0xAD,0xAE,0xD,0xD,0xD,0xD,0xD];
        byte[] selectors=[0x56,0xFF,0xFF,0x54,0x54,0x53,0x54,0x53,0x54];
        int?[] values=[10,null,null,2,2,1,2,1,2];
        for(int i=0;i<9;i++)
        {
            int offset=0xD4+i*0x58;var row=plan.Rows[i];
            check(row.PlacedOrdinal==i&&row.SceneRowOffset==offset&&row.ActorClassId==classes[i]&&row.RewardSelector==selectors[i]&&
                row.ActorKind==scene[offset+0x3A]&&row.DeterministicSelectorValue==values[i]&&
                row.AssumedContribution==(i is 1 or 2?0:(int?)null)&&row.SceneRowSha256==Hash(scene.AsSpan(offset,0x58))&&
                Half(scene,offset+0x36)==classes[i]&&scene[offset+0x53]==selectors[i],
                "Exact final authored row, selector, known value and explicit assumption "+i);
        }
        check(plan.Rows[1].Premise=="retained-passive-dispatch-zero"&&plan.Rows[2].Premise=="experimental-key-zero-not-certified"&&
            plan.Rows.Where((_,i)=>i is not 1 and not 2).All(r=>r.Premise=="source-deterministic-selector-value"),
            "Census distinguishes a retained passive path, unproven key-zero assumption and native selector facts.");
        check(plan.Evidence.Any(e=>e.Offset==0x2C264&&e.ByteLength==0x58&&e.Sha256=="5a7677129481343250dfaa32337c82b805d64902f154ea5c0af991a460947e48")&&
            plan.Evidence.Any(e=>e.Offset==0x2B3C0&&e.ByteLength==0x51C&&e.Sha256=="979e567dd4bebbdd9cadaf8f884e1110be16f293c6590ba6214941be5be7a4a8"),
            "Exact full retained native value chain and FF-not-zero emitter evidence accompany the experimental target.");
        var passive=host.CurrentExecutableAncestor.FamilyCompositionAncestor!.Placements[1].Dispatch!;
        check(plan.GrassDispatchTraceSha256==passive.ExecutedTraceSha256,
            "Actual extended Host replay retains the complete already-issued passive Grass trace.");
        byte[] inverse=plan.RevertExactExecutable(actual),original=plan.RevertExactOriginal(actual);
        var domain=host.FamilySourceAncestor.ResourceInputs["key.special-renderer-and-global-routines"].Single().Extent.Space;
        byte[] sourceOriginal=host.FamilySourceAncestor.ReadSource(new(domain,0,domain.ByteLength));
        check(inverse.SequenceEqual(prototype)&&original.SequenceEqual(sourceOriginal)&&
            Hash(original)=="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"&&
            host.CurrentExecutableAncestor.RevertExact(host.RevertExactExecutable(inverse)).SequenceEqual(original),
            "Complete inverse composes through actual Host and Family executable to the same actual Original source owner.");
        Reject<InvalidDataException>(()=>host.RequireExactOutputs(overlay,initializer,actual),check);
        Reject<InvalidDataException>(()=>host.CurrentExecutableAncestor.RequireExactExecutable(actual),check);
        Reject<InvalidDataException>(()=>plan.RequireExactExecutable(prototype),check);
        Reject<InvalidDataException>(()=>plan.RevertExactExecutable(original),check);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestExperimentalExecutable.Compose(null!,host),check);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestExperimentalExecutable.Compose(placement,null!),check);
        Reject<ArgumentNullException>(()=>plan.RequireAncestry(null!,host),check);
        Reject<ArgumentNullException>(()=>plan.RequireAncestry(placement,null!),check);

        var otherPlacement=NativeFreshLockedChestPlacementPlan.Compose(body,placement.Intent);
        var otherHost=NativeFreshLockedChestResearchHostExtension.Compose(host.CurrentExecutableAncestor,host.FamilySourceAncestor);
        check(!ReferenceEquals(otherPlacement,placement)&&otherPlacement.PlanSha256==placement.PlanSha256&&
            !ReferenceEquals(otherHost,host)&&otherHost.PlanSha256==host.PlanSha256,
            "Equal-hash foreign placement/Host controls are genuine independently issued objects.");
        Reject<InvalidDataException>(()=>plan.RequireAncestry(otherPlacement,host),check);
        Reject<InvalidDataException>(()=>plan.RequireAncestry(placement,otherHost),check);
        Reject<InvalidDataException>(()=>NativeFreshLockedChestExperimentalExecutable.Compose(placement,otherHost),check);
        var repeat=NativeFreshLockedChestExperimentalExecutable.Compose(placement,host);
        check(repeat.PlanSha256==plan.PlanSha256&&repeat.CopyExecutable().SequenceEqual(expected),
            "Repeated actual issuance is deterministic without accumulating a second target patch.");
        var moved=NativeFreshLockedChestPlacementPlan.Compose(body,placement.Intent with{KeyX=placement.Intent.KeyX+16});
        var movedPlan=NativeFreshLockedChestExperimentalExecutable.Compose(moved,host);
        check(moved.LevelDataSha256!=placement.LevelDataSha256&&movedPlan.CopyExecutable().SequenceEqual(expected)&&
            movedPlan.PlanSha256!=plan.PlanSha256,"Distinct authored Body ownership changes plan identity even when all executable bytes remain equal.");
        Reject<InvalidDataException>(()=>plan.RequireAncestry(moved,host),check);
        foreach(int at in new[]{0,0x1CA8,0x2B3C0,0x2C264,0x4B098,0x4B12F,0x5FC38,0x5FC7D,0x5FC7E,0x5FC7F,0x5FC80,0x65FFF})
        {
            byte[] bad=actual.ToArray();bad[at]^=1;
            Reject<InvalidDataException>(()=>plan.RequireExactExecutable(bad),check);
            Reject<InvalidDataException>(()=>plan.RevertExactOriginal(bad),check);
        }
        foreach(byte[] bad in new byte[][]{[],actual[..^1],[..actual,(byte)0]})
            Reject<InvalidDataException>(()=>plan.RevertExactExecutable(bad),check);
        actual[0]^=1;inverse[0]^=1;original[0]^=1;scene[0]^=1;placedBody[0]^=1;
        check(plan.CopyExecutable().SequenceEqual(expected)&&plan.RevertExactExecutable(expected).SequenceEqual(prototype)&&
            Hash(placement.CopyScene())==placement.SceneSha256&&Hash(placement.CopyLevelData())==placement.LevelDataSha256&&
            host.CopyExecutablePrototype().SequenceEqual(prototype),"Detached output/inverse copies cannot mutate any retained executable owner.");
        placement.RequireExactScene(placement.CopyScene());placement.RequireExactLevelData(placement.CopyLevelData());
        check(host.CurrentExecutableAncestor.FamilyCensus!.StaticTreasureTarget==10&&Half(host.CopyExecutablePrototype(),0x5FC7E)==10,
            "Legacy P2 census, plan and provisional Host target10 remain unchanged.");
        Console.WriteLine($"NATIVE_FRESH_CHEST_EXECUTABLE_SOURCE_OK executable={Hash(expected)} knownSubtotal=20 experimentalTarget=20 completeCensus=false runtime=false");
    }
    private static ushort Half(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(b[at..]);
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static bool IsHash(string value)=>value.Length==64&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Experimental chest executable refused "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
