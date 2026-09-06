using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

// Explicit output-producing fault exercise. No default/source execution, no
// successful candidate publication, and no writes to source or retained sets.
internal static class SharedStagingCases
{
    internal static async Task RunAsync(string image,string workspace,string directory)
    {
        string root=Path.GetFullPath(directory);
        if(Directory.Exists(root)||File.Exists(root))throw new IOException("Fault exercise requires a new directory.");
        string sourceBefore=HashFile(image);
        Directory.CreateDirectory(root);
        int legacy=await StagingCases.RunAsync(image,workspace,Path.Combine(root,"legacy"));
        if(legacy!=14)throw new InvalidDataException("Legacy staging control changed its exact14 checks.");
        int checks=0;
        string cue=Path.Combine(root,"private-two-source.cue");
        byte[] cueBytes=DiscCueSheet.TextEncoding.GetBytes($"FILE \"{Path.GetFullPath(image)}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        File.WriteAllBytes(cue,cueBytes);
        var catalog=LevelCatalog.Load(workspace);
        var plan=await NativeDarkHollowTwoWizardResearchCandidatePlan.CaptureAsync(image,cue,catalog);
        var old=await NativeDarkHollowWizardResearchCandidatePlan.CaptureAsync(image,cue,catalog);
        string[] names=[NativeDarkHollowTwoWizardResearchCandidatePlan.ImageName,NativeDarkHollowTwoWizardResearchCandidatePlan.ReceiptName,NativeDarkHollowTwoWizardResearchCandidatePlan.CueName];

        foreach(var point in new[]{NativeWizardResearchFault.AfterImageWrite,NativeWizardResearchFault.BeforePublication})
        {
            string output=Path.Combine(root,"two-fault-"+point);
            await Reject(()=>NativeDarkHollowTwoWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,output,point,(at,path)=>
            {
                if(at!=NativeWizardResearchFault.BeforePublication)return;
                string stage=Path.GetDirectoryName(path)!;
                string[] before=names.Select(n=>HashFile(Path.Combine(stage,n))).ToArray();
                var verified=NativeDarkHollowTwoWizardResearchCandidatePublisher.VerifyAsync(plan,stage).GetAwaiter().GetResult();
                Check(verified.ImageSha256=="612e68e1a8642d42f2417fd6153368c9549094ac391a3835c9cf6dfee7808cba"&&verified.PlanSha256==plan.PlanSha256,"actual staged two-owner physical image independently pinned");
                // Complete-set branch is verification only; there is no commit.
                var retry=NativeDarkHollowTwoWizardResearchCandidatePublisher.PublishAsync(plan,stage).GetAwaiter().GetResult();
                Check(retry==verified,"complete staged retry verifies exact same immutable receipt");
                Check(names.Select(n=>HashFile(Path.Combine(stage,n))).SequenceEqual(before),"retry/verification never rewrites complete staged set");
                Reject(()=>NativeDarkHollowWizardResearchCandidatePublisher.VerifyAsync(old,stage),"occupied").GetAwaiter().GetResult();
                foreach(string name in names.Skip(1))
                {
                    string file=Path.Combine(stage,name);byte[] original=File.ReadAllBytes(file),changed=original.ToArray();changed[^1]^=1;
                    try
                    {
                        File.WriteAllBytes(file,changed);
                        Reject(()=>NativeDarkHollowTwoWizardResearchCandidatePublisher.VerifyAsync(plan,stage),"companion").GetAwaiter().GetResult();
                        File.WriteAllBytes(file,original);File.Delete(file);
                        Reject(()=>NativeDarkHollowTwoWizardResearchCandidatePublisher.VerifyAsync(plan,stage),"partial").GetAwaiter().GetResult();
                        File.CreateSymbolicLink(file,cue);
                        Reject(()=>NativeDarkHollowTwoWizardResearchCandidatePublisher.VerifyAsync(plan,stage),"symbolic").GetAwaiter().GetResult();
                    }
                    finally{File.Delete(file);File.WriteAllBytes(file,original);}
                }
                Check(names.Select(n=>HashFile(Path.Combine(stage,n))).SequenceEqual(before),"private tamper fixtures completely restored before deliberate fault");
            }),"Deliberate");Empty(output);
        }
        foreach(var point in new[]{NativeWizardResearchFault.AfterImageWrite,NativeWizardResearchFault.BeforePublication})
        {
            string output=Path.Combine(root,"two-cancel-"+point);using var canceled=new CancellationTokenSource();
            await Cancel(()=>NativeDarkHollowTwoWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,output,NativeWizardResearchFault.None,(at,_)=>
            {if(at==point)canceled.Cancel();},canceled.Token));Empty(output);
        }
        string early=Path.Combine(root,"two-cancel-before-io");
        using(var canceled=new CancellationTokenSource())
        {
            canceled.Cancel();await Cancel(()=>NativeDarkHollowTwoWizardResearchCandidatePublisher.PublishAsync(plan,early,canceled.Token));
            Check(!Directory.Exists(early),"canceled publication has not created an output root");
        }
        string corrupt=Path.Combine(root,"two-protected-sector");
        await Reject(()=>NativeDarkHollowTwoWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,corrupt,NativeWizardResearchFault.None,(at,path)=>
        {
            if(at!=NativeWizardResearchFault.AfterImageWrite)return;
            using FileStream stream=new(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);stream.Position=30L*2352+2072;
            int value=stream.ReadByte();stream.Position--;stream.WriteByte((byte)(value^1));
        }),"physical readback");Empty(corrupt);
        string late=Path.Combine(root,"two-late-source-cue");
        try
        {
            await Reject(()=>NativeDarkHollowTwoWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,late,NativeWizardResearchFault.None,(at,_)=>
            {if(at==NativeWizardResearchFault.BeforePublication)File.Delete(cue);}),"CUE");
        }
        finally{File.WriteAllBytes(cue,cueBytes);}
        Empty(late);await plan.RequireFreshAsync();Check(File.ReadAllBytes(cue).SequenceEqual(cueBytes),"only private CUE altered and restored exactly");
        string occupied=Path.Combine(root,"two-occupied");Directory.CreateDirectory(occupied);
        string sentinel=Path.Combine(occupied,"keep.txt");File.WriteAllText(sentinel,"unrelated user fixture");
        await Reject(()=>NativeDarkHollowTwoWizardResearchCandidatePublisher.PublishAsync(plan,occupied),"occupied");
        Check(File.ReadAllText(sentinel)=="unrelated user fixture","unrelated output content is preserved");
        string link=Path.Combine(root,"two-linked");Directory.CreateSymbolicLink(link,occupied);
        try{await Reject(()=>NativeDarkHollowTwoWizardResearchCandidatePublisher.PublishAsync(plan,link),"symbolic");}
        finally{File.Delete(link);}

        // Exercise the unchanged shared new-only transaction at every rename
        // boundary with tiny text fixtures, never a bootable candidate. Reflection
        // selects the exact private fault seam, not a production authority input.
        foreach(string boundary in new[]{"prepared","after-backup:0","after-backup:1","after-backup:2","after-publish:0","after-publish:1","after-publish:2"})
        {
            var (output,entries)=Tiny("rollback-"+boundary.Replace(':','-'));
            RefuseTransaction(()=>Invoke(output,entries,phase=>{if(phase==boundary)throw new IOException("deliberate tiny rename failure");}));
            Check(entries.All(e=>!File.Exists(e.DestinationPath)&&File.ReadAllText(e.StagedPath!)==Path.GetFileName(e.DestinationPath)),"all tiny staged owners restored after "+boundary);
            Check(!File.Exists(Path.Combine(output,".export-publication.lock"))&&!Directory.EnumerateDirectories(output,".export-recovery-*").Any(),"complete rollback releases only its own journal/lock");
        }
        var (recoveryRoot,recoveryEntries)=Tiny("rollback-incomplete");
        try
        {
            Invoke(recoveryRoot,recoveryEntries,phase=>
            {
                if(phase!="after-publish:0")return;
                File.WriteAllText(recoveryEntries[0].StagedPath!,"competing staged owner");throw new IOException("deliberate blocked rollback");
            });
            throw new InvalidDataException("Incomplete rollback did not retain typed recovery.");
        }
        catch(ExportArtifactRollbackException error)
        {
            Check(Directory.Exists(error.RetainedRecoveryDirectory)&&File.Exists(Path.Combine(error.RetainedRecoveryDirectory,"recovery.json")),"incomplete rollback preserves typed journal");
            Check(File.Exists(Path.Combine(recoveryRoot,".export-publication.lock"))&&File.ReadAllText(recoveryEntries[0].StagedPath!)=="competing staged owner"&&File.ReadAllText(recoveryEntries[0].DestinationPath)==Path.GetFileName(recoveryEntries[0].DestinationPath),"incomplete rollback preserves lock and both competing owners for recovery");
            Console.WriteLine("NATIVE_TWO_WIZARD_TINY_RECOVERY_RETAINED "+error.RetainedRecoveryDirectory);
        }
        Check(HashFile(image)==sourceBefore&&sourceBefore=="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37","complete original source BIN unchanged before/after all fault tests");
        Console.WriteLine($"NATIVE_TWO_WIZARD_RESEARCH_STAGING_OK legacyChecks={legacy} twoAndTransactionChecks={checks} successfulCandidatePublications=0 root={root}");

        void Check(bool value,string reason){if(!value)throw new InvalidDataException(reason);checks++;}
        void Empty(string path)=>Check(Directory.Exists(path)&&!Directory.EnumerateFileSystemEntries(path).Any(),"failed/canceled candidate stage cleaned without final artifacts");
        async Task Reject(Func<Task<NativeWizardResearchPublication>> action,string fragment)
        {try{await action();}catch(Exception e)when((e is IOException or InvalidDataException)&&e.Message.Contains(fragment,StringComparison.OrdinalIgnoreCase)){checks++;return;}throw new InvalidDataException("Expected refusal: "+fragment);}
        async Task Cancel(Func<Task<NativeWizardResearchPublication>> action)
        {try{await action();}catch(OperationCanceledException){checks++;return;}throw new InvalidDataException("Expected cancellation.");}
        void RefuseTransaction(Action action)
        {try{action();}catch(IOException error)when(error is not ExportArtifactRollbackException&&error.Message=="deliberate tiny rename failure"){checks++;return;}throw new InvalidDataException("Expected exact tiny transaction failure.");}
        (string Root,ExportArtifactReplacement[] Entries) Tiny(string name)
        {
            string output=Path.Combine(root,"tiny-"+name),stage=Path.Combine(output,"staging");Directory.CreateDirectory(stage);
            var entries=names.Select(n=>new ExportArtifactReplacement(Path.Combine(output,n),Path.Combine(stage,n))).ToArray();
            foreach(var entry in entries)File.WriteAllText(entry.StagedPath!,Path.GetFileName(entry.DestinationPath));
            return(output,entries);
        }
    }
    private static string HashFile(string path)
    {using FileStream input=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);return Convert.ToHexStringLower(SHA256.HashData(input));}
    private static void Invoke(string root,IReadOnlyList<ExportArtifactReplacement> entries,Action<string> checkpoint)
    {
        try
        {
            typeof(ExportArtifactTransaction).GetMethod("PublishCoreWithPolicy",BindingFlags.NonPublic|BindingFlags.Static,null,
                [typeof(string),typeof(IReadOnlyList<ExportArtifactReplacement>),typeof(CancellationToken),typeof(Action<string>),typeof(bool)],null)!
                .Invoke(null,[root,entries,CancellationToken.None,checkpoint,true]);
        }
        catch(TargetInvocationException error)when(error.InnerException!=null)
        {System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();}
    }
}
