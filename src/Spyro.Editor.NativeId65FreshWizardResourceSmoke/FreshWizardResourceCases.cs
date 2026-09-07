// Tests-first fixed A/N16 research resource contract. No output writer.
using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

internal static class FreshWizardResourceCases
{
    private const string UploadHash = "d74c2571ed857865b2b989af3001117f44e0915f9c6d59927e30a49d90a79402";
    private const string DataHash = "bea93c58d6d568c7c6162816ddad1afff32a51886f9412a0809b22887e0b83d4";
    private const string ModelsHash = "782c4500b3152f6051b45b8a0a56f6c99c4f703c753948f656318ccfa8618cda";
    private const string SceneHash = "4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433";
    private const string BodyHash = "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c";
    private static int checks, refusals;

    internal static NativeId65FreshDocumentIntent A(int field = -1) =>
        NativeId65FreshDocumentIntent.Create(
            field == 0 ? 8464 : 8448, field == 1 ? 8464 : 8448, field == 2 ? 8720 : 8704,
            field == 3 ? 1040 : 1024, field == 4 ? 1040 : 1024,
            field == 5 ? 8976 : 8960, field == 6 ? 8976 : 8960, field == 7 ? 65 : 64,
            new((byte)(field == 8 ? 81 : 80), (byte)(field == 9 ? 129 : 128), (byte)(field == 10 ? 113 : 112)),
            new((byte)(field == 11 ? 129 : 128), (byte)(field == 12 ? 97 : 96), (byte)(field == 13 ? 65 : 64)),
            field == 14 ? 2 : 19);

    internal static async Task SourceFree()
    {
        Type t = typeof(NativeId65FreshWizardResearchResourcePlan);
        Check(t.IsSealed && t.GetConstructors().Length == 0, "No caller construction/inheritance authority.");
        Check(t.GetProperties().All(p => p.SetMethod == null), "No public mutable result metadata.");
        MethodInfo[] factories = t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Check(factories.Length == 1 && factories[0].Name == "CaptureAsync" &&
            factories[0].GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] {
                typeof(string), typeof(NativeId65FreshDocumentCompilation), typeof(NativeWizardFamilyAssetPlan), typeof(CancellationToken)
            }), "Only actual issued inputs; no public bytes/hash/flags/policy or source catalog.");
        Check(t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .All(m => !new[] { "Publish", "Write", "CompileBody", "CompileScene", "CompileOverlay" }.Any(n => m.Name.Contains(n, StringComparison.Ordinal))),
            "No output or body/scene/overlay writer on the research resource type.");
        Check(typeof(NativeId65FreshDocumentCompilation).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).Order().SequenceEqual(new[] { "CanonicalSha256", "DiscPlan", "Intent" }),
            "Ancestry retention adds no public JSON property to existing Compilation.");
        await Reject<ArgumentNullException>(() => NativeId65FreshWizardResearchResourcePlan.CaptureAsync("not-opened.bin", null!, null!));
        using (var c = new CancellationTokenSource())
        {
            c.Cancel();
            await Reject<OperationCanceledException>(() => NativeId65FreshWizardResearchResourcePlan.CaptureAsync(null!, null!, null!, c.Token));
        }
        NativeId65FreshWizardResearchResourceSource.RequireFixedIntent(A());
        for (int field = 0; field < 15; field++)
            Reject<InvalidDataException>(() => NativeId65FreshWizardResearchResourceSource.RequireFixedIntent(A(field)));
        // This neutral internal guard does not mint an issued resource/source.
        byte[] ownerTags = new byte[0x80000];
        ownerTags[0] = 1;
        Reject<InvalidDataException>(() => NativeId65FreshWizardResearchResourceSource.RequireBackground(ownerTags, 0, 0, 1, 1, false));
        NativeId65FreshWizardResearchResourceSource.RequireBackground(ownerTags, 32, 0, 32, 1, true);
        foreach (var v in new[] {
            (-1,0,16,32,false), (1008,0,32,32,false), (112,0,32,32,false),
            (16,250,16,32,false), (1,0,32,1,true), (0,512,32,1,true),
            (0,0,int.MaxValue,32,false), (0,int.MaxValue,16,32,false)
        }) Reject<InvalidDataException>(() => NativeId65FreshWizardResearchResourceSource.RequireBackground(ownerTags, v.Item1,v.Item2,v.Item3,v.Item4,v.Item5));
        Array.Fill(ownerTags,(byte)1);
        Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchResourceSource.FindBackground(ownerTags,32,1,true));
        Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchResourceSource.FindBackground(ownerTags,16,32,false));
        Console.WriteLine($"FRESH_WIZARD_RESOURCE_FREE_OK checks={checks} refusals={refusals}");
    }

    internal static async Task Source(string original)
    {
        RequireOriginal(original);
        try
        {
            var intent = A();
            var compiled = await NativeId65FreshDocumentCompilation.CaptureAsync(original, intent);
            var family = await NativeWizardFamilyAssetPlan.CaptureAsync(original);
            await Reject<ArgumentNullException>(()=>NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,compiled,null!));
            string compilationJson = JsonSerializer.Serialize(compiled);
            byte[] oldBody = compiled.DiscPlan.CopyBody();
            Check(Hash(oldBody) == BodyHash, "Whole actual original A body is the independent baseline, not a requested output.");
            var fresh = compiled.FreshResourceAncestor;
            var chain = fresh.ResourceChainAncestor;
            byte[] oldUpload = fresh.CopyUploadPayload(), oldData = chain.CopyDataComponents(), oldModels = chain.CopyModelPayload();
            Check(oldUpload.Length == 0xDE000 && oldData.Length == 0x8C8 && oldModels.Length == 0xF800 &&
                Hash(fresh.CopyScene()) == SceneHash, "Actual private ancestor bytes and exact complete scene.");
            var p = await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original, compiled, family);
            p.RequireAncestry(compiled, family);
            Check(ReferenceEquals(p.CompilationAncestor, compiled) && ReferenceEquals(p.FamilyAncestor, family),
                "Private actual issued ancestry, not matching reported hashes.");
            byte[] upload = p.CopyUploadPayload(), data = p.CopyDataComponents(), models = p.CopyModelPayload();
            Check(upload.Length == 0xE5800 && Hash(upload) == UploadHash && data.Length == 0x8FC && Hash(data) == DataHash &&
                models.Length == 0x17800 && Hash(models) == ModelsHash, "All three pre-Core independent raw goldens.");
            byte[] paddedData = new byte[0x1000]; data.CopyTo(paddedData, 0);
            Check(Hash(paddedData) == "1536654261475fcab6b0c0e3e4c4997f899a045ce92eb3efc36a5fc94624cffe", "Test-only padding golden; no body emitted.");
            Check(Hash(upload.AsSpan(0,0x80000)) == "c44226f0b677da153c2da4661831471393a7a11ed69d28c6bb08dbad5212a1d5" &&
                Hash(upload.AsSpan(0x80000)) == "f922913cf797ee91f0e0ccb909e511a2746c9f28a5df9d8ca2b54934979b968c", "Complete independent VRAM and bank owners.");
            p.RequireExactOutputs(upload, data, models);
            NativeId65FreshWizardResearchResourceSource.RequireInstalledGrammar(data, models);
            Reject<InvalidDataException>(() => NativeId65NormalDataLayout.DecodeUntextured(data));
            Check(NativeId65NormalDataLayout.DecodeUntextured(oldData).UsedByteLength == oldData.Length,
                "Old grammar still accepts its old chain but refuses the new11/78 chain inside its length range.");
            RequireOwners(p, upload, data, models);
            RequireVram(p, family, fresh, oldUpload, upload);
            RequirePatches(p, family, oldData, upload, data, models);
            RequireDataAndAudio(p, family, oldUpload, oldData, upload, data, models);
            RequireModels(family, oldModels, models);
            Check(p.ModelRoots.Select(r=>(r.ClassId,r.Offset,r.ByteLength,r.RegistrationExtentByteLength)).SequenceEqual(new[] {
                ((ushort)0,0,0x10A4C,0x10A4C), ((ushort)0x78,0x10A4C,0x2BD0,0x2BD0),
                ((ushort)0x26,0x1361C,0x564,0x564), ((ushort)0x11B,0x13B80,0x35B4,0x3C80)
            }), "Actual ordered model roots; original package versus final native allocation extent.");

            // Real production validators, not an expected-hash lambda in test.
            foreach (int at in new[] { 8, 0x150, 0x154, 0x1D0, 0x1D2, 0x1DC, 0x2E0, 0x8FB })
            {
                byte[] b = data.ToArray(); b[at] ^= 1;
                Reject<InvalidDataException>(() => p.RequireExactOutputs(upload, b, models));
            }
            foreach ((int at,uint value) in new[] { (8,173u),(8,0x10000u),(0x150,0x90u),(0x154,12u),(0x1D0,0x00080015u),(0x1D0,0x00080040u),(0x2E0,79u) })
            {
                byte[] b = data.ToArray(); Put(b,at,value);
                Reject<InvalidDataException>(() => NativeId65FreshWizardResearchResourceSource.RequireInstalledGrammar(b, models));
            }
            Reject<InvalidDataException>(() => NativeId65FreshWizardResearchResourceSource.RequireInstalledGrammar(data[..^1], models));
            byte[] noTerminator = models.ToArray(); Put(noTerminator,0x10A48,8);
            Reject<InvalidDataException>(() => NativeId65FreshWizardResearchResourceSource.RequireInstalledGrammar(data,noTerminator));
            foreach (int at in new[] { 0, 0x10A4C+0x2930, 0x1712C, 0x177FF })
            {
                byte[] b = models.ToArray(); b[at] ^= 1;
                Reject<InvalidDataException>(() => p.RequireExactOutputs(upload,data,b));
            }
            foreach (int at in new[] { 0, 0x80000, 0xDE000, 0xE57FF })
            {
                byte[] b = upload.ToArray(); b[at] ^= 1;
                Reject<InvalidDataException>(() => p.RequireExactOutputs(b,data,models));
            }
            byte[] copy = p.CopyUploadPayload(); copy[0] ^= 1;
            Check(Hash(p.CopyUploadPayload()) == UploadHash, "No whole-buffer alias after mutation/refusals.");
            Check(JsonSerializer.Serialize(compiled) == compilationJson && compiled.DiscPlan.CopyBody().SequenceEqual(oldBody) &&
                fresh.CopyUploadPayload().SequenceEqual(oldUpload) && chain.CopyDataComponents().SequenceEqual(oldData) &&
                chain.CopyModelPayload().SequenceEqual(oldModels) && Hash(fresh.CopyScene()) == SceneHash,
                "No ancestry/public JSON/old resource/body/scene mutation.");
            var repeated = await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,compiled,family);
            Check(repeated.PlanSha256 == p.PlanSha256, "Deterministic same actual issued pair.");
            var sameBytesDifferentFamily = await NativeWizardFamilyAssetPlan.CaptureAsync(original);
            Reject<InvalidDataException>(() => p.RequireAncestry(compiled,sameBytesDifferentFamily));
            var sameBytesDifferentCompilation = await NativeId65FreshDocumentCompilation.CaptureAsync(original,A());
            Reject<InvalidDataException>(() => p.RequireAncestry(sameBytesDifferentCompilation,family));
            Check(sameBytesDifferentCompilation.CopyCanonicalDocument().SequenceEqual(compiled.CopyCanonicalDocument()),
                "The reference negative is actually byte-identical, not an easier changed-content case.");
            var independentlyPaired = await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,sameBytesDifferentCompilation,sameBytesDifferentFamily);
            Check(Hash(independentlyPaired.CopyUploadPayload()) == UploadHash, "Independently issued exact pair can make its own immutable result.");
            var edited = await NativeId65FreshDocumentCompilation.CaptureAsync(original,A(0));
            await Reject<InvalidDataException>(() => NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,edited,family));
            foreach(int coordinate in new[]{0,258048})
            {
                var extreme=NativeId65FreshDocumentIntent.Create(coordinate,coordinate,coordinate,16,16,
                    coordinate+1,coordinate+1,0,new(80,128,112),new(128,96,64),2);
                var extremeCompiled=await NativeId65FreshDocumentCompilation.CaptureAsync(original,extreme);
                Check(extremeCompiled.Intent.FloorX==coordinate,"Actual valid min/max compilation, not a fabricated byte shape.");
                await Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,extremeCompiled,family));
            }
            Check(!p.GlobalAudioCompatibilityProven && !p.RuntimeVerified && !p.CompleteNativeDependencyClosureProven && !p.PublicationApproved,
                "Research global1F/body/runtime/publication boundaries remain false.");
        }
        finally { RequireOriginal(original); }
        Console.WriteLine($"FRESH_WIZARD_RESOURCE_SOURCE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }

    private static void RequireOwners(NativeId65FreshWizardResearchResourcePlan p, byte[] upload, byte[] data, byte[] models)
    {
        foreach (var domain in Enum.GetValues<NativeFreshWizardResourceDomain>())
        {
            byte[] bytes = domain switch { NativeFreshWizardResourceDomain.Upload => upload, NativeFreshWizardResourceDomain.Data => data, _ => models };
            int end = 0;
            foreach (var o in p.Owners.Where(o => o.Domain == domain))
            {
                Check(o.Offset == end && o.ByteLength > 0 && o.Offset+(long)o.ByteLength <= bytes.Length &&
                    Hash(bytes.AsSpan(o.Offset,o.ByteLength)) == o.Sha256, "Complete ordered immutable buffer owner partition.");
                end += o.ByteLength;
            }
            Check(end == bytes.Length, "No gap/unowned suffix in any resource domain.");
        }
    }

    private static void RequireVram(NativeId65FreshWizardResearchResourcePlan p, NativeWizardFamilyAssetPlan family,
        NativeId65FreshSceneBundle fresh, byte[] oldUpload, byte[] upload)
    {
        bool[] protectedBytes = new bool[0x80000], inserted = new bool[0x80000];
        foreach (var owner in fresh.UploadOwners.Where(o=>o.Offset<0x80000))
        {
            Check(owner.Offset>=0 && owner.ByteLength>0 && owner.Offset+(long)owner.ByteLength<=0x80000,
                "Every old VRAM owner is bounded before filling the mask.");
            if(owner.Provenance!="fresh-zero-upload-background")
                Array.Fill(protectedBytes,true,owner.Offset,owner.ByteLength);
        }
        Check(Enumerable.Range(0,0x80000).Count(i=>protectedBytes[i] && oldUpload[i]==0)==17369,
            "Actual source-owned zero pixels remain protected, not free-by-value.");
        byte[] actualMask=protectedBytes.Select(b=>b?(byte)1:(byte)0).ToArray();int rejectedProtectedZeros=0;
        for(int i=0;i<actualMask.Length;i++)if(protectedBytes[i] && oldUpload[i]==0)
        {
            try{NativeId65FreshWizardResearchResourceSource.RequireBackground(actualMask,i%1024,i/1024,1,1,false);}
            catch(InvalidDataException){rejectedProtectedZeros++;}
        }
        Check(rejectedProtectedZeros==17369,"All17369 actual protected zero-byte reservations individually refused; aggregate count.");
        Check(p.TexturePlacements.Count==10, "Five independent complete pixel/palette pairs.");
        foreach(var place in p.TexturePlacements)
        {
            byte[] source = family.CopyOwner(place.SourceOwnerId);
            Check(place.X>=0 && place.Y>=0 && place.RowBytes>0 && place.Rows>0 &&
                place.X+(long)place.RowBytes<=1024 && place.Y+(long)place.Rows<=512 &&
                place.RowBytes*(long)place.Rows==source.Length, "Whole strided source placement bounds before index math.");
            int cursor=0;
            for(int row=0;row<place.Rows;row++) for(int col=0;col<place.RowBytes;col++)
            {
                int at=(place.Y+row)*1024+place.X+col;
                Check(!protectedBytes[at] && !inserted[at] && upload[at]==source[cursor++],
                    "Every inserted byte resolves exact source and never aliases retained/other new owner.");
                inserted[at]=true;
            }
        }
        Check(inserted.Count(b=>b)==2216 && Enumerable.Range(0,0x80000).All(i=>inserted[i] || upload[i]==oldUpload[i]),
            "Complete allocated and retained/unallocated VRAM complement, including zero bytes.");
    }

    private static void RequirePatches(NativeId65FreshWizardResearchResourcePlan p, NativeWizardFamilyAssetPlan family,
        byte[] oldData, byte[] upload, byte[] data, byte[] models)
    {
        Check(p.Patches.Count == 71, "61texture +3local +2sampleoffset +2particle +2sound +1map fields.");
        var touched = new HashSet<(NativeFreshWizardResourceDomain,int)>();
        var inverse = new Dictionary<string,byte[]> {
            ["wizard.model"] = models.AsSpan(0x13B80,0x35B4).ToArray(), ["bolt.model"] = models.AsSpan(0x1361C,0x564).ToArray(),
            ["particle7"] = data.AsSpan(0x1D0,12).ToArray(), ["global1F.descriptor"] = data.AsSpan(0x8D4,20).ToArray(),
            ["local0.descriptor"] = data.AsSpan(0x8E8,20).ToArray()
        };
        foreach (var patch in p.Patches)
        {
            byte[] before = patch.CopyBefore(), after = patch.CopyAfter();
            byte[] final = patch.Domain switch { NativeFreshWizardResourceDomain.Upload => upload, NativeFreshWizardResourceDomain.Data => data, _ => models };
            Check(before.Length == after.Length && before.Length > 0 && patch.Offset >= 0 && patch.Offset+(long)after.Length <= final.Length,
                "Complete bounded patch shape before any indexing.");
            Check(final.AsSpan(patch.Offset,after.Length).SequenceEqual(after) &&
                Enumerable.Range(patch.Offset,after.Length).All(i => touched.Add((patch.Domain,i))), "Exact nonoverlapping final patch owner.");
            byte[] original = patch.SourceOwnerId == "fresh.data" ? oldData : family.CopyOwner(patch.SourceOwnerId);
            Check(patch.SourceOffset >= 0 && patch.SourceOffset+(long)before.Length <= original.Length &&
                original.AsSpan(patch.SourceOffset,before.Length).SequenceEqual(before), "Source preimage resolves actual retained source owner.");
            if(inverse.TryGetValue(patch.SourceOwnerId,out byte[]? owner))
                before.CopyTo(owner.AsSpan(patch.SourceOffset));
            before[0] ^= 1; after[0] ^= 1;
            Check(!patch.CopyBefore().SequenceEqual(before) && !patch.CopyAfter().SequenceEqual(after), "Patch arrays never leak mutable authority.");
        }
        foreach(var owner in inverse)
            Check(owner.Value.SequenceEqual(family.CopyOwner(owner.Key)), "Whole source-form package/particle/descriptor recovered by declared inverse only.");
    }

    private static void RequireDataAndAudio(NativeId65FreshWizardResearchResourcePlan p, NativeWizardFamilyAssetPlan family,
        byte[] oldUpload, byte[] oldData, byte[] upload, byte[] data, byte[] models)
    {
        Check(data.AsSpan(0,0x150).SequenceEqual(oldData.AsSpan(0,0x150)) && Word(data,0x150)==0x8C && Word(data,0x154)==11 &&
            data.AsSpan(0x158,0x78).SequenceEqual(oldData.AsSpan(0x158,0x78)), "Whole surface/portal prefix and ten particle records unchanged.");
        Check(Word(data,0x1D0)==0x00080007 && Word(data,0x1DC)==0x720 && Word(data,0x2E0)==78, "Exact new native component grammar.");
        Check(upload.AsSpan(0x80000,0x5E000).SequenceEqual(oldUpload.AsSpan(0x80000)) &&
            data.AsSpan(0x2E4,76*20).SequenceEqual(oldData.AsSpan(0x2D8,76*20)), "All old76 descriptors and complete retained bank.");
        for (int selector=0; selector<256; selector++)
            Check(data[0x1E0+selector] == (selector==0x1F ? (byte)76 : oldData[0x1D4+selector]), "One declared global map patch; other255 exact.");
        Check(data[0x206]==22 && Word(data,0x2E4+22*20+12)==0x20028 && models.AsSpan(0x13B84,3).SequenceEqual(new byte[]{77,66,67}),
            "Global26 nonzero controls and local/global namespaces are not normalized or interchanged.");
        Check(Word(data,0x8D4)==0x5E000 && Word(data,0x8E8)==0x5FD40 &&
            upload.AsSpan(0xDE000,0x1D40).SequenceEqual(family.CopyOwner("global1F.sample")) &&
            upload.AsSpan(0xDFD40,0x5430).SequenceEqual(family.CopyOwner("local0.sample")) &&
            upload.AsSpan(0xE5170).ToArray().All(b=>b==0), "Exact two complete ADPCM additions and separately owned round padding.");
        Check(p.SoundBindings.Any(s=>s.Namespace==NativeWizardFamilySoundNamespace.GlobalSelector && s.Selector==0x1F &&
            s.TargetDescriptorIndex==76 && s.PreviousTargetDescriptorIndex==0), "Actual occupied-selector replacement remains visible in typed metadata.");
    }

    private static void RequireModels(NativeWizardFamilyAssetPlan family, byte[] old, byte[] models)
    {
        Check(models.AsSpan(0,0xCC2C).SequenceEqual(old.AsSpan(0,0xCC2C)) &&
            models.AsSpan(0xCC2C,0x3E1C).SequenceEqual(family.CopyOwner("player.animation8")) &&
            Word(models,0x10A48)==uint.MaxValue, "Old18/19/29/40 stream and full animation8 before terminator.");
        Check(models.AsSpan(0x10A4C,0x2BD0).SequenceEqual(old.AsSpan(0xCC30,0x2BD0)) &&
            models.AsSpan(0x1712C,8).SequenceEqual(family.CopyOwner("wizard.model").AsSpan(0x35AC)) &&
            models.AsSpan(0x17134).ToArray().All(b=>b==0), "Whole companion allocation, original Wizard padding and new6CC padding are distinct.");
    }

    private static void RequireOriginal(string path)
    {
        using var s = File.OpenRead(path);
        Check(s.Length==661547040 && Convert.ToHexStringLower(SHA256.HashData(s))==
            "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37" &&
            s.Position==661547040 && s.ReadByte()==-1 && s.Length==661547040 && s.Position==661547040,
            "Whole original exact SHA/length/actual EOF before/finally; no source writes.");
    }
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static uint Word(byte[] b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
    private static void Put(byte[] b,int at,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at),v);
    private static void Check(bool ok,string label) { if(!ok)throw new InvalidDataException(label); checks++; }
    private static void Reject<T>(Action action) where T:Exception
    { try{action();}catch(T){checks++;refusals++;return;} throw new InvalidDataException("Expected refusal "+typeof(T).Name); }
    private static async Task Reject<T>(Func<Task> action) where T:Exception
    { try{await action();}catch(T){checks++;refusals++;return;} throw new InvalidDataException("Expected refusal "+typeof(T).Name); }
}
