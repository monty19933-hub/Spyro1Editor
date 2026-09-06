using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

// Explicit output-producing fault exercise; never runs in source-free or
// read-only-source mode and never edits the real source CUE/BIN.
internal static class StagingCases
{
    internal static async Task<int> RunAsync(string image,string workspace,string directory)
    {
        int checks=0;string root=Path.GetFullPath(directory);
        if(Directory.Exists(root)||File.Exists(root))throw new IOException("Staging exercise requires a new directory.");
        Directory.CreateDirectory(root);string cue=Path.Combine(root,"private-source.cue");
        byte[] cueBytes=DiscCueSheet.TextEncoding.GetBytes($"FILE \"{Path.GetFullPath(image)}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        File.WriteAllBytes(cue,cueBytes);
        var plan=await NativeDarkHollowWizardResearchCandidatePlan.CaptureAsync(image,cue,LevelCatalog.Load(workspace));
        foreach(var fault in new[]{NativeWizardResearchFault.AfterImageWrite,NativeWizardResearchFault.BeforePublication})
        {
            string output=Path.Combine(root,fault.ToString());
            await Reject(()=>NativeDarkHollowWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,output,fault,(point,path)=>
            {
                if(point!=NativeWizardResearchFault.BeforePublication)return;
                string stage=Path.GetDirectoryName(path)!;
                var retry=NativeDarkHollowWizardResearchCandidatePublisher.PublishAsync(plan,stage).GetAwaiter().GetResult();
                Check(retry.PlanSha256==plan.PlanSha256&&!retry.RuntimeVerified,"immutable staged retry verification");
                foreach(string name in new[]{NativeDarkHollowWizardResearchCandidatePlan.CueName,NativeDarkHollowWizardResearchCandidatePlan.ReceiptName})
                {
                    string file=Path.Combine(stage,name);byte[] before=File.ReadAllBytes(file),changed=before.ToArray();changed[^1]^=1;File.WriteAllBytes(file,changed);
                    try{Reject(()=>NativeDarkHollowWizardResearchCandidatePublisher.VerifyAsync(plan,stage),"companion").GetAwaiter().GetResult();}
                    finally{File.WriteAllBytes(file,before);}
                }
            }),"Deliberate");Empty(output);
        }
        string corrupt=Path.Combine(root,"protected-sector");
        await Reject(()=>NativeDarkHollowWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,corrupt,NativeWizardResearchFault.None,(point,path)=>
        {
            if(point!=NativeWizardResearchFault.AfterImageWrite)return;
            using FileStream output=new(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);output.Position=30L*2352+2072;
            int before=output.ReadByte();output.Position--;output.WriteByte((byte)(before^1));
        }),"physical readback");Empty(corrupt);
        string late=Path.Combine(root,"late-cue");
        try
        {
            await Reject(()=>NativeDarkHollowWizardResearchCandidatePublisher.PublishForSmokeAsync(plan,late,NativeWizardResearchFault.None,(point,_)=>
            {if(point==NativeWizardResearchFault.BeforePublication)File.Delete(cue);}),"CUE");
        }
        finally{File.WriteAllBytes(cue,cueBytes);}
        Empty(late);await plan.RequireFreshAsync();
        string occupied=Path.Combine(root,"occupied");Directory.CreateDirectory(occupied);File.WriteAllText(Path.Combine(occupied,"keep.txt"),"user fixture");
        await Reject(()=>NativeDarkHollowWizardResearchCandidatePublisher.PublishAsync(plan,occupied),"occupied");
        Check(File.ReadAllText(Path.Combine(occupied,"keep.txt"))=="user fixture","occupied data preserved");
        string link=Path.Combine(root,"linked");Directory.CreateSymbolicLink(link,occupied);
        await Reject(()=>NativeDarkHollowWizardResearchCandidatePublisher.PublishAsync(plan,link),"symbolic");File.Delete(link);
        Console.WriteLine($"NATIVE_WIZARD_RESEARCH_STAGING_ROLLBACK_AND_COMPANIONS_OK checks={checks}");return checks;
        void Check(bool value,string name){if(!value)throw new InvalidDataException(name);checks++;}
        void Empty(string path)=>Check(!Directory.EnumerateFileSystemEntries(path).Any(),"failed staging leaves no bootable or partial output");
        async Task Reject(Func<Task<NativeWizardResearchPublication>> action,string fragment)
        {try{await action();}catch(Exception e)when((e is IOException or InvalidDataException)&&e.Message.Contains(fragment,StringComparison.OrdinalIgnoreCase)){checks++;return;}throw new InvalidDataException("Expected refusal: "+fragment);}
    }
}
