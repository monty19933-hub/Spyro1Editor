using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class SourceCases
{
    private static int checks,refusals;
    internal static void SourceFree()
    {
        DiscCases.Geometry(Check,Reject);
        byte[] original=new byte[96];var row=new NativeFlatIsoFile("WAD.WAD",37,0x6927000,20);
        Both(original,22,row.Lba);Both(original,30,row.Size);byte[] after=original.ToArray();
        NativeId65FreshWizardResearchDiscPlan.PatchRoot(after,row,37,0x6B44800);
        Check(BinaryPrimitives.ReadInt32LittleEndian(after.AsSpan(30))==0x6B44800&&
            BinaryPrimitives.ReadInt32BigEndian(after.AsSpan(34))==0x6B44800&&after.AsSpan(0,30).SequenceEqual(original.AsSpan(0,30))&&
            after.AsSpan(38).SequenceEqual(original.AsSpan(38)),"Only exact both-endian metadata owner changed.");
        NativeId65FreshWizardResearchDiscPlan.PatchRoot(after,row with{Size=0x6B44800},37,row.Size);
        Check(after.SequenceEqual(original),"Neutral both-endian full inverse preserves complete bytes.");
        foreach(int at in new[]{22,26,30,34})
        {
            byte[] bad=original.ToArray();bad[at]^=1;byte[] before=bad.ToArray();
            Reject(()=>NativeId65FreshWizardResearchDiscPlan.PatchRoot(bad,row,37,0x6B44800));
            Check(bad.SequenceEqual(before),"Mismatched endian preimage cannot partially patch metadata.");
        }
        foreach(var bad in new[]{row with{RecordOffset=-1},row with{RecordOffset=int.MaxValue},row with{RecordOffset=63},row with{Name="PETEXA0.STR"},row with{IsDirectory=true}})
            Reject(()=>NativeId65FreshWizardResearchDiscPlan.PatchRoot(after,bad,37,0x6B44800));
        foreach(var pair in new[]{(-1,0x6B44800),(37,0),(37,-1)})Reject(()=>NativeId65FreshWizardResearchDiscPlan.PatchRoot(after,row,pair.Item1,pair.Item2));
        Check(after.SequenceEqual(original),"Every neutral refusal preserves input.");
        Console.WriteLine($"FRESH_WIZARD_DISC_FREE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }
    internal static async Task Source(string path)
    {
        Original(path);
        try
        {
            var intent=NativeId65FreshDocumentIntent.Create(8448,8448,8704,1024,1024,8960,8960,64,new(80,128,112),new(128,96,64),19);
            var c=await NativeId65FreshDocumentCompilation.CaptureAsync(path,intent);
            var family=await NativeWizardFamilyAssetPlan.CaptureAsync(path);
            var resource=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(path,c,family);
            var scene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(path,c,family,resource,new(8704,8960,8704,9216,8960));
            var fallback=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(path,resource);
            var host=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(path,scene,fallback);
            var body=await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(path,host);
            var exe=await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(path,body);
            var equal=await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(path,body);
            Check(!ReferenceEquals(exe,equal)&&exe.PlanSha256==equal.PlanSha256,"Genuinely equal-content separate issued EXE control.");
            string ancestors=JsonSerializer.Serialize(new{c,family,resource,scene,fallback,host,body,exe,equal});
            var plan=await DiscCases.PrimarySource(path,exe,equal,Check,Reject);
            Check(plan.SourceCertified&&!plan.RuntimeVerified&&!plan.PublicationApproved&&!plan.InstalledDispatchProven&&
                !plan.GlobalTransitionState12Supported&&!plan.PersistentOwnerIssued&&!plan.CardOrHistoryCompatibilityProven&&
                !plan.WholePickupOrDeathProven&&!plan.GlobalAudioCompatibilityProven&&!plan.PeakRuntimeMemoryProven&&
                !plan.HardwareTransfersProven&&!plan.CompleteIncomingEdgeClosureProven&&!plan.CompleteNativeDependencyClosureProven&&!plan.NormalCreateBinEnabled,
                "Detached physical bytes/readback do not grant pending native/runtime/publication owners.");
            Check(plan.ExecutablePlanSha256==exe.PlanSha256&&plan.BodyPlanSha256==body.PlanSha256&&plan.HostPlanSha256==host.PlanSha256&&
                plan.CanonicalDocumentSha256==c.CanonicalSha256&&plan.ChangedSectorOwnerCount==1290&&plan.FinalExecutableLba==54958&&
                plan.FinalExecutableEndExclusive==55162&&plan.FinalWadByteLength==0x6B44800,"Complete actual chain and derived final layout.");
            var copies=new[]{plan.CopyBody(),plan.CopyOverlay(),plan.CopyExecutable(),plan.CopyWadDirectory(),plan.CopyIsoRootDirectory()};
            string[] hashes=copies.Select(b=>Hash(b)).ToArray();string saved=JsonSerializer.Serialize(plan);
            foreach(byte[] bytes in copies){bytes[0]^=1;bytes[^1]^=1;}
            Check(new[]{plan.CopyBody(),plan.CopyOverlay(),plan.CopyExecutable(),plan.CopyWadDirectory(),plan.CopyIsoRootDirectory()}.Select(b=>Hash(b)).SequenceEqual(hashes)&&
                saved==JsonSerializer.Serialize(plan),"Every returned payload and read-only metadata is detached.");
            byte[] sourceRoot,sourceWad;
            using(var image=File.OpenRead(path))
            {var l=DiscImage.DetectLayout(image);sourceRoot=DiscImage.ReadFileBytes(image,l,22,0,2048);sourceWad=DiscImage.ReadFileBytes(image,l,37,0,2048);}
            byte[] changedRoot=plan.CopyIsoRootDirectory(),changedWad=plan.CopyWadDirectory();
            Check(Enumerable.Range(0,2048).Where(i=>changedRoot[i]!=sourceRoot[i]).All(i=>i>=562&&i<570||i>=690&&i<698)&&
                changedWad.AsSpan(0,79*8).SequenceEqual(sourceWad.AsSpan(0,79*8))&&changedWad.AsSpan(81*8).SequenceEqual(sourceWad.AsSpan(81*8)),
                "Complete root/WAD complement: only exact EXE LBA and WAD length/reserved pair differ.");
            Check(BinaryPrimitives.ReadInt32LittleEndian(changedRoot.AsSpan(562))==54958&&BinaryPrimitives.ReadInt32BigEndian(changedRoot.AsSpan(566))==54958&&
                BinaryPrimitives.ReadInt32LittleEndian(changedRoot.AsSpan(690))==0x6B44800&&BinaryPrimitives.ReadInt32BigEndian(changedRoot.AsSpan(694))==0x6B44800,
                "Both endian exact original record offsets survive physical assembly.");
            RequireBoundaries(path,plan,body);
            var alternateScene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(path,c,family,resource,scene.Placement with{OriginZ=8909});
            var alternateHost=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(path,alternateScene,fallback);
            var alternateBody=await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(path,alternateHost);
            var alternateExe=await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(path,alternateBody);
            var alternate=await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(path,alternateExe);
            Check(alternateExe.CopyExecutable().SequenceEqual(exe.CopyExecutable())&&alternate.BodySha256!=plan.BodySha256&&
                alternate.ExecutablePlanSha256!=plan.ExecutablePlanSha256&&alternate.PlanSha256!=plan.PlanSha256&&
                ReferenceEquals(alternate.ExecutableAncestor,alternateExe)&&alternate.CopyBody().AsSpan(0x100000,0x3000).SequenceEqual(alternateScene.CopyScene()),
                "Equal EXE never replaces actual alternate issued scene/body.");
            Reject(()=>plan.RequireAncestry(alternateExe));
            string alternateHash=AlternateInverse(path,alternate);
            using(var original=File.OpenRead(path))using(var expectedSource=File.OpenRead(path))
            using(var expected=new DiscStreamCases.ExpectedImage(expectedSource,alternate))
            {
                var verified=alternate.VerifyImage(original,expected);
                Check(verified.ChangedSectors==1290&&verified.OutputSha256==alternateHash,"Alternate successful readback returns its own computed physical identity, not the primary golden.");
            }
            await CaptureCases.Source(path,exe,Check);
            DiscStreamCases.Source(path,plan,Check);
            using(var cancel=new CancellationTokenSource())
            {cancel.Cancel();await RefuseAsync<OperationCanceledException>(()=>NativeId65FreshWizardResearchDiscPlan.CaptureAsync("must-not-open.bin",exe,cancel.Token));}
            await RefuseAsync<ArgumentException>(()=>NativeId65FreshWizardResearchDiscPlan.CaptureAsync("",exe));
            await RefuseAsync<InvalidDataException>(()=>NativeId65FreshWizardResearchDiscPlan.CaptureAsync(typeof(SourceCases).Assembly.Location,exe));
            Check(JsonSerializer.Serialize(new{c,family,resource,scene,fallback,host,body,exe,equal})==ancestors&&JsonSerializer.Serialize(plan)==saved,
                "All refusals and verifier streams preserve every actual ancestor and immutable Plan.");
            Console.WriteLine($"planSha256={plan.PlanSha256} alternatePlanSha256={alternate.PlanSha256} alternatePhysicalSha256={alternateHash}");
        }
        finally{Original(path);}
        Console.WriteLine($"FRESH_WIZARD_DISC_SOURCE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }
    private static void RequireBoundaries(string path,NativeId65FreshWizardResearchDiscPlan plan,NativeId65FreshWizardResearchBodyPlan body)
    {
        using var f=File.OpenRead(path);byte[] b=new byte[2352];byte[] overlay=plan.CopyOverlay(),payload=body.CopyLevelData(),exe=plan.CopyExecutable();
        foreach(int lba in new[]{22,37,53874,53875,53908,53909,54078,54079,54957,54958,55124,55125,55161})
        {
            f.Position=lba*2352L;f.ReadExactly(b);byte[] after=plan.CreateExpectedRawSector(lba,b);
            Check(after.AsSpan(0,16).SequenceEqual(b.AsSpan(0,16))&&after.AsSpan(16,4).SequenceEqual(after.AsSpan(20,4)),"Destination sync/MSF/mode retained; both subheaders agree.");
            int flags=lba==22||lba is 54957 or 55161?0x89:8;Check(after[18]==flags,"Exact original terminal flags clear/move; destination remains Form1.");
            byte[] rebuilt=after.ToArray();RawMode2Form1SectorIntegrity.RebuildLogicalWriteSector(rebuilt,new(2352,24,22,2048),lba);
            Check(rebuilt.SequenceEqual(after),"Full sector afterimage has stable exact EDC/ECC.");
            if(lba==53874)Check(after.AsSpan(24,2048).SequenceEqual(b.AsSpan(24,2048)),"Old WAD terminal payload remains exact.");
            if(lba is 53875 or 53908)Check(after.AsSpan(24,2048).SequenceEqual(overlay.AsSpan((lba-53875)*2048,2048)),"Actual Host first/last physical payload.");
            if(lba is 53909 or 54957)Check(after.AsSpan(24,2048).SequenceEqual(payload.AsSpan((lba-53909)*2048,2048)),"Actual Body first/last physical payload.");
            if(lba is 54958 or 55161)Check(after.AsSpan(24,2048).SequenceEqual(exe.AsSpan((lba-54958)*2048,2048)),"Actual compiled EXE first/last physical payload, not original EXE.");
        }
    }
    private static string AlternateInverse(string path,NativeId65FreshWizardResearchDiscPlan plan)
    {
        using var f=File.OpenRead(path);using var forward=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);using var inverse=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] b=new byte[2352];int changed=0;
        for(int lba=0;lba<281270;lba++)
        {f.ReadExactly(b);byte[] after=plan.CreateExpectedRawSector(lba,b),restored=plan.CreateOriginalRawSector(lba,after);Check(restored.SequenceEqual(b),"Every alternate physical sector actually inverted.");forward.AppendData(after);inverse.AppendData(restored);if(!after.SequenceEqual(b))changed++;}
        Check(changed==1290&&Convert.ToHexStringLower(inverse.GetHashAndReset())==NativeId65FreshDiscSource.OriginalSha256&&f.ReadByte()==-1&&f.Position==661547040&&f.Length==661547040,"Alternate complete inverse and EOF.");
        return Convert.ToHexStringLower(forward.GetHashAndReset());
    }
    private static void Both(byte[] b,int at,int value){BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at),value);BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(at+4),value);}
    internal static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);checks++;}
    private static void Reject(Action a)=>Reject(a,"Expected bounded InvalidData refusal.");
    private static void Reject(Action a,string why){try{a();}catch(InvalidDataException){refusals++;return;}throw new Exception(why);}
    private static async Task RefuseAsync<T>(Func<Task> a)where T:Exception{try{await a();}catch(T){refusals++;return;}throw new Exception("Expected "+typeof(T).Name);}
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Original(string path){using var f=File.OpenRead(path);Check(f.Length==661547040&&Convert.ToHexStringLower(SHA256.HashData(f))==NativeId65FreshDiscSource.OriginalSha256&&f.Position==661547040&&f.ReadByte()==-1&&f.Position==661547040&&f.Length==661547040,"Original complete source SHA/actual EOF/finally.");}
}
