using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// NOT dispatched by default/source. Requires separate exact-directory approval.
// Every publisher invocation forces precommit failure even in path/lock tests.
internal static class StagingCases
{
    internal static async Task Run(NativeId65FreshWizardResearchCandidatePlan original,string workspace)
    {
        string root=Path.GetFullPath(workspace);
        PersistencePathSafety.EnsureTreesDoNotOverlap(original.SourceImagePath,root,"Fault fixture overlaps original BIN.");
        PersistencePathSafety.EnsureTreesDoNotOverlap(original.SourceCuePath,root,"Fault fixture overlaps original CUE.");
        if(Exists(root))throw new IOException("Wizard fault workspace must be absent and non-linked.");
        await original.RequireFreshAsync();
        string originalCueHash=FileHash(original.SourceCuePath);int checks=0;
        Directory.CreateDirectory(root);
        string cue=Path.Combine(root,"private-source.cue"),alias=Path.Combine(root,"private-bin-alias");
        File.CreateSymbolicLink(alias,original.SourceImagePath);
        byte[] cueOwner=Encoding.UTF8.GetBytes($"FILE \"{alias}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        File.WriteAllBytes(cue,cueOwner);
        var plan=await NativeId65FreshWizardResearchCandidatePlan.CaptureAsync(original.SourceImagePath,cue,original.DiscAncestor);
        try
        {
            foreach(var fault in new[]{NativeId65FreshWizardResearchFault.AfterFirstSector,NativeId65FreshWizardResearchFault.AfterImageWrite,NativeId65FreshWizardResearchFault.BeforePublication})
            {
                string output=Path.Combine(root,"forced-"+fault);bool reached=false;
                await Refuse(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,output,fault,(point,image)=>
                {
                    if(point!=fault)return;reached=true;
                    if(point==NativeId65FreshWizardResearchFault.AfterFirstSector)
                    {
                        // The production stream has checked logical2352-byte
                        // Length/Position; its buffer need not be flushed yet.
                        // Complete flush/reopen is tested at BeforePublication.
                        long visible=new FileInfo(image).Length;
                        Check(File.Exists(image)&&new FileInfo(image).LinkTarget==null&&visible is 0 or 2352,
                            "First logical pair has a new staged file; buffered disk length is not durability proof.");
                        Console.WriteLine($"FRESH_WIZARD_FIRST_PAIR_FILE_OBSERVATION visibleBytes={visible} logicalPairBytes=2352 flushClaimed=false");
                    }
                    if(point!=NativeId65FreshWizardResearchFault.BeforePublication)return;
                    var verified=NativeId65FreshWizardResearchCandidatePublisher.VerifyAsync(plan,Path.GetDirectoryName(image)!).GetAwaiter().GetResult();
                    Check(verified.ImageSha256==plan.ExpectedImageSha256&&verified.ChangedRawSectors==1290&&!verified.RuntimeVerified&&!verified.NormalCreateBinApproved,
                        "Actual complete staged image/CUE/receipt reopens under exact primary identity before forced refusal.");
                }),"Deliberate");
                Check(reached,"Actual requested precommit phase reached.");Empty(output);
            }
            // Header, parity, newly added beyond-old-profile EXE and untouched
            // sector corruption: none modifies either original source.
            foreach(long offset in new[]{22L*2352+12,37L*2352+2076,55125L*2352+24,30L*2352+2072})
            {
                string output=Path.Combine(root,"corrupt-"+offset);
                await Refuse(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,output,
                    NativeId65FreshWizardResearchFault.BeforePublication,(point,image)=>
                    {
                        if(point!=NativeId65FreshWizardResearchFault.AfterImageWrite)return;
                        using var f=new FileStream(image,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
                        f.Position=offset;int b=f.ReadByte();f.Position--;f.WriteByte((byte)(b^1));f.Flush(true);
                    }),null,typeof(InvalidDataException));
                Empty(output);
            }
            foreach(string name in new[]{NativeId65FreshWizardResearchCandidatePlan.CueName,NativeId65FreshWizardResearchCandidatePlan.ReceiptName})
            {
                string output=Path.Combine(root,"late-"+Path.GetExtension(name));
                await Refuse(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,output,
                    NativeId65FreshWizardResearchFault.BeforePublication,(point,image)=>
                    {
                        if(point!=NativeId65FreshWizardResearchFault.BeforePublication)return;
                        string file=Path.Combine(Path.GetDirectoryName(image)!,name);
                        using var f=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
                        int b=f.ReadByte();f.Position=0;f.WriteByte((byte)(b^1));f.Flush(true);
                    }),"companion exact");
                Empty(output);
            }
            foreach(var phase in new[]{NativeId65FreshWizardResearchFault.AfterFirstSector,NativeId65FreshWizardResearchFault.AfterImageWrite,NativeId65FreshWizardResearchFault.BeforePublication})
            {
                string output=Path.Combine(root,"cancel-"+phase);using var cancel=new CancellationTokenSource();bool reached=false;
                await Refuse(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,output,
                    NativeId65FreshWizardResearchFault.BeforePublication,(point,_)=>{if(point==phase){reached=true;cancel.Cancel();}},cancel.Token),
                    null,typeof(OperationCanceledException));
                Check(reached&&cancel.IsCancellationRequested,"Actual precommit cancellation checkpoint.");Empty(output);
            }
            foreach(string change in new[]{"cue-bytes","supplied-cue-link","third-bin-alias"})
            {
                string output=Path.Combine(root,"late-source-"+change),replacement=Path.Combine(root,"replacement.cue"),tiny=Path.Combine(root,"replacement.bin");
                try
                {
                    await Refuse(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,output,
                        NativeId65FreshWizardResearchFault.BeforePublication,(point,_)=>
                        {
                            if(point!=NativeId65FreshWizardResearchFault.BeforePublication)return;
                            if(change=="cue-bytes")File.WriteAllBytes(cue,[..cueOwner,10]);
                            else if(change=="supplied-cue-link")
                            {File.WriteAllBytes(replacement,cueOwner);File.Delete(cue);File.CreateSymbolicLink(cue,replacement);}
                            else{File.WriteAllBytes(tiny,[1]);File.Delete(alias);File.CreateSymbolicLink(alias,tiny);}
                        }),"CUE");
                }
                finally
                {
                    File.Delete(cue);File.WriteAllBytes(cue,cueOwner);
                    File.Delete(alias);File.CreateSymbolicLink(alias,original.SourceImagePath);
                }
                Empty(output);await plan.RequireFreshAsync();
            }
            string retained=Path.Combine(root,"foreign-stage"),retainedStage="";
            await Refuse(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,retained,
                NativeId65FreshWizardResearchFault.AfterFirstSector,(point,image)=>
                {
                    if(point!=NativeId65FreshWizardResearchFault.AfterFirstSector)return;
                    retainedStage=Path.GetDirectoryName(image)!;File.WriteAllText(Path.Combine(retainedStage,"foreign.txt"),"preserve this exact new owner");
                }),"Deliberate");
            Check(File.ReadAllText(Path.Combine(retainedStage,"foreign.txt"))=="preserve this exact new owner"&&
                File.Exists(Path.Combine(retained,".native-id65-fresh-wizard.writer.lock"))&&new FileInfo(Path.Combine(retained,".native-id65-fresh-wizard.writer.lock")).Length==32&&
                !File.Exists(Path.Combine(retainedStage,NativeId65FreshWizardResearchCandidatePlan.ImageName)),
                "Precommit unknown stage owner and marker lease retained, only owned partial image removed.");
            foreach(string entry in new[]{"foreign.txt",".native-id65-fresh-wizard.writer.lock",".export-publication.lock"})
            {
                string occupied=Path.Combine(root,"occupied-"+entry.TrimStart('.'));Directory.CreateDirectory(occupied);
                string sentinel=Path.Combine(occupied,entry);File.WriteAllText(sentinel,"existing owner");
                await Refuse(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,occupied,
                    NativeId65FreshWizardResearchFault.BeforePublication),"occupied");
                Check(File.ReadAllText(sentinel)=="existing owner"&&Directory.GetFileSystemEntries(occupied).Length==1,"Occupied owner unchanged.");
            }
            string linked=Path.Combine(root,"linked-output");Directory.CreateSymbolicLink(linked,retained);
            await Refuse(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,linked,
                NativeId65FreshWizardResearchFault.BeforePublication),"symbolic");
            foreach(string overlap in new[]{plan.SourceImagePath,plan.SourceCuePath,Path.GetDirectoryName(plan.SourceImagePath)!})
                await Refuse(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,overlap,
                    NativeId65FreshWizardResearchFault.BeforePublication),"overlap",typeof(InvalidOperationException));
            // Duplicate invocation while the outer first-pair lease is owned.
            string duplicate=Path.Combine(root,"duplicate");
            await Refuse(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,duplicate,
                NativeId65FreshWizardResearchFault.AfterFirstSector,(point,_)=>
                {
                    if(point==NativeId65FreshWizardResearchFault.AfterFirstSector)
                        Refuse(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,duplicate,
                            NativeId65FreshWizardResearchFault.BeforePublication),"occupied").GetAwaiter().GetResult();
                }),"Deliberate");
            Empty(duplicate);
        }
        finally
        {
            await original.RequireFreshAsync(CancellationToken.None);
            Check(FileHash(original.SourceCuePath)==originalCueHash,"Complete unchanged original CUE after every attempted staged materialization.");
        }
        Console.WriteLine($"FRESH_WIZARD_RESEARCH_STAGING_OK checks={checks} successfulPublications=0 retainedEvidence={root}");
        void Check(bool ok,string message){if(!ok)throw new InvalidDataException(message);checks++;}
        void Empty(string output)=>Check(Directory.Exists(output)&&!Directory.EnumerateFileSystemEntries(output).Any(),"Owned partial stage and lease cleaned; no published artifacts.");
        async Task Refuse(Func<Task> action,string? message,Type? type=null)
        {
            try{await action();}
            catch(Exception error)when((type==null?error is IOException or InvalidDataException:error.GetType()==type)&&
                (message==null||error.Message.Contains(message,StringComparison.OrdinalIgnoreCase))){checks++;return;}
            throw new InvalidDataException("Expected actual forced precommit refusal: "+message);
        }
    }
    private static bool Exists(string path)=>File.Exists(path)||Directory.Exists(path)||new FileInfo(path).LinkTarget!=null||new DirectoryInfo(path).LinkTarget!=null;
    private static string FileHash(string path){using var f=File.OpenRead(path);long n=f.Length;string hash=Convert.ToHexStringLower(SHA256.HashData(f));if(f.Position!=n||f.Length!=n||f.ReadByte()!=-1||f.Position!=n||f.Length!=n)throw new InvalidDataException("Actual complete source EOF changed.");return hash;}
}
