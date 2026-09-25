using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

// Opt-in only. Every large image is a disposable staging copy and every attempt
// fails/cancels before the existing new-only transaction. Originals are read-only.
internal static class PublicationFaults
{
    internal static async Task Run(NativeJapaneseEggResearchCandidatePlan plan,string directory,string expectedImage)
    {
        string root=Path.GetFullPath(directory);
        if(Directory.Exists(root)||File.Exists(root))throw new IOException("F5 fault exercise requires a NEW exact directory.");
        int checks=0;Directory.CreateDirectory(root);
        string[] names=NativeJapaneseEggResearchCandidatePlan.OutputNames;
        foreach(var point in new[]{NativeWizardResearchFault.AfterImageWrite,NativeWizardResearchFault.BeforePublication})
        {
            string output=Path.Combine(root,"fault-"+point);
            await Reject(()=>NativeJapaneseEggResearchCandidatePublisher.PublishForSmokeAsync(plan,output,point,(at,image)=>
            {
                Check(Path.GetFileName(image)==NativeJapaneseEggResearchCandidatePlan.ImageName&&Path.GetFileName(Path.GetDirectoryName(image)!).StartsWith(".native-japanese-eggs-stage-",StringComparison.Ordinal),"Actual staged image uses fixed F5 role names.");
                if(at!=NativeWizardResearchFault.BeforePublication)return;
                string stage=Path.GetDirectoryName(image)!;
                string[] before=names.Select(n=>Hash(Path.Combine(stage,n))).ToArray();
                var verified=NativeJapaneseEggResearchCandidatePublisher.VerifyAsync(plan,stage).GetAwaiter().GetResult();
                Check(verified.ImageSha256==expectedImage&&verified.PlanSha256==plan.PlanSha256&&verified.ResearchOnly&&!verified.RuntimeVerified&&!verified.NormalCreateBinApproved,"Actual whole physical staging is the fixed visual-only candidate.");
                Check(names.Select(n=>Hash(Path.Combine(stage,n))).SequenceEqual(before),"Complete-set verification makes no staged artifact changes.");
            }),"Deliberate");EmptyAndRemove(output);
        }
        foreach(var point in new[]{NativeWizardResearchFault.AfterImageWrite,NativeWizardResearchFault.BeforePublication})
        {
            string output=Path.Combine(root,"cancel-"+point);using var canceled=new CancellationTokenSource();
            await Cancel(()=>NativeJapaneseEggResearchCandidatePublisher.PublishForSmokeAsync(plan,output,NativeWizardResearchFault.None,(at,_)=>
            {if(at==point)canceled.Cancel();},canceled.Token));EmptyAndRemove(output);
        }
        string early=Path.Combine(root,"cancel-before-io");
        using(var canceled=new CancellationTokenSource())
        {canceled.Cancel();await Cancel(()=>NativeJapaneseEggResearchCandidatePublisher.PublishAsync(plan,early,canceled.Token));Check(!Directory.Exists(early),"Pre-cancelled publication creates no directory.");}
        string raw=Path.Combine(root,"tampered-raw");
        await Reject(()=>NativeJapaneseEggResearchCandidatePublisher.PublishForSmokeAsync(plan,raw,NativeWizardResearchFault.None,(at,image)=>
        {
            if(at!=NativeWizardResearchFault.AfterImageWrite)return;
            using FileStream f=new(image,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
            f.Position=30L*2352+2072;int b=f.ReadByte();f.Position--;f.WriteByte((byte)(b^1));
        }),"physical readback");EmptyAndRemove(raw);
        foreach(string name in new[]{NativeJapaneseEggResearchCandidatePlan.CueName,NativeJapaneseEggResearchCandidatePlan.ReceiptName})
        {
            string output=Path.Combine(root,"tampered-"+(name.EndsWith(".cue",StringComparison.Ordinal)?"cue":"receipt"));
            await Reject(()=>NativeJapaneseEggResearchCandidatePublisher.PublishForSmokeAsync(plan,output,NativeWizardResearchFault.None,(at,image)=>
            {
                if(at!=NativeWizardResearchFault.BeforePublication)return;
                string file=Path.Combine(Path.GetDirectoryName(image)!,name);byte[] b=File.ReadAllBytes(file);b[^1]^=1;File.WriteAllBytes(file,b);
            }),"companion");EmptyAndRemove(output);
        }
        string occupied=Path.Combine(root,"occupied");Directory.CreateDirectory(occupied);
        string sentinel=Path.Combine(occupied,"keep.txt");File.WriteAllText(sentinel,"owned test sentinel");
        await Reject(()=>NativeJapaneseEggResearchCandidatePublisher.PublishAsync(plan,occupied),"occupied");
        Check(File.ReadAllText(sentinel)=="owned test sentinel","Occupied output never changes unrelated content.");
        string link=Path.Combine(root,"linked-output");Directory.CreateSymbolicLink(link,occupied);
        try{await Reject(()=>NativeJapaneseEggResearchCandidatePublisher.PublishAsync(plan,link),"symbolic");}
        finally{File.Delete(link);}
        File.Delete(sentinel);Directory.Delete(occupied);
        await Reject(()=>NativeJapaneseEggResearchCandidatePublisher.PublishAsync(plan,Path.GetDirectoryName(plan.JapaneseSourceImagePath)!),"Japanese donor");
        await Reject(()=>NativeJapaneseEggResearchCandidatePublisher.PublishAsync(plan,plan.JapaneseSourceImagePath),"Japanese donor");
        await Reject(()=>NativeJapaneseEggResearchCandidatePublisher.PublishAsync(plan,plan.SourceImagePath),"source BIN");
        await Reject(()=>NativeJapaneseEggResearchCandidatePublisher.PublishAsync(plan,plan.SourceCuePath),"source CUE");
        await plan.RequireFreshAsync();Check(true,"Complete original JP/USA/CUE identities unchanged after all disposable faults.");
        Check(!Directory.EnumerateFileSystemEntries(root).Any(),"Only this run's known temporary owners were removed; no residual bootable image.");
        Console.WriteLine($"NATIVE_JP_EGG_PUBLICATION_FAULTS_OK checks={checks} successfulCandidatePublications=0 originalsUnchanged=true stagedCopiesCleaned=true root={root}");

        void Check(bool value,string why){if(!value)throw new InvalidDataException(why);checks++;}
        void EmptyAndRemove(string output)
        {Check(Directory.Exists(output)&&!Directory.EnumerateFileSystemEntries(output).Any(),"Failed/cancelled F5 stage contains no final artifacts or leaked private image.");Directory.Delete(output);}
        async Task Reject(Func<Task<NativeJapaneseEggResearchPublication>> run,string text)
        {try{await run();}catch(Exception e)when((e is IOException or InvalidDataException or InvalidOperationException)&&e.Message.Contains(text,StringComparison.OrdinalIgnoreCase)){checks++;return;}throw new InvalidDataException("Expected exact F5 failure: "+text);}
        async Task Cancel(Func<Task<NativeJapaneseEggResearchPublication>> run)
        {try{await run();}catch(OperationCanceledException){checks++;return;}throw new InvalidDataException("Expected F5 cancellation.");}
    }
    private static string Hash(string file)
    {using FileStream stream=new(file,FileMode.Open,FileAccess.Read,FileShare.Read);return Convert.ToHexStringLower(SHA256.HashData(stream));}
}
