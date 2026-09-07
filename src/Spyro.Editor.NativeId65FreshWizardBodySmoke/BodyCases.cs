using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class BodyCases
{
    private static int checks,refusals;
    private const string BodyGolden="33e54ff9e15608ada3f6d430c54fbabc21a8263e059c050b391db22cc541bc00";
    private const string HeaderGolden="fe3757b0f68a8b7003d4a9e4660bc9e2b643fbadc6f1e71123bca29e26ec2ca8";
    private const string TransferGolden="92820254671062b6b20e2cfd2c243faac32ae6223e22d50d06221b4be8539883";
    private static readonly int[] Lengths=[0xE5800,0x1000,0x19000,0x3000,0x49800,0x42800,0x5D000,0x20800];
    private static readonly (int Class,int At)[] Roots=[(0,0),(0x78,0x10A4C),(0x26,0x1361C),(0x11B,0x13B80),(0xE,0x17800),(0xF,0x17FEC),(0x10,0x18778)];
    private static readonly string[] PartHashes=[
        "8c4438fc13eca3b3bd40e9c056094c308fb272aded227577fb812ec9b16d96dc",
        "1536654261475fcab6b0c0e3e4c4997f899a045ce92eb3efc36a5fc94624cffe",
        "84d00dbbce264bc08c61abfe2579fe3d7a7464ba2df6b189834664da10b82c84",
        "b7bd222702ff9b5d6ea40c623159f4aa22b3425ef9368059167d1ccab930266b",
        "bdf27988f3d2b0fd4c4b905636b6980aff49ab1475d85edb4ce8aa2cc469351f",
        "23968146e46b3263997e16692a192bbdb049966b010673d17611f2ab6903cef0",
        "402bb40e21bf84922cbc35312b0e49b95292a2f96326636aba05685443686991",
        "eabadb4ea663043665bc4ba2d2e00c1c7c104688906b56bac2d7aba7c6cae8da"];
    private static byte[] Header()
    {
        byte[] b=new byte[0x800];int cursor=0x800;
        for(int i=0;i<Lengths.Length;i++){Put(b,i*8,cursor);Put(b,i*8+4,Lengths[i]);cursor+=Lengths[i];}
        for(int i=0;i<Roots.Length;i++)
        {Put(b,0x50+i*4,0xE7000+Roots[i].At);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(0x150+i*2),(ushort)Roots[i].Class);}
        return b;
    }
    internal static async Task SourceFree()
    {
        var t=typeof(NativeId65FreshWizardResearchBodyPlan);
        Check(t.IsSealed&&t.GetConstructors().Length==0&&t.GetProperties().All(p=>p.SetMethod is null),"Closed immutable body issuer.");
        var api=t.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
        Check(api.Length==1&&api[0].Name=="CaptureAsync"&&api[0].GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
            typeof(string),typeof(NativeId65FreshWizardResearchHostPlan),typeof(CancellationToken)}),"Only original plus actual Host; no staging/bytes/hash authority.");
        Check(!t.GetMethods().Any(m=>new[]{"Publish","Write","CopyExecutable"}.Any(n=>m.Name.StartsWith(n,StringComparison.Ordinal))),"No final executable or output API.");
        using(var c=new CancellationTokenSource())
        {c.Cancel();await Reject<OperationCanceledException>(()=>NativeId65FreshWizardResearchBodyPlan.CaptureAsync(null!,null!,c.Token));}
        await Reject<ArgumentNullException>(()=>NativeId65FreshWizardResearchBodyPlan.CaptureAsync("not-opened.bin",null!));
        byte[] header=Header();Check(Hash(header)==HeaderGolden,"Independent preproduction native header golden.");
        Check(NativeId65FreshWizardBodyLayout.DecodeHeader(header).Count==8,"Eight bounded descriptors; absent8/9 are not owned payloads.");
        // Every descriptor/root/class/reserved byte is checked by a structural
        // decoder, not only comparison with the eventual full body hash.
        for(int i=0;i<header.Length;i++)
        {byte[] bad=header.ToArray();bad[i]^=1;Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.DecodeHeader(bad));}
        foreach(int n in new[]{0,0x1D0,0x7FF,0x801,0x1000})
            Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.DecodeHeader(new byte[n]));
        foreach(int n in new[]{-1,0,4,0x7FFFFFFC})
        {byte[] bad=header.ToArray();Put(bad,8,n);Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.DecodeHeader(bad));}
        byte[] aux=new byte[0x100];int at=0x24;
        for(int i=0;i<4;i++){int n=i==3?0x30:0x20;Put(aux,4+i*8,at);Put(aux,8+i*8,n);at+=n;}
        Check(NativeId65FreshWizardBodyLayout.DecodeAuxiliaryOwners(aux).Count==6,"Neutral complete header/audio/two models/track/padding partition.");
        foreach(int field in Enumerable.Range(0,9).Select(i=>i*4))
        {byte[] bad=aux.ToArray();Put(bad,field,field==0?1:int.MaxValue);Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.DecodeAuxiliaryOwners(bad));}
        byte[] wrongTrack=aux.ToArray();Put(wrongTrack,32,0x2C);Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.DecodeAuxiliaryOwners(wrongTrack));
        Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.DecodeAuxiliaryOwners(new byte[0x23]));
        Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.DecodeAuxiliaryOwners(new byte[0x5D001]));
        byte[] largePad=new byte[0x1000];aux.CopyTo(largePad,0);Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.DecodeAuxiliaryOwners(largePad));
        Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.Compile([],[],[],[],[]));
        Check(Hash(header)==HeaderGolden,"All rejected neutral mutations preserve header fixture.");
        Console.WriteLine($"FRESH_WIZARD_BODY_FREE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }
    internal static async Task Source(string original)
    {
        Original(original);
        try
        {
            var intent=NativeId65FreshDocumentIntent.Create(8448,8448,8704,1024,1024,8960,8960,64,new(80,128,112),new(128,96,64),19);
            var c=await NativeId65FreshDocumentCompilation.CaptureAsync(original,intent);
            var f=await NativeWizardFamilyAssetPlan.CaptureAsync(original);
            var r=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,c,f);
            var scene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,f,r,new(8704,8960,8704,9216,8960));
            var fallback=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(original,r);
            var host=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(original,scene,fallback);
            string ancestors=JsonSerializer.Serialize(new{c,f,r,scene,fallback,host});
            var p=await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(original,host);p.RequireAncestry(host);
            Check(ReferenceEquals(p.HostAncestor,host),"Retained exact issued Host reference.");
            byte[] body=p.CopyLevelData(),overlay=p.CopyOverlay(),initializer=p.CopyInitializerPatch(),transfer=p.CopyTransferSourceImage();
            Check(body.Length==0x20C800&&Hash(body)==BodyGolden&&p.LevelDataSha256==BodyGolden&&p.HeaderSha256==HeaderGolden&&body.AsSpan(0,0x800).SequenceEqual(Header()),"Independent complete body/header golden, not old2MiB envelope.");
            Check(p.CopyBufferAddress==0x8008BA38&&p.ModelRuntimeAddress==0x8008C334&&p.SceneRuntimeAddress==0x800A5334&&p.KnownSceneEndAddress==0x800A8334&&
                p.ResidentPeteAddress==0x801C5BB8&&p.NormalReservedLowerAddress==0x80187BB0,"Actual native-derived known addresses, not peak fit.");
            Check(p.HostPlanSha256==host.PlanSha256&&p.CanonicalDocumentSha256==c.CanonicalSha256&&p.SourceCertified&&
                !p.FinalExecutableIssued&&!p.InstalledDispatchProven&&!p.RuntimeVerified&&!p.PublicationApproved&&!p.PersistentOwnerIssued&&
                !p.CompleteNativeDependencyClosureProven&&!p.CompleteLevelResourcesProven&&!p.GlobalAudioCompatibilityProven&&
                !p.PeakRuntimeMemoryProven&&!p.HardwareTransfersProven&&!p.CompleteIncomingEdgeClosureProven,"Detached bytes retain false native/publication authority.");
            Check(p.Descriptors.Count==8&&p.ModelRoots.Count==7,"Complete exact descriptor/root inventory.");
            Check(fallback.ModelRoots[1].ByteLength==0x2BD0&&p.ModelRoots[1].PayloadByteLength==0x2BD0&&
                p.ModelRoots[1].RegistrationAllocationByteLength==0x2BD0,"Frozen actual-issued R1 class78 owner is2BD0, not old NormalBody source-package2930 semantics.");
            Check(p.Evidence.Where(e=>e.Domain=="actual-issued-Host").Select(e=>e.Sha256).SequenceEqual(new[]{host.OverlaySha256,host.InitializerSha256})&&
                p.Evidence.Single(e=>e.Domain=="actual-issued-scene").Sha256==scene.SceneSha256&&
                p.Evidence.Where(e=>e.Domain=="actual-issued-fallback").Select(e=>e.Sha256).SequenceEqual(new[]{fallback.UploadSha256,fallback.DataSha256,fallback.ModelSha256})&&
                p.Evidence.Single(e=>e.Domain=="derived-same-fallback-transfer").Sha256==TransferGolden,
                "Actual emitted inputs have distinct domains from original retail EXE/WAD consumer evidence.");
            int cursor=0x800;
            for(int i=0;i<8;i++)
            {var d=p.Descriptors[i];Check(d.Index==i&&d.Offset==cursor&&d.ByteLength==Lengths[i]&&d.RetainedAuxiliary==(i>=4)&&Hash(body.AsSpan(cursor,d.ByteLength))==PartHashes[i],"Full independently derived descriptor owner "+i);cursor+=d.ByteLength;}
            Check(cursor==body.Length,"No body suffix outside descriptor owners.");
            for(int i=0;i<7;i++)
            {var root=p.ModelRoots[i];var source=fallback.ModelRoots[i];Check(root.SlotIndex==i&&root.EntryRelativeOffset==0xE7000+Roots[i].At&&root.ModelRelativeOffset==Roots[i].At&&
                root.PayloadByteLength==source.ByteLength&&root.RegistrationAllocationByteLength==source.RegistrationExtentByteLength&&root.ClassId==Roots[i].Class,"All actual same-fallback roots, including local0 stream.");}
            Check(body.AsSpan(0x800,0xE5800).SequenceEqual(fallback.CopyUploadPayload())&&body.AsSpan(0xE7000,0x19000).SequenceEqual(fallback.CopyModelPayload())&&
                body.AsSpan(0x100000,0x3000).SequenceEqual(scene.CopyScene()),"Complete actual supplied descendants, not reconstructed from equal hashes.");
            Check(overlay.SequenceEqual(host.CopyOverlay())&&initializer.SequenceEqual(host.CopyInitializerPatch()),"Full actual Host copies only, no EXE.");
            Check(transfer.Length==0x7F000&&Hash(transfer)==TransferGolden&&p.TransferSourceSha256==TransferGolden&&
                transfer.AsSpan(0,0x65800).SequenceEqual(fallback.CopyUploadPayload().AsSpan(0x80000))&&
                transfer.AsSpan(0x65800).SequenceEqual(fallback.CopyUploadPayload().AsSpan(0x65800,0x19800)),"Exact same-fallback bank/residue, including DMA-only tail.");
            var stale=await NativeId65FreshWizardResearchUploadStaging.CaptureAsync(original,r);
            Check(stale.TransferSourceSha256=="33282cc17414acccd98381f8d5b9e068ea2327bb3c8a7138dd82b1705c217fad"&&stale.TransferSourceSha256!=p.TransferSourceSha256,"Old R0 stage unchanged and genuinely different.");
            Reject<InvalidDataException>(()=>p.RequireExactOutputs(body,overlay,initializer,stale.CopyTransferSourceImage()));
            Check(p.TransferOwners.Count==2&&p.TransferSourceReads.Count==2&&p.TransferSourceReads[0].RequestedByteLength==0x7EFF0&&
                p.TransferSourceReads[1].ByteLength==0x7F000,"Distinct CPU/DMA transfer source owners.");
            Check(p.ReadPhases.Select(x=>x.Id).SequenceEqual(new[]{"overlay-install","header-read","vram-read","bank-read","cpu-spu-source","dma-spu-source","data-read","model-read","scene-read"})&&
                p.ReadPhases.All(x=>x.RequiresExternalCompletion),"Ordered explicit external-completion phases, not hardware return emulation.");
            Check(p.ReadPhases[3].ByteLength==0x65800&&p.ReadPhases[7].RuntimeAddress==p.ModelRuntimeAddress&&p.PhaseOverlaps.Count==1&&
                p.PhaseOverlaps[0].ByteLength==0x704,"Only declared data padding/model overlap; whole enlarged host ends at copy anchor.");
            cursor=0;
            foreach(var owner in p.Owners)
            {Check(owner.Offset==cursor&&owner.ByteLength>0&&Hash(body.AsSpan(cursor,owner.ByteLength))==owner.Sha256,"Every contiguous body owner SHA.");cursor+=owner.ByteLength;}
            Check(cursor==body.Length,"Complete ownership partition includes padding.");p.RequireOwners(p.Owners);
            var wrongOwners=p.Owners.ToArray();wrongOwners[0]=wrongOwners[0] with{Provenance="caller-certificate"};Reject<InvalidDataException>(()=>p.RequireOwners(wrongOwners));
            wrongOwners=p.Owners.ToArray();wrongOwners[1]=wrongOwners[1] with{Offset=wrongOwners[1].Offset+4};Reject<InvalidDataException>(()=>p.RequireOwners(wrongOwners));
            Reject<InvalidDataException>(()=>p.RequireOwners(p.Owners.Reverse().ToArray()));Reject<InvalidDataException>(()=>p.RequireOwners(p.Owners.Skip(1).ToArray()));
            foreach(int selector in new[]{-1,4,5,255,int.MaxValue})Reject<InvalidDataException>(()=>p.RequireAuxiliarySelector(selector));
            for(int i=0;i<4;i++)p.RequireAuxiliarySelector(i);
            foreach(var d in p.Descriptors.Where(d=>d.RetainedAuxiliary))
            {int next=d.Offset;var sub=p.AuxiliaryOwners.Where(o=>o.DescriptorIndex==d.Index).ToArray();Check(sub.Length==6,"Complete auxiliary header/audio/models/track/padding.");
                foreach(var o in sub){Check(o.BodyRelativeOffset==next&&Hash(body.AsSpan(next,o.ByteLength))==o.Sha256,"Exact auxiliary subowner");next+=o.ByteLength;}
                Check(next==d.Offset+d.ByteLength,"Complete auxiliary source allocation and padding.");}
            NativeId65FreshWizardBodyLayout.ValidateStructure(body);
            var aux=p.Descriptors.Skip(4).Select(d=>body.AsSpan(d.Offset,d.ByteLength).ToArray()).ToArray();
            Reject<InvalidDataException>(()=>NativeId65NormalBodyLayout.Compile(fallback.CopyUploadPayload(),fallback.CopyDataComponents(),fallback.CopyModelPayload(),scene.CopyScene(),aux));
            var neutral=NativeId65FreshWizardBodyLayout.Compile(fallback.CopyUploadPayload(),fallback.CopyDataComponents(),fallback.CopyModelPayload(),scene.CopyScene(),aux);
            Check(!neutral.SourceCertified&&neutral.CopyLevelData().SequenceEqual(body),"Neutral packer cannot issue public source capability.");
            foreach(int at in Enumerable.Range(0,0x800).Concat(Enumerable.Range(0xE6000+0x8FC,0x704)).Concat(
                p.Descriptors.SelectMany(d=>new[]{d.Offset,d.Offset+d.ByteLength-1})).Concat(new[]{0xE7000+0x18AA8,0x100000+0xFE4,0x100000+0x2FE8}))
            {byte[] bad=body.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>p.RequireExactOutputs(bad,overlay,initializer,transfer));
                if(at<0x800||at>=0xE6000+0x8FC&&at<0xE7000)Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.ValidateStructure(bad));}
            foreach(int length in new[]{0,0x200000,0x20C7FF,0x20C801})Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.ValidateStructure(new byte[length]));
            foreach(int field in new[]{0,4,8,12,16,20,24,28,32})
            {byte[] bad=body.ToArray();Put(bad,p.Descriptors[4].Offset+field,int.MaxValue);Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.ValidateStructure(bad));}
            byte[][] tooMany=[..aux,aux[0]];Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.Compile(fallback.CopyUploadPayload(),fallback.CopyDataComponents(),fallback.CopyModelPayload(),scene.CopyScene(),tooMany));
            byte[][] shortAux=aux.Select(a=>a.ToArray()).ToArray();shortAux[0]=shortAux[0][..^1];Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.Compile(fallback.CopyUploadPayload(),fallback.CopyDataComponents(),fallback.CopyModelPayload(),scene.CopyScene(),shortAux));
            for(int domain=0;domain<4;domain++)
            {byte[][] parts=[fallback.CopyUploadPayload(),fallback.CopyDataComponents(),fallback.CopyModelPayload(),scene.CopyScene()];parts[domain]=parts[domain][..^4];
                Reject<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.Compile(parts[0],parts[1],parts[2],parts[3],aux));}
            foreach(int at in new[]{0,0x657FF,0x65800,0x7EFF0,0x7EFFF})
            {byte[] bad=transfer.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>p.RequireExactOutputs(body,overlay,initializer,bad));}
            foreach(int at in new[]{0,0xF800,0x10FFF})
            {byte[] bad=overlay.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>p.RequireExactOutputs(body,bad,initializer,transfer));}
            foreach(int at in new[]{0,0x94,0x97})
            {byte[] bad=initializer.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>p.RequireExactOutputs(body,overlay,bad,transfer));}
            string json=JsonSerializer.Serialize(p);body[0]^=1;overlay[0]^=1;initializer[0]^=1;transfer[0]^=1;
            Check(Hash(p.CopyLevelData())==BodyGolden&&Hash(p.CopyTransferSourceImage())==TransferGolden&&p.CopyOverlay().SequenceEqual(host.CopyOverlay())&&
                p.CopyInitializerPatch().SequenceEqual(host.CopyInitializerPatch())&&JsonSerializer.Serialize(p)==json,"Returned buffers and failures cannot mutate accepted result.");
            var repeat=await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(original,host);Check(repeat.PlanSha256==p.PlanSha256,"Exact same Host repeat.");
            var equalHost=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(original,scene,fallback);
            Check(equalHost.PlanSha256==host.PlanSha256,"Different actual host genuinely equal content.");Reject<InvalidDataException>(()=>p.RequireAncestry(equalHost));
            var altScene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,f,r,scene.Placement with{OriginZ=8909});
            var altHost=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(original,altScene,fallback);
            var alt=await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(original,altHost);
            Check(alt.LevelDataSha256!=p.LevelDataSha256&&alt.PlanSha256!=p.PlanSha256&&alt.CopyLevelData().AsSpan(0x100000,0x3000).SequenceEqual(altScene.CopyScene())&&
                alt.CopyLevelData().AsSpan(0,0x100000).SequenceEqual(p.CopyLevelData().AsSpan(0,0x100000))&&
                alt.CopyLevelData().AsSpan(0x103000).SequenceEqual(p.CopyLevelData().AsSpan(0x103000)),"Alternate valid actual scene changes only its retained scene owner, never replaced by primary golden.");
            Reject<InvalidDataException>(()=>p.RequireAncestry(altHost));
            await Reject<ArgumentException>(()=>NativeId65FreshWizardResearchBodyPlan.CaptureAsync("",host));
            await Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchBodyPlan.CaptureAsync(typeof(BodyCases).Assembly.Location,host));
            Check(JsonSerializer.Serialize(new{c,f,r,scene,fallback,host})==ancestors,"All prior instances/JSON remain untouched.");
            Console.WriteLine($"bodySha256={p.LevelDataSha256} headerSha256={p.HeaderSha256} transferSha256={p.TransferSourceSha256} planSha256={p.PlanSha256}");
        }
        finally{Original(original);}
        Console.WriteLine($"FRESH_WIZARD_BODY_SOURCE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }
    private static void Original(string path)
    {using var f=File.OpenRead(path);Check(f.Length==661547040&&HashStream(f)=="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"&&f.Position==661547040&&f.ReadByte()==-1&&f.Position==661547040&&f.Length==661547040,"Original whole source before/finally and actual EOF.");}
    private static string HashStream(Stream f)=>Convert.ToHexStringLower(SHA256.HashData(f));
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Put(Span<byte> b,int at,int n)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],n);
    private static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);checks++;}
    private static void Reject<T>(Action action)where T:Exception
    {try{action();}catch(T){checks++;refusals++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    private static async Task Reject<T>(Func<Task> action)where T:Exception
    {try{await action();}catch(T){checks++;refusals++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
