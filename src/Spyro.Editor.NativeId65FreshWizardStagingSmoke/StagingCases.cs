using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class StagingCases
{
    private static int checks,refusals;
    private const string TransferGolden="33282cc17414acccd98381f8d5b9e068ea2327bb3c8a7138dd82b1705c217fad";
    internal static async Task SourceFree()
    {
        var t=typeof(NativeId65FreshWizardResearchUploadStaging);
        Check(t.IsSealed&&t.GetConstructors().Length==0&&t.GetProperties().All(p=>p.SetMethod==null),"Closed immutable issuer.");
        var api=t.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
        Check(api.Length==1&&api[0].Name=="CaptureAsync"&&api[0].GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
            typeof(string),typeof(NativeId65FreshWizardResearchResourcePlan),typeof(CancellationToken)}),"Only actual resource issuance; no bytes/hash/boolean certificate.");
        Check(!t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Any(m=>
            new[]{"Publish","Write","CompileBody"}.Any(s=>m.Name.StartsWith(s,StringComparison.Ordinal))),"No writer/body/publication API.");
        using(var cancel=new CancellationTokenSource())
        {cancel.Cancel();await Reject<OperationCanceledException>(()=>NativeId65FreshWizardResearchUploadStaging.CaptureAsync(null!,null!,cancel.Token));}
        await Reject<ArgumentNullException>(()=>NativeId65FreshWizardResearchUploadStaging.CaptureAsync("not-opened.bin",null!));
        byte[] upload=new byte[0xE5800];for(int i=0;i<upload.Length;i++)upload[i]=(byte)(i*31+i/257);
        byte[] original=upload.ToArray();var l=NativeId65FreshWizardUploadTransferLayout.Compile(upload);
        RequireLayout(l,upload);Check(!l.SourceCertified,"Neutral layout never issues source authority.");
        foreach(int length in new[]{0,0xDE000,0xE57FF,0xE5801,0x100000})
            Reject<InvalidDataException>(()=>NativeId65FreshWizardUploadTransferLayout.Compile(new byte[length]));
        Reject<InvalidDataException>(()=>NativeId65NormalUploadTransferLayout.Compile(upload));
        byte[] bytes=l.CopyTransferSourceImage();
        foreach(int at in Enumerable.Range(0,0x7F000/0x800).Select(i=>i*0x800).Concat(new[]{0x5DFFF,0x5E000,0x6516F,0x65170,0x657FF,0x65800,0x7EFEF,0x7EFF0,0x7EFFF}).Distinct())
        {bytes[at]^=1;Reject<InvalidDataException>(()=>l.RequireExactTransferSource(bytes));bytes[at]^=1;}
        Reject<InvalidDataException>(()=>l.RequireExactTransferSource(bytes[..^1]));
        Reject<InvalidDataException>(()=>l.RequireExactTransferSource(new byte[0x7F001]));
        Check(bytes.SequenceEqual(l.CopyTransferSourceImage()),"Every tested mutation refusal preserves output.");
        bytes[0]^=1;Check(!bytes.SequenceEqual(l.CopyTransferSourceImage()),"Copy-on-read output.");
        upload[0x80000]^=1;Check(l.CopyTransferSourceImage()[0]==original[0x80000],"No mutable input alias.");
        byte[] outside=original.ToArray();outside[0]^=1;
        var other=NativeId65FreshWizardUploadTransferLayout.Compile(outside);
        Check(other.TransferSourceSha256==l.TransferSourceSha256&&other.PairedUploadSha256!=l.PairedUploadSha256,
            "Equal transfer hashes do not identify the complete paired resource; private reference ancestry remains required.");
        Check(original.AsSpan(0x80000).ToArray().Concat(new byte[0x19800]).ToArray().AsSpan().SequenceEqual(l.CopyTransferSourceImage())==false,
            "Padding the bank loses nonzero same-resource residue.");
        Console.WriteLine($"FRESH_WIZARD_STAGING_FREE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }

    internal static async Task Source(string original)
    {
        RequireOriginal(original);
        try
        {
            var intent=NativeId65FreshDocumentIntent.Create(8448,8448,8704,1024,1024,8960,8960,64,new(80,128,112),new(128,96,64),19);
            var c=await NativeId65FreshDocumentCompilation.CaptureAsync(original,intent);
            var f=await NativeWizardFamilyAssetPlan.CaptureAsync(original);
            var r=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,c,f);
            byte[] upload=r.CopyUploadPayload(),data=r.CopyDataComponents(),models=r.CopyModelPayload();
            byte[] oldScene=c.FreshResourceAncestor.CopyScene(),oldBody=c.DiscPlan.CopyBody();
            string oldJson=JsonSerializer.Serialize(c),resourceJson=JsonSerializer.Serialize(r);
            var s=await NativeId65FreshWizardResearchUploadStaging.CaptureAsync(original,r);
            s.RequireAncestry(r);Check(ReferenceEquals(s.ResourceAncestor,r),"Exact actual resource reference retained.");
            RequireLayout(s.Layout,upload);
            Check(s.TransferSourceSha256==TransferGolden&&Hash(s.CopyTransferSourceImage())==TransferGolden,
                "Independent whole-transfer raw oracle golden established before Core.");
            Check(s.PairedUploadSha256=="d74c2571ed857865b2b989af3001117f44e0915f9c6d59927e30a49d90a79402"&&
                s.Owners[0].Sha256=="f922913cf797ee91f0e0ccb909e511a2746c9f28a5df9d8ca2b54934979b968c"&&
                s.Owners[1].Sha256=="e2e713a331281d553f43cd2df764208ba00770dc1d2fea767963f698cd238ac1","Complete upload/bank/residue independent goldens.");
            Check(s.BankRequestedByteLength==0x65800&&s.BankCdSectorCount==203&&s.CpuRequestedByteLength==0x7EFF0&&
                s.DmaProgrammedSourceByteLength==0x7F000&&s.LinearSpuDestination==0x1010&&s.DmaLinearSpuEnd==0x80010,
                "Source native request witness203 sectors and distinct fixed CPU/DMA lengths.");
            Check(s.SourceCertified&&!s.CompleteSpuStateProven&&!s.SpuWrapSemanticsProven&&!s.ReservedLow16PreservationProven&&
                !s.TransferTimingProven&&!s.PeakRuntimeMemoryProven&&!s.HardwareTransfersProven&&!s.GlobalAudioCompatibilityProven&&
                !s.CompleteNativeDependencyClosureProven&&!s.BodyIssued&&!s.RuntimeVerified&&!s.PublicationApproved,
                "Detached source bytes do not certify hardware/wrap/global selector/body/runtime/publication.");
            Check(s.PendingOwners.Count>=4&&s.PlanSha256.Length==64&&s.ResourcePlanSha256==r.PlanSha256,"Explicit actual ancestry/pending owners.");
            Check(Convert.ToHexStringLower(s.CopyTransferSourceImage().AsSpan(0x7EFF0))=="3232b131122e903191311232f231322e","All nonzero DMA-only16 retained, no low-SPU ownership claim.");
            s.RequireExactTransferSource(s.CopyTransferSourceImage());
            foreach(int at in new[]{0,0x5DFFF,0x5E000,0x65170,0x657FF,0x65800,0x7EFF0,0x7EFFF})
            {byte[] bad=s.CopyTransferSourceImage();bad[at]^=1;Reject<InvalidDataException>(()=>s.RequireExactTransferSource(bad));}
            var repeat=await NativeId65FreshWizardResearchUploadStaging.CaptureAsync(original,r);
            Check(repeat.PlanSha256==s.PlanSha256&&repeat.TransferSourceSha256==s.TransferSourceSha256,"Same actual resource deterministic.");
            var r2=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,c,f);
            Check(r2.UploadSha256==r.UploadSha256&&r2.PlanSha256==r.PlanSha256,"Other real resource is genuinely byte-identical.");
            Reject<InvalidDataException>(()=>s.RequireAncestry(r2));
            var s2=await NativeId65FreshWizardResearchUploadStaging.CaptureAsync(original,r2);s2.RequireAncestry(r2);
            Check(s2.TransferSourceSha256==s.TransferSourceSha256,"Independent actual resource makes its own stage, not a rebind.");
            using(var cancel=new CancellationTokenSource())
            {cancel.Cancel();await Reject<OperationCanceledException>(()=>NativeId65FreshWizardResearchUploadStaging.CaptureAsync("not-opened.bin",r,cancel.Token));}
            await Reject<ArgumentException>(()=>NativeId65FreshWizardResearchUploadStaging.CaptureAsync("",r));
            // Small hostile source identity is refused by the actual public factory; no original mutations.
            await Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchUploadStaging.CaptureAsync(typeof(StagingCases).Assembly.Location,r));
            var old=await NativeId65NormalUploadStaging.CaptureAsync(original,c.FreshResourceAncestor);
            Check(old.TransferSourceSha256=="626ae349b98cb4bea955ac53020586f645f872225085a67bc809f46f4ab0fb07","Old staging golden unchanged.");
            Check(Hash(oldScene)=="4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433"&&
                Hash(oldBody)=="b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c"&&
                Hash(data)=="bea93c58d6d568c7c6162816ddad1afff32a51886f9412a0809b22887e0b83d4"&&
                Hash(models)=="782c4500b3152f6051b45b8a0a56f6c99c4f703c753948f656318ccfa8618cda","P0/body/data/models goldens preserved.");
            r.RequireExactOutputs(upload,data,models);
            Check(c.FreshResourceAncestor.CopyScene().SequenceEqual(oldScene)&&c.DiscPlan.CopyBody().SequenceEqual(oldBody)&&
                JsonSerializer.Serialize(c)==oldJson&&JsonSerializer.Serialize(r)==resourceJson,"Success/refusal never modifies upstream values.");
            Console.WriteLine($"transferSha256={s.TransferSourceSha256} planSha256={s.PlanSha256}");
        }
        finally{RequireOriginal(original);}
        Console.WriteLine($"FRESH_WIZARD_STAGING_SOURCE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }
    private static void RequireLayout(NativeId65FreshWizardUploadTransferLayout l,byte[] upload)
    {
        byte[] b=l.CopyTransferSourceImage();Check(b.Length==0x7F000&&Hash(b)==l.TransferSourceSha256&&Hash(upload)==l.PairedUploadSha256,"Complete immutable pair and transfer.");
        Check(b.AsSpan(0,0x65800).SequenceEqual(upload.AsSpan(0x80000))&&b.AsSpan(0x65800).SequenceEqual(upload.AsSpan(0x65800,0x19800)),"Entire bank followed by SAME-resource VRAM residue, not zero padding.");
        Check(l.Owners.Count==2&&l.Owners[0].Offset==0&&l.Owners[0].ByteLength==0x65800&&l.Owners[0].PairedUploadOffset==0x80000&&
            l.Owners[1].Offset==0x65800&&l.Owners[1].ByteLength==0x19800&&l.Owners[1].PairedUploadOffset==0x65800,"Exact two phase owners.");
        int cursor=0;foreach(var o in l.Owners)
        {Check(o.Offset==cursor&&o.ByteLength>0&&o.ByteLength<=b.Length-cursor&&Hash(b.AsSpan(cursor,o.ByteLength))==o.Sha256&&
            b.AsSpan(cursor,o.ByteLength).SequenceEqual(upload.AsSpan(o.PairedUploadOffset,o.ByteLength)),"Bounded contiguous complete owner/hash/preimage.");cursor+=o.ByteLength;}
        Check(cursor==b.Length&&l.SourceReads.SequenceEqual(new[]{new NativeId65TransferSourceRead(NativeId65UploadBackend.CpuFifo,0,0x7EFF0,0x7EFF0,2),
            new NativeId65TransferSourceRead(NativeId65UploadBackend.Dma,0,0x7F000,0x7EFF0,64)}),"No unowned complement; distinct CPU/DMA reads.");
    }
    private static void RequireOriginal(string path)
    {using var s=File.OpenRead(path);Check(s.Length==661547040&&Convert.ToHexStringLower(SHA256.HashData(s))=="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"&&s.Position==661547040&&s.ReadByte()==-1&&s.Position==661547040&&s.Length==661547040,"Original complete before/finally SHA/actualEOF/position/length.");}
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);checks++;}
    private static void Reject<T>(Action a)where T:Exception{try{a();}catch(T){checks++;refusals++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    private static async Task Reject<T>(Func<Task> a)where T:Exception{try{await a();}catch(T){checks++;refusals++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
