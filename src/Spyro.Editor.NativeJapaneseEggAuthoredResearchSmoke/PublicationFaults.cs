using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

// Explicit opt-in. Every image is private disposable staging; every exercise
// fails/cancels before the existing new-only publication transaction.
internal static class PublicationFaults
{
    internal static async Task Run(NativeJapaneseEggAuthoredResearchCandidatePlan plan,string directory,string expectedImage)
    {
        string root=Path.GetFullPath(directory);if(Directory.Exists(root)||File.Exists(root))throw new IOException("Fault exercise requires a NEW exact directory.");
        Directory.CreateDirectory(root);int checks=0;string[] names=NativeJapaneseEggAuthoredResearchCandidatePlan.OutputNames;
        foreach(var point in new[]{NativeWizardResearchFault.AfterImageWrite,NativeWizardResearchFault.BeforePublication})
        {
            string output=Path.Combine(root,"fault-"+point);
            await Reject(()=>NativeJapaneseEggAuthoredResearchCandidatePublisher.PublishForSmokeAsync(plan,output,point,(at,image)=>
            {
                Check(Path.GetFileName(image)==NativeJapaneseEggAuthoredResearchCandidatePlan.ImageName&&Path.GetFileName(Path.GetDirectoryName(image)!).StartsWith(".native-japanese-eggs-authored-colors-stage-",StringComparison.Ordinal),"Closed authored staging identity.");
                if(at!=NativeWizardResearchFault.BeforePublication)return;string stage=Path.GetDirectoryName(image)!;
                string[] hashes=names.Select(n=>Hash(Path.Combine(stage,n))).ToArray();
                var verified=NativeJapaneseEggAuthoredResearchCandidatePublisher.VerifyAsync(plan,stage).GetAwaiter().GetResult();
                Check(verified.ImageSha256==expectedImage&&verified.PlanSha256==plan.PlanSha256&&verified.IndependentColorVariants&&!verified.RuntimeVerified,"Full physical staged set is the closed diagnostic-color candidate.");
                Check(names.Select(n=>Hash(Path.Combine(stage,n))).SequenceEqual(hashes),"Verification does not mutate staged files.");
            }),"Deliberate");Clean(output);
        }
        foreach(var point in new[]{NativeWizardResearchFault.AfterImageWrite,NativeWizardResearchFault.BeforePublication})
        {
            string output=Path.Combine(root,"cancel-"+point);using var cancel=new CancellationTokenSource();
            await Cancel(()=>NativeJapaneseEggAuthoredResearchCandidatePublisher.PublishForSmokeAsync(plan,output,NativeWizardResearchFault.None,(at,_)=>{if(at==point)cancel.Cancel();},cancel.Token));Clean(output);
        }
        string early=Path.Combine(root,"cancel-before-io");using(var cancel=new CancellationTokenSource())
        {cancel.Cancel();await Cancel(()=>NativeJapaneseEggAuthoredResearchCandidatePublisher.PublishAsync(plan,early,cancel.Token));Check(!Directory.Exists(early),"Pre-cancel creates no directory.");}
        string raw=Path.Combine(root,"tampered-raw");
        await Reject(()=>NativeJapaneseEggAuthoredResearchCandidatePublisher.PublishForSmokeAsync(plan,raw,NativeWizardResearchFault.None,(at,image)=>
        {if(at!=NativeWizardResearchFault.AfterImageWrite)return;using FileStream f=new(image,FileMode.Open,FileAccess.ReadWrite,FileShare.None);f.Position=30L*2352+2072;int b=f.ReadByte();f.Position--;f.WriteByte((byte)(b^1));}),"physical readback");Clean(raw);
        foreach(string name in new[]{NativeJapaneseEggAuthoredResearchCandidatePlan.CueName,NativeJapaneseEggAuthoredResearchCandidatePlan.ReceiptName})
        {
            string output=Path.Combine(root,name.EndsWith(".cue",StringComparison.Ordinal)?"tampered-cue":"tampered-receipt");
            await Reject(()=>NativeJapaneseEggAuthoredResearchCandidatePublisher.PublishForSmokeAsync(plan,output,NativeWizardResearchFault.None,(at,image)=>
            {if(at!=NativeWizardResearchFault.BeforePublication)return;string file=Path.Combine(Path.GetDirectoryName(image)!,name);byte[] b=File.ReadAllBytes(file);b[^1]^=1;File.WriteAllBytes(file,b);}),"companion");Clean(output);
        }
        string occupied=Path.Combine(root,"occupied");Directory.CreateDirectory(occupied);string sentinel=Path.Combine(occupied,"keep.txt");File.WriteAllText(sentinel,"owned fixture");
        await Reject(()=>NativeJapaneseEggAuthoredResearchCandidatePublisher.PublishAsync(plan,occupied),"occupied");Check(File.ReadAllText(sentinel)=="owned fixture","Occupied output preserved.");
        string link=Path.Combine(root,"link");Directory.CreateSymbolicLink(link,occupied);
        try{await Reject(()=>NativeJapaneseEggAuthoredResearchCandidatePublisher.PublishAsync(plan,link),"symbolic");}finally{File.Delete(link);}
        File.Delete(sentinel);Directory.Delete(occupied);
        await Reject(()=>NativeJapaneseEggAuthoredResearchCandidatePublisher.PublishAsync(plan,plan.SourceImagePath),"source BIN");
        await Reject(()=>NativeJapaneseEggAuthoredResearchCandidatePublisher.PublishAsync(plan,plan.SourceCuePath),"source CUE");
        await Reject(()=>NativeJapaneseEggAuthoredResearchCandidatePublisher.PublishAsync(plan,plan.JapaneseSourceImagePath),"Japanese donor");
        await Reject(()=>NativeJapaneseEggAuthoredResearchCandidatePublisher.PublishAsync(plan,Path.GetDirectoryName(plan.JapaneseSourceImagePath)!),"Japanese donor");
        await plan.RequireFreshAsync();Check(!Directory.EnumerateFileSystemEntries(root).Any(),"No remaining staging or committed artifacts; sources remain exact.");
        Console.WriteLine($"NATIVE_JP_EGG_AUTHORED_PUBLICATION_FAULTS_OK checks={checks} successfulCandidatePublications=0 originalsUnchanged=true stagedCopiesCleaned=true root={root}");

        void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);checks++;}
        void Clean(string output){Check(Directory.Exists(output)&&!Directory.EnumerateFileSystemEntries(output).Any(),"Failure/cancel cleans only this invocation's staging owners.");Directory.Delete(output);}
        async Task Reject(Func<Task<NativeJapaneseEggAuthoredResearchPublication>> action,string expected)
        {try{await action();}catch(Exception e)when((e is IOException or InvalidDataException or InvalidOperationException)&&e.Message.Contains(expected,StringComparison.OrdinalIgnoreCase)){checks++;return;}throw new InvalidDataException("Expected authored fault: "+expected);}
        async Task Cancel(Func<Task<NativeJapaneseEggAuthoredResearchPublication>> action)
        {try{await action();}catch(OperationCanceledException){checks++;return;}throw new InvalidDataException("Expected authored cancellation.");}
    }
    private static string Hash(string path){using FileStream f=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);return Convert.ToHexStringLower(SHA256.HashData(f));}
}
