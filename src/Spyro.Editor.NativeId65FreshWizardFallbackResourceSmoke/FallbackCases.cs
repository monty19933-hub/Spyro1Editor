using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class FallbackCases
{
    private static int checks,refusals;
    private const string UploadGolden="8c4438fc13eca3b3bd40e9c056094c308fb272aded227577fb812ec9b16d96dc";
    private const string ModelGolden="84d00dbbce264bc08c61abfe2579fe3d7a7464ba2df6b189834664da10b82c84";
    private static NativeId65FreshDocumentIntent A()=>NativeId65FreshDocumentIntent.Create(
        8448,8448,8704,1024,1024,8960,8960,64,new(80,128,112),new(128,96,64),19);
    internal static async Task SourceFree()
    {
        var t=typeof(NativeId65FreshWizardResearchFallbackResourcePlan);
        Check(t.IsSealed && t.GetConstructors().Length==0 && t.GetProperties().All(p=>p.SetMethod==null),"Closed immutable result.");
        var api=t.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
        Check(api.Length==1&&api[0].Name=="CaptureAsync"&&api[0].GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
            typeof(string),typeof(NativeId65FreshWizardResearchResourcePlan),typeof(CancellationToken)}),"Only actual resource plus exact source capture, no caller roster/bytes/hash/flag.");
        Check(!t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Any(m=>
            new[]{"Publish","Write","CompileBody","CompileScene","CompileOverlay"}.Any(s=>m.Name.StartsWith(s,StringComparison.Ordinal))),"Detached no-output API.");
        using(var c=new CancellationTokenSource())
        {c.Cancel();await Reject<OperationCanceledException>(()=>NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(null!,null!,c.Token));}
        await Reject<ArgumentNullException>(()=>NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync("not-opened.bin",null!));
        // Synthetic neutral shape is NOT a source certificate or package issuer.
        byte[] simple=new byte[0x7EC];Put(simple,0,0xFF31A178);Put(simple,4,0x178);Put(simple,8,0x2E0);Put(simple,12,0x2E4);
        NativeTownFallbackSimpleModel.RequireShape(simple);
        Reject<InvalidDataException>(()=>NativeMobyModelTextureLayout.Decode(simple));
        foreach(int length in new[]{0,15,0x7EB,0x7ED,0x800})
            Reject<InvalidDataException>(()=>NativeTownFallbackSimpleModel.RequireShape(new byte[length]));
        foreach(int at in Enumerable.Range(0,16))
        {byte[] bad=simple.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>NativeTownFallbackSimpleModel.RequireShape(bad));}
        NativeId65FreshWizardResearchFallbackResourceSource.RequireTextureOwner(0,0,42,false);
        NativeId65FreshWizardResearchFallbackResourceSource.RequireTextureOwner(1,0,0,true);
        for(int tag=0;tag<256;tag++)
        {
            if(tag!=0)Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchFallbackResourceSource.RequireTextureOwner((byte)tag,0,0,false));
            if(tag!=1)Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchFallbackResourceSource.RequireTextureOwner((byte)tag,0,0,true));
        }
        Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchFallbackResourceSource.RequireTextureOwner(0,1,1,false));
        Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchFallbackResourceSource.RequireTextureOwner(1,0,1,true));
        Check(simple.AsSpan(16).ContainsAnyExcept((byte)0)==false,"Neutral checks do not mutate data.");
        Console.WriteLine($"FRESH_WIZARD_FALLBACK_RESOURCE_FREE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }

    internal static async Task Source(string original)
    {
        RequireOriginal(original);
        try
        {
            var c=await NativeId65FreshDocumentCompilation.CaptureAsync(original,A());
            var f=await NativeWizardFamilyAssetPlan.CaptureAsync(original);
            var r=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,c,f);
            byte[] upload=r.CopyUploadPayload(),data=r.CopyDataComponents(),models=r.CopyModelPayload();
            string resourceJson=JsonSerializer.Serialize(r),compilationJson=JsonSerializer.Serialize(c);
            var engine=await NativeId65EngineProfileContract.CaptureAsync(original);byte[] town=engine.CopyTownData();
            var p=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(original,r);
            Check(ReferenceEquals(p.ResourceAncestor,r),"Actual prior resource reference retained.");p.RequireAncestry(r);
            byte[] nextUpload=p.CopyUploadPayload(),nextData=p.CopyDataComponents(),nextModels=p.CopyModelPayload();
            Check(nextUpload.Length==0xE5800 && nextData.Length==0x8FC && nextModels.Length==0x19000 &&
                p.UploadSha256==UploadGolden && Hash(nextUpload)==UploadGolden && p.ModelSha256==ModelGolden && Hash(nextModels)==ModelGolden,
                "Independent pre-production raw full output goldens.");
            Check(nextData.SequenceEqual(data) && nextUpload.AsSpan(0x80000).SequenceEqual(upload.AsSpan(0x80000)) &&
                nextModels.AsSpan(0,0x17800).SequenceEqual(models),"Complete data/audio/old model17800 prefix preserved.");
            Check(p.SourceCertified&&!p.GlobalAudioCompatibilityProven&&!p.CompleteNativeDependencyClosureProven&&!p.RuntimeVerified&&
                !p.PublicationApproved&&!p.BodyIssued&&!p.PersistentOwnerIssued&&p.PendingOwners.Count>=4,"Source byte issuance is not body/save/audio/runtime authority.");
            Check(p.ResourcePlanSha256==r.PlanSha256 && p.CanonicalDocumentSha256==c.CanonicalSha256 && p.PlanSha256.Length==64,
                "Actual immutable ancestry identity.");
            Check(p.ModelRoots.Count==7 && p.ModelRoots.Take(4).SequenceEqual(r.ModelRoots) &&
                p.ModelRoots.Skip(4).SequenceEqual(new[]{new NativeFreshWizardModelRoot(0xE,0x17800,0x7EC,0x7EC),
                    new NativeFreshWizardModelRoot(0xF,0x17FEC,0x78C,0x78C),new NativeFreshWizardModelRoot(0x10,0x18778,0x330,0x888)}),
                "Seven source-native roots preserve entire original Wizard registration extent.");
            Check(nextModels.AsSpan(0x17800,0x7EC).SequenceEqual(town.AsSpan(0x1C8D04,0x7EC)) &&
                nextModels.AsSpan(0x17FEC,0x78C).SequenceEqual(town.AsSpan(0x1C94F0,0x78C)) &&
                nextModels.AsSpan(0x18778,0x330).SequenceEqual(town.AsSpan(0x1BED08,0x330)),"Three complete actual source packages, no metadata-only ownership.");
            NativeTownFallbackSimpleModel.RequireShape(nextModels.AsSpan(0x17800,0x7EC));
            Check(nextModels.AsSpan(0x17FEC,8).SequenceEqual(Convert.FromHexString("01000000ffffffff")),"Exact next0F owner supplies native simple renderer lookahead8.");
            Check(nextModels.AsSpan(0x17134,0x6CC).ContainsAnyExcept((byte)0)==false &&
                nextModels.AsSpan(0x18AA8,0x558).ContainsAnyExcept((byte)0)==false,"Prior Wizard padding and separate final model padding retain all zeros.");
            Check(NativeMobyModelTextureLayout.Decode(nextModels.AsSpan(0x17FEC,0x78C)).References.Count==72 &&
                NativeMobyModelTextureLayout.Decode(nextModels.AsSpan(0x18778,0x330)).References.Count==8,"Complete distinct animated grammar remains unchanged.");
            Reject<InvalidDataException>(()=>NativeMobyModelTextureLayout.Decode(nextModels.AsSpan(0x17800,0x7EC)));
            Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchResourceSource.RequireInstalledGrammar(nextData,nextModels));
            var expected=new List<(int Offset,int Length)>();
            foreach(var region in new (int At,int Width,int Rows)[]{(0x582B0,16,32),(0x7C2C0,32,1),(0x7E2C0,32,1),(0x48320,16,32),(0x722E0,32,1)})
                for(int row=0;row<region.Rows;row++)expected.Add((region.At+row*1024,region.Width));
            expected=expected.OrderBy(x=>x.Offset).ToList();
            Check(p.TextureSourceSpans.Count==67 && p.TextureSourceSpans.Select(s=>(s.Offset,s.ByteLength)).SequenceEqual(expected) &&
                p.TextureSourceSpans.Sum(s=>s.ByteLength)==1120,"Exactly67 source row/CLUT spans totaling1120 bytes.");
            Check(p.RetainedTextureAliases.Count==4 && p.RetainedTextureAliases.Select(s=>(s.Offset,s.ByteLength)).SequenceEqual(
                Enumerable.Range(0,4).Select(i=>(0x6C3A0+i*1024,2))),"Exactly four existing same-source two-byte aliases.");
            var changed=new bool[0x80000];
            foreach(var span in p.TextureSourceSpans.Concat(p.RetainedTextureAliases))
            {
                Check(span.SourceWadOffset==0x119E800L+span.Offset && span.ByteLength>0 && span.Offset>=0 && span.Offset<=0x80000-span.ByteLength &&
                    Hash(town.AsSpan(0x800+span.Offset,span.ByteLength))==span.Sha256 &&
                    nextUpload.AsSpan(span.Offset,span.ByteLength).SequenceEqual(town.AsSpan(0x800+span.Offset,span.ByteLength)),"Complete original strided source identity.");
            }
            foreach(var span in p.TextureSourceSpans)
            {Check(!changed.AsSpan(span.Offset,span.ByteLength).Contains(true),"Disjoint new source owner.");Array.Fill(changed,true,span.Offset,span.ByteLength);}
            foreach(var alias in p.RetainedTextureAliases)
                Check(!changed.AsSpan(alias.Offset,alias.ByteLength).Contains(true)&&nextUpload.AsSpan(alias.Offset,alias.ByteLength).SequenceEqual(upload.AsSpan(alias.Offset,alias.ByteLength)),"Existing protected alias unchanged.");
            for(int i=0;i<changed.Length;i++)if(!changed[i])Check(nextUpload[i]==upload[i],"Exhaustive old/protected-zero VRAM complement.");
            byte[][] outputs=[nextUpload,nextData,nextModels];
            for(int domain=0;domain<3;domain++)
            {
                int cursor=0;foreach(var owner in p.Owners.Where(o=>(int)o.Domain==domain))
                {Check(owner.Offset==cursor && owner.ByteLength>0 && owner.ByteLength<=outputs[domain].Length-cursor &&
                    Hash(outputs[domain].AsSpan(cursor,owner.ByteLength))==owner.Sha256,"Exhaustive bound immutable output owner.");cursor+=owner.ByteLength;}
                Check(cursor==outputs[domain].Length,"No unowned final domain suffix.");
            }
            p.RequireExactOutputs(nextUpload,nextData,nextModels);
            foreach(var owner in p.Owners)
            {byte[][] bad=outputs.Select(b=>b.ToArray()).ToArray();bad[(int)owner.Domain][owner.Offset]^=1;
                Reject<InvalidDataException>(()=>p.RequireExactOutputs(bad[0],bad[1],bad[2]));}
            foreach(int d in Enumerable.Range(0,3))
            {byte[][] bad=outputs.Select(b=>b.ToArray()).ToArray();bad[d]=bad[d][..^1];Reject<InvalidDataException>(()=>p.RequireExactOutputs(bad[0],bad[1],bad[2]));}
            foreach(int at in new[]{0x17134,0x177FF,0x17800,0x17FEC,0x17FF0,0x18AA8,0x18FFF})
            {byte[] bad=nextModels.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>p.RequireExactOutputs(nextUpload,nextData,bad));}
            nextUpload[0]^=1;nextModels[0]^=1;Check(Hash(p.CopyUploadPayload())==UploadGolden&&Hash(p.CopyModelPayload())==ModelGolden,"Copy-on-read no aliases.");
            var repeat=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(original,r);
            Check(repeat.PlanSha256==p.PlanSha256,"Repeat same actual source/resource is deterministic.");
            var r2=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,c,f);
            Check(r2.PlanSha256==r.PlanSha256,"Foreign actual resource genuinely byte-identical.");
            Reject<InvalidDataException>(()=>p.RequireAncestry(r2));
            var own=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(original,r2);own.RequireAncestry(r2);
            Check(own.UploadSha256==p.UploadSha256&&own.ModelSha256==p.ModelSha256,"Second actual source issues its own revision, not rebinding first.");
            using(var cancel=new CancellationTokenSource())
            {cancel.Cancel();await Reject<OperationCanceledException>(()=>NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync("not-opened.bin",r,cancel.Token));}
            await Reject<ArgumentException>(()=>NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync("",r));
            await Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(typeof(FallbackCases).Assembly.Location,r));
            Check(Hash(upload)=="d74c2571ed857865b2b989af3001117f44e0915f9c6d59927e30a49d90a79402" &&
                Hash(data)=="bea93c58d6d568c7c6162816ddad1afff32a51886f9412a0809b22887e0b83d4" &&
                Hash(models)=="782c4500b3152f6051b45b8a0a56f6c99c4f703c753948f656318ccfa8618cda","All old resource goldens unchanged.");
            r.RequireExactOutputs(upload,data,models);
            Check(JsonSerializer.Serialize(r)==resourceJson&&JsonSerializer.Serialize(c)==compilationJson,"All upstream JSON survives successes/refusals unchanged.");
            Console.WriteLine($"uploadSha256={p.UploadSha256} modelSha256={p.ModelSha256} planSha256={p.PlanSha256}");
        }
        finally{RequireOriginal(original);}
        Console.WriteLine($"FRESH_WIZARD_FALLBACK_RESOURCE_SOURCE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }
    private static void RequireOriginal(string path)
    {using var f=File.OpenRead(path);Check(f.Length==661547040&&Convert.ToHexStringLower(SHA256.HashData(f))==
        "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"&&f.Position==661547040&&f.ReadByte()==-1&&f.Position==661547040&&f.Length==661547040,"Whole original before/finally identity+actualEOF.");}
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Put(Span<byte> b,int at,uint n)=>BinaryPrimitives.WriteUInt32LittleEndian(b[at..],n);
    private static void Check(bool ok,string reason){if(!ok)throw new InvalidDataException(reason);checks++;}
    private static void Reject<T>(Action a)where T:Exception
    {try{a();}catch(T){checks++;refusals++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    private static async Task Reject<T>(Func<Task> a)where T:Exception
    {try{await a();}catch(T){checks++;refusals++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
