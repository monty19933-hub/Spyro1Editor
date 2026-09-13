using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class NativeLockedChestFamilySourceCases
{
    private const string PackageSha256="8edc9e9ac1e5fb1a92224d2f5f8ca541371940c1ae3f9873c02f6b802d6e22e7";
    private static readonly int[] SourceIndices = [78,79,118,119,120,121,122];
    private static readonly int[] PropertyOffsets = [0xEA94,0xEA98,0xEF9C,0xEFC4,0xEFEC,0xF014,0xF03C];
    private static readonly int[] PropertyLengths = [4,0x18,0x28,0x28,0x28,0x28,0x28];

    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeLockedChestFamilySource);
        var privateStatic=BindingFlags.Static|BindingFlags.NonPublic;
        var productionPackage=(string)type.GetField("PackageSha256",privateStatic)!.GetRawConstantValue()!;
        var historicalPackage=(string)typeof(TownSquareNativeLockedChestCandidateExporter)
            .GetField("ExpectedLockedChestPackageSha256",privateStatic)!.GetRawConstantValue()!;
        var ownerPins=(string[])type.GetField("OwnerHashes",privateStatic)!.GetValue(null)!;
        check(ownerPins.Length==5&&ownerPins.Append(productionPackage).Append(PackageSha256).All(pin=>
            pin.Length==64&&pin.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f')),
            "Every new source-owner/package SHA256 pin has exactly64 lowercase hexadecimal digits.");
        check(productionPackage==historicalPackage&&PackageSha256==historicalPackage,
            "Both new package pins equal the unchanged historical exporter pin before any source IO.");
        check(type.IsSealed&&!type.IsPublic&&type.GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0,
            "Locked-chest source decoder is internal and cannot be publicly constructed from claims.");
        var factory=type.GetMethod("DecodeOriginal",BindingFlags.Static|BindingFlags.NonPublic);
        check(factory!=null&&factory.ReturnType==type&&factory.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
            typeof(NativeAddressSpace),typeof(NativeAddressSpace),typeof(NativeAddressSpace),typeof(NativeAddressSpace),
            typeof(NativeAddressSpace),typeof(Func<NativeByteExtent,byte[]>)}),
            "Source decoder takes only its five Original owners and a source reader, not paths or a destination policy.");
        check(type.GetProperties(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
            .All(p=>p.Name is not ("SourceCertified" or "NativeImportApproved" or "DestinationBound" or "RuntimeVerified" or "PublicationApproved")),
            "Internal decoding must not manufacture original-image certification or destination/runtime approval.");
        var domains=Domains();int reads=0;
        byte[] Tiny(NativeByteExtent extent){reads++;return [];}
        Refuse<ArgumentNullException>(()=>Decode(domains,null!),check);
        Refuse<ArgumentNullException>(()=>NativeLockedChestFamilySource.DecodeOriginal(null!,domains[1],domains[2],domains[3],domains[4],Tiny),check);
        foreach(int slot in Enumerable.Range(0,5))
        {
            var invalid=domains.ToArray();var prior=invalid[slot];
            invalid[slot]=new(prior.Kind,prior.OwnerIdentity,NativeAddressRevision.Final,prior.ByteLength);
            reads=0;Refuse<InvalidDataException>(()=>Decode(invalid,Tiny),check);
            check(reads==0,"All five Original domain roles are validated before a source read.");
        }
        var wrongKind=domains.ToArray();wrongKind[4]=new(NativeAddressKind.LevelData,"wrong-exe-kind",NativeAddressRevision.Original,0x66000);
        reads=0;Refuse<InvalidDataException>(()=>Decode(wrongKind,Tiny),check);
        check(reads==0,"An EXE-shaped level-data domain is not executable ownership.");
        var wrongLength=domains.ToArray();wrongLength[0]=new(NativeAddressKind.LevelData,"short-peace",NativeAddressRevision.Original,1);
        reads=0;Refuse<InvalidDataException>(()=>Decode(wrongLength,Tiny),check);
        check(reads==0,"Complete source-owner lengths are checked before decoding a slice.");
        Refuse<InvalidDataException>(()=>Decode(domains,Tiny),check);
        Refuse<InvalidDataException>(()=>Decode(domains,e=>new byte[checked((int)e.ByteLength)]),check);
        var sentinel=new IOException("locked-chest-source-reader-sentinel");Exception? observed=null;
        try{_=Decode(domains,_=>throw sentinel);}catch(Exception error){observed=error;}
        check(ReferenceEquals(observed,sentinel),"A pure decoder preserves the actual source-read failure without retry or replacement.");
        Console.WriteLine("NATIVE_LOCKED_CHEST_SOURCE_DECODER_CLOSED_ORIGINAL_ONLY_FREE_OK");
    }

    internal static void Source(NativeTownSquareLockedChestClosure native,Action<bool,string> check)
    {
        // Reuse the existing compiler's one actual capture. This test does not
        // reopen a ROM, generate a candidate, or issue another source owner.
        NativeLockedChestFamilySource source=native.FamilySource;
        source.RequireRetainedOwners();
        var rows=source.PrivateRows;var globals=source.MutableGlobals;
        check(rows.Count==7&&rows.Select(r=>r.SourceInstanceIndex).SequenceEqual(SourceIndices)&&
            rows.Select(r=>r.Role).SequenceEqual(new[]{"key","chest","reward.1","reward.2","reward.3","reward.4","reward.5"})&&
            rows.Select(r=>r.NativeClass).SequenceEqual(new ushort[]{0xAD,0xAE,0xD,0xD,0xD,0xD,0xD})&&
            rows.Sum(r=>r.Properties.Extent.ByteLength)==0xE4,
            "One source family retains its seven native roles and complete private allocation requirement.");
        for(int i=0;i<rows.Count;i++)
        {
            var row=rows[i];byte[] raw=source.ReadSource(row.Row.Extent),properties=source.ReadSource(row.Properties.Extent);
            check(row.Row.Extent.Offset==0x1CAA60+SourceIndices[i]*0x58L&&row.Row.Extent.ByteLength==0x58&&
                row.Properties.Extent.Offset==0x1C2800+PropertyOffsets[i]&&properties.Length==PropertyLengths[i]&&
                row.PropertiesPointerBaseOffset==0x1C2800&&Word(raw,0)==PropertyOffsets[i]&&
                Half(raw,0x36)==row.NativeClass&&row.RequiresBytePersistentOwner==(i!=0)&&
                row.Row.Id=="native"&&row.Properties.Id=="native"&&
                ReferenceEquals(row.Row.Extent.Space,rows[0].Row.Extent.Space)&&
                ReferenceEquals(row.Properties.Extent.Space,row.Row.Extent.Space),
                "Private row/property source offsets, native pointer base, class and persistence semantics remain exact.");
            check(row.OwnerFields.Count==(i==0?0:1)&&
                (i==0||row.OwnerFields[0]==new NativeMobyDependencyOwnerField(0,i==1?0:1)&&
                    Word(properties,0)==SourceIndices[i==1?0:1]),
                "Chest links to the actual key role; every reward links to the actual chest role.");
            var installed=native.Instances.Template.Rows[i];
            check(installed==row||SameSchema(installed,row),"Old Town instantiation consumes the extracted exact source schema.");
            check(Hash(raw)==row.Row.ExpectedSha256&&Hash(properties)==row.Properties.ExpectedSha256,
                "Source row and complete properties bytes match their retained read owners.");
        }
        check(Hash(source.ReadSource(rows[0].Row.Extent))=="db4fbcd35c285395e2be99ab6205061d9922ea58dccb849675d4c7f514081849"&&
            Hash(source.ReadSource(rows[1].Row.Extent))=="84d9f7aeb6a59adeb7a450f673c11f8f65453dab24a34b0242575b5e3baa910a",
            "Original key/chest row pins were not replaced with relocated Town rows.");
        check(source.ReadSource(rows[0].Properties.Extent).All(b=>b==0),"Initial native key state remains zero.");
        var package=source.PackageRead;byte[] packageBytes=source.CopyPackage();
        check(package.Id=="native"&&package.Extent.Offset==0x1B3978&&package.Extent.ByteLength==0x1008&&
            ReferenceEquals(package.Extent.Space,rows[0].Row.Extent.Space)&&
            package.ExpectedSha256==PackageSha256&&
            Hash(packageBytes)==package.ExpectedSha256&&Word(packageBytes,4)==0xFFFFFF2F,
            "AE retains the complete original animation package and model-local descriptor47, not Town descriptor54.");
        CheckSharedTextureGrammar(source,packageBytes,check);
        byte[] relocated=packageBytes.ToArray();
        check(TownSquareNativeLockedChestCandidateExporter.RebaseLockedChestDescriptors(relocated)==146,
            "All original animation/LOD face references still satisfy the existing exact146-descriptor grammar.");
        relocated[4]=0x36;
        check(native.Graph.CopyPayload("actor.ae").SequenceEqual(relocated),
            "The existing Town graph, not the source object, owns texture and local-sound relocation.");
        check(native.Graph.Nodes.Single(n=>n.Id=="actor.ae").Reads.Single()==package,
            "Town actor package extraction consumes the retained original read identity.");

        var handlers=source.HandlerReads;
        var expectedHandlers=new[]{
            ("setup",0x919C,0x30,"9343243adf185b81f638f37a41be77c61d27508c45158fed1ebe0b9fc6dd97ad"),
            ("state",0x9330,0x70C,"83c507315fcf747b0c94c6bd47c7622e79f38c68ff746421fd9b2021314a479f"),
            ("reward",0x3B64,0x434,"129d8511c63925af80ab73a02def446efc39e23974255fd4b8d38f51411c2056")};
        check(handlers.Count==3&&handlers.Select(r=>r.Id).SequenceEqual(expectedHandlers.Select(r=>r.Item1))&&
            native.Graph.Nodes.Single(n=>n.Id=="behavior.bundle").Reads.SequenceEqual(handlers.OrderBy(r=>r.Id,StringComparer.Ordinal)),
            "Town handler relocation consumes the three original reads in their unchanged IDs/order.");
        for(int i=0;i<handlers.Count;i++)
            check(handlers[i].Extent.Offset==expectedHandlers[i].Item2&&handlers[i].Extent.ByteLength==expectedHandlers[i].Item3&&
                handlers[i].ExpectedSha256==expectedHandlers[i].Item4&&Hash(source.ReadSource(handlers[i].Extent))==expectedHandlers[i].Item4,
                "Original setup/state/reward preimages stay independently pinned.");

        IReadOnlyDictionary<string,IReadOnlyList<NativeMobyDependencyRead>> inputs=source.ResourceInputs;
        check(inputs.Count>0&&inputs.Keys.Count(k=>k.StartsWith("texture.",StringComparison.Ordinal))==132,
            "The four source chest texture tiles retain all32 pixel rows and one CLUT each.");
        foreach(var pair in inputs)
        {
            var graphNode=native.Graph.Nodes.Single(n=>n.Id==pair.Key);
            check(pair.Value.Count>0&&pair.Value.All(r=>r.Extent.Space.Revision==NativeAddressRevision.Original&&
                Hash(source.ReadSource(r.Extent))==r.ExpectedSha256&&graphNode.Reads.Contains(r)),
                "Every exposed source resource read is actually consumed by the old graph, not an invented destination binding.");
        }
        check(inputs["key.native-handler"].Single().Extent.Offset==0x9114&&
            inputs["key.native-handler"].Single().Extent.ByteLength==0x160&&
            inputs["key.particle-descriptor"].Single().Extent.Offset==0x1725C8&&
            inputs["sound.chest-local54.equivalence"].Single(r=>r.Id=="donor-descriptor").Extent.Offset==0x160E20&&
            inputs["sound.chest-local54.equivalence"].Single(r=>r.Id=="donor-sample").Extent.Offset==0xC1B00,
            "Original Town support and donor local sound are named evidence inputs, not Fresh residency promises.");
        check(globals.Count==2&&globals.Select(g=>g.Role).SequenceEqual(new[]{"key-state","tracked-chest-gem"})&&
            globals.Select(g=>g.RuntimeAddress).SequenceEqual(new uint[]{0x80075830,0x80075758})&&
            globals.All(g=>g.ByteLength==4)&&globals[0].Consumers.SequenceEqual(new[]{inputs["key.native-handler"].Single(),handlers[1]})&&
            globals[1].Consumers[0]==handlers[1]&&globals[1].Consumers[1].Id=="award-tracked-gem"&&
            globals[1].Consumers[1].Extent.Offset==0x2C21C&&globals[1].Consumers[1].Extent.ByteLength==0x14,
            "Both native singleton state consumers remain source-bound and cannot become private by row copying.");
        check(native.Instances.Template.Globals.Zip(globals).All(p=>p.First.Role==p.Second.Role&&
            p.First.RuntimeAddress==p.Second.RuntimeAddress&&ReferenceEquals(p.First.ExecutableDomain,p.Second.ExecutableDomain)&&
            p.First.Consumers.SequenceEqual(p.Second.Consumers)),"Old instance allocator receives the exact extracted global obligations.");
        check(native.ModelRegistration.Packages.Count==1&&native.ModelRegistration.Packages[0].ActorId==0xAE,
            "Rootless key/reward controllers do not acquire fabricated model registrations.");

        var domains=new[]{rows[0].Row.Extent.Space,handlers[0].Extent.Space,
            inputs["key.particle-descriptor"].Single().Extent.Space,inputs["key.native-handler"].Single().Extent.Space,
            globals[0].ExecutableDomain};
        string[] ownerHashes=["c140bd422471c66cd57bd85b29912819f798f5b080779153674714889306c9d3",
            "299f4822489c48736c9f46bb9556e82952fc1693f5f9f42bc2e6415c1908aebc",
            "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0",
            "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5",
            "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"];
        var retained=domains.ToDictionary(d=>d,d=>source.ReadSource(new(d,0,d.ByteLength)));
        check(domains.Distinct().Count()==5&&domains.Select((d,i)=>Hash(retained[d])==ownerHashes[i]).All(v=>v),
            "All five complete Original byte owners remain pinned, including unrelated source complement.");
        foreach(var domain in domains)
        {
            var foreign=new NativeAddressSpace(domain.Kind,domain.OwnerIdentity,domain.Revision,domain.ByteLength);
            Refuse<InvalidDataException>(()=>source.ReadSource(new(foreign,0,4)),check);
            byte[] copy=source.ReadSource(new(domain,0,domain.ByteLength));copy[^1]^=1;
            Refuse<InvalidDataException>(()=>Decode(domains,e=>ReferenceEquals(e.Space,domain)?
                copy.AsSpan((int)e.Offset,(int)e.ByteLength).ToArray():source.ReadSource(e)),check);
            check(Hash(source.ReadSource(new(domain,0,domain.ByteLength)))==Hash(retained[domain]),
                "A corrupt source retry cannot alter an earlier retained owner.");
        }
        var replay=Decode(domains,source.ReadSource);replay.RequireRetainedOwners();
        check(replay.PrivateRows.Zip(rows).All(p=>SameSchema(p.First,p.Second))&&replay.HandlerReads.SequenceEqual(handlers)&&
            replay.CopyPackage().SequenceEqual(packageBytes),"Decode replay retains source roles, reads and bytes deterministically.");
        var aliasedInput=Decode(domains,e=>{
            check(e.Offset==0&&e.ByteLength==e.Space.ByteLength,"Decoder captures complete source owners, not caller-selected fragments.");
            return retained[e.Space];
        });
        foreach(var domain in domains)retained[domain][^1]^=1;
        aliasedInput.RequireRetainedOwners();
        check(domains.Select((d,i)=>Hash(aliasedInput.ReadSource(new(d,0,d.ByteLength)))==ownerHashes[i]).All(v=>v),
            "Decoder copies every borrowed source buffer before retaining its immutable owner.");
        packageBytes[0]^=1;var firstRow=source.ReadSource(rows[0].Row.Extent);firstRow[0]^=1;
        check(Hash(source.CopyPackage())==package.ExpectedSha256&&Hash(source.ReadSource(rows[0].Row.Extent))==rows[0].Row.ExpectedSha256,
            "Returned package and source bytes cannot mutate decoder state.");
        RequireReadOnly(rows,check);RequireReadOnly(globals,check);RequireReadOnly(handlers,check);
        foreach(var row in rows)RequireReadOnly(row.OwnerFields,check);
        foreach(var global in globals)RequireReadOnly(global.Consumers,check);
        foreach(var reads in inputs.Values)RequireReadOnly(reads,check);
        if(inputs is IDictionary<string,IReadOnlyList<NativeMobyDependencyRead>> dictionary)
            Refuse<NotSupportedException>(()=>dictionary.Clear(),check);
        source.RequireRetainedOwners();

        bool spatial=native.Recipe==NativeTownSquareClosureRecipe.SpatiallySeparatedV3;
        check(native.Graph.Fingerprint==(spatial?"52c68e8595a21ef8807abb6090dfb02d84a8643af114346c3c7c962247c45d6d":
            "d484fc13504ac0afc0b7a63e81dd4a6a3a3a7672dd7959654b02fd6103a3edcc")&&
            Hash(native.Data)==(spatial?"880c487599e8382447cd7b5c2d1799abec4eafd5af26351f37a22f66ed8ec9b0":
            "e442a02d3aa899dca0d5ddac4392f4b602aabb7ac6dc5dd1e4616c00fab6908d")&&
            Hash(native.Overlay)=="fd859bff13985e18c3aff74cc889006c48b2743bf57a2b0f9d80662d25a09c2d"&&
            Hash(native.Executable)=="4268f9943233b9f8c24d1d4b0924cba8747547b44b82eddd13f0a2ac10ac17de",
            "Source extraction preserves the complete historical V2/V3 graph, data, overlay and executable goldens.");
        Console.WriteLine($"NATIVE_LOCKED_CHEST_SOURCE_EXTRACTION_AND_OLD_TOWN_GOLDENS_OK recipe={native.Recipe} destination=false runtime=false");
    }

    private static void CheckSharedTextureGrammar(NativeLockedChestFamilySource source,byte[] package,Action<bool,string> check)
    {
        // Exercise the shared renderer grammar independently of the Town rebaser.
        // These synthetic descriptor destinations do not allocate or install VRAM.
        var layout=NativeMobyModelTextureLayout.Decode(package);
        check(layout.PackageSha256==source.PackageRead.ExpectedSha256&&layout.References.Count==146,
            $"Shared AE texture grammar decodes the complete original package and exactly146 references; actual={layout.References.Count}.");
        (ushort Page,int U,int V,ushort Clut)[] regions=[
            (0xE,192,192,0x4A2A),(0xE,192,224,0x4C2A),
            (0xF,0,160,0x5C2A),(0xF,0,192,0x5E2A)];
        var groups=layout.References.GroupBy(r=>(r.Page,r.Palette)).ToDictionary(g=>g.Key,g=>g.ToArray());
        check(groups.Count==4&&regions.All(r=>groups.ContainsKey((r.Page,r.Clut))),
            "Shared AE descriptor regions must be exactly E/4A2A,E/4C2A,F/5C2A,F/5E2A; actual="+
            string.Join(",",groups.Keys.OrderBy(k=>k.Page).ThenBy(k=>k.Palette).Select(k=>$"{k.Page:X}/{k.Palette:X}")));
        byte[] rebased=package.ToArray();bool[] fields=new bool[package.Length];
        int[] uvOffsets=[0,4,8,10];
        for(int i=0;i<regions.Length;i++)
        {
            var region=regions[i];var references=groups[(region.Page,region.Clut)];
            var coordinates=references.SelectMany(r=>{
                byte[] bytes=r.CopyBytes();return uvOffsets.Select(at=>(U:(int)bytes[at],V:(int)bytes[at+1]));
            }).ToArray();
            int minU=coordinates.Min(p=>p.U),maxU=coordinates.Max(p=>p.U);
            int minV=coordinates.Min(p=>p.V),maxV=coordinates.Max(p=>p.V);
            check(minU==region.U&&maxU==region.U+31&&minV==region.V&&maxV==region.V+31,
                $"AE region{i} uses all four UV pairs within its exact32x32 source footprint; actual U={minU}..{maxU}, V={minV}..{maxV}.");
            int pixelX=(region.Page&15)*128+region.U/2-1024;
            int pixelY=((region.Page&16)!=0?256:0)+region.V;
            for(int row=0;row<32;row++)
            {
                var read=source.ResourceInputs[$"texture.{i}.{row}"].Single();
                check(ReferenceEquals(read.Extent.Space,source.PackageRead.Extent.Space)&&
                    read.Extent.Offset==0x800+(pixelY+row)*1024+pixelX&&read.Extent.ByteLength==16&&
                    Hash(source.ReadSource(read.Extent))==read.ExpectedSha256,
                    $"Shared AE footprint{i} row{row} resolves the exact retained Original16-byte pixel owner.");
            }
            var palette=source.ResourceInputs[$"texture.{i}.32"].Single();
            int paletteOffset=0x800+(region.Clut>>6)*1024+(region.Clut&63)*32-1024;
            check(ReferenceEquals(palette.Extent.Space,source.PackageRead.Extent.Space)&&
                palette.Extent.Offset==paletteOffset&&palette.Extent.ByteLength==32&&
                Hash(source.ReadSource(palette.Extent))==palette.ExpectedSha256,
                $"Shared AE footprint{i} resolves its complete retained Original32-byte CLUT owner.");
            int deltaU=16+32*i-region.U,deltaV=48+32*i-region.V;
            ushort targetPage=(ushort)(8+i),targetClut=(ushort)(0x2820+i);
            foreach(var reference in references)
            {
                byte[] before=reference.CopyBytes();int at=reference.PackageOffset;
                check(!fields.AsSpan(at,12).Contains(true)&&package.AsSpan(at,12).SequenceEqual(before),
                    "Each shared AE descriptor has one disjoint exact original12-byte field owner.");
                Array.Fill(fields,true,at,12);
                byte[] after=NativeMobyModelTextureLayout.Rebase(reference,deltaU,deltaV,targetClut,targetPage);
                check(uvOffsets.All(o=>after[o]==before[o]+deltaU&&after[o+1]==before[o+1]+deltaV)&&
                    Half(after,2)==targetClut&&Half(after,6)==targetPage,
                    "Shared Rebase translates all four AE UV pairs and only their descriptor words.");
                after.CopyTo(rebased,at);
            }
        }
        var afterLayout=NativeMobyModelTextureLayout.Decode(rebased);
        check(afterLayout.References.Count==146&&afterLayout.FaceTableOffsets.SequenceEqual(layout.FaceTableOffsets)&&
            afterLayout.References.Select(r=>r.PackageOffset).SequenceEqual(layout.References.Select(r=>r.PackageOffset))&&
            Enumerable.Range(0,package.Length).All(i=>fields[i]||rebased[i]==package[i]),
            "Synthetic AE rebasing preserves the complete non-descriptor package complement and actual renderer field topology.");
        byte[] recovered=rebased.ToArray();
        var afterFields=afterLayout.References.ToDictionary(r=>r.PackageOffset);
        foreach(var reference in layout.References)
        {
            int i=Array.FindIndex(regions,r=>r.Page==reference.Page&&r.Clut==reference.Palette);
            var region=regions[i];
            byte[] before=NativeMobyModelTextureLayout.Rebase(afterFields[reference.PackageOffset],
                region.U-(16+32*i),region.V-(48+32*i),region.Clut,region.Page);
            before.CopyTo(recovered,reference.PackageOffset);
        }
        check(recovered.AsSpan().SequenceEqual(package)&&Hash(recovered)==source.PackageRead.ExpectedSha256&&
            source.CopyPackage().AsSpan().SequenceEqual(package),
            "Inverse shared AE rebasing recovers the complete exact Original package without mutating retained source bytes.");
        check(!layout.CompleteModelClosureProven&&!layout.PublicationApproved&&
            !afterLayout.CompleteModelClosureProven&&!afterLayout.PublicationApproved,
            "Shared AE texture grammar and synthetic roundtrip do not issue complete model, residency or import eligibility.");
        Console.WriteLine("NATIVE_LOCKED_CHEST_SHARED_TEXTURE_GRAMMAR_146_FOUR_SOURCE_FOOTPRINTS_ROUNDTRIP_OK residency=false eligibility=false");
    }

    private static NativeLockedChestFamilySource Decode(NativeAddressSpace[] domains,Func<NativeByteExtent,byte[]> read)=>
        NativeLockedChestFamilySource.DecodeOriginal(domains[0],domains[1],domains[2],domains[3],domains[4],read);
    private static NativeAddressSpace[] Domains()=>[
        new(NativeAddressKind.LevelData,"fixture-peace-data",NativeAddressRevision.Original,0x29E800),
        new(NativeAddressKind.LevelData,"fixture-peace-overlay",NativeAddressRevision.Original,0x12800),
        new(NativeAddressKind.LevelData,"fixture-town-data",NativeAddressRevision.Original,0x2E2000),
        new(NativeAddressKind.LevelData,"fixture-town-overlay",NativeAddressRevision.Original,0xF800),
        new(NativeAddressKind.ExecutableFile,"fixture-exe",NativeAddressRevision.Original,0x66000)];
    private static bool SameSchema(NativeMobyDependencyPrivateSchema a,NativeMobyDependencyPrivateSchema b)=>
        a.Role==b.Role&&a.SourceInstanceIndex==b.SourceInstanceIndex&&a.NativeClass==b.NativeClass&&a.Row==b.Row&&a.Properties==b.Properties&&
        a.PropertiesPointerBaseOffset==b.PropertiesPointerBaseOffset&&a.RequiresBytePersistentOwner==b.RequiresBytePersistentOwner&&
        a.OwnerFields.SequenceEqual(b.OwnerFields);
    private static void RequireReadOnly<T>(IReadOnlyList<T> items,Action<bool,string> check)
    {
        if(items is IList<T> list)Refuse<NotSupportedException>(()=>list.Clear(),check);
        else check(true,"Read-only sequence exposes no writable list interface.");
    }
    private static void Refuse<T>(Action action,Action<bool,string> check)where T:Exception
    {
        try{action();}catch(T){check(true,"Expected source-decoder refusal "+typeof(T).Name);return;}
        throw new Exception("Expected source-decoder refusal "+typeof(T).Name);
    }
    private static uint Word(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static ushort Half(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
