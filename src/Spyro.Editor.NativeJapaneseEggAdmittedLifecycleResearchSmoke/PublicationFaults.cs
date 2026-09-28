using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

internal static class PublicationFaults
{
    internal static async Task Run(NativeJapaneseEggAdmittedLifecycleResearchPlan plan,string directory,string expectedImage)
    {
        string root = Path.GetFullPath(directory);
        if (Directory.Exists(root) || File.Exists(root) || new DirectoryInfo(root).LinkTarget != null)
            throw new IOException("Fault tests require a NEW dedicated directory.");
        Directory.CreateDirectory(root); int checks = 0;
        void Check(bool ok,string why) { if (!ok) throw new Exception(why); checks++; }
        async Task Refuse(Func<Task> action,string why,string? message = null)
        {
            try { await action(); }
            catch (Exception e) when (e is InvalidDataException or IOException or OperationCanceledException)
            { Check(message == null || e.Message.Contains(message,StringComparison.Ordinal),"Actual refusal owner: " + why + ": " + e.Message); return; }
            throw new Exception("Expected refusal: " + why);
        }
        static string HashFile(string path) { using var file = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(file)); }
        foreach (var fault in new[] { NativeWizardResearchFault.AfterImageWrite,NativeWizardResearchFault.BeforePublication })
        {
            string output = Path.Combine(root,fault.ToString()); bool reached = false;
            await Refuse(() => NativeJapaneseEggAdmittedLifecycleResearchPublisher.PublishForSmokeAsync(plan,output,fault,(point,image) =>
            {
                if (point != fault) return; reached = true;
                if (point == NativeWizardResearchFault.BeforePublication)
                {
                    string stage = Path.GetDirectoryName(image)!;
                    var result = NativeJapaneseEggAdmittedLifecycleResearchPublisher.VerifyAsync(plan,stage).GetAwaiter().GetResult();
                    Check(result.ImageSha256 == expectedImage && result.WholeImageReadback && result.GuardedCollectionEmitted &&
                        !result.RuntimeVerified && !result.CardWrites,"Whole staged set exact with guarded research boundaries.");
                    var hashes = Directory.GetFiles(stage).ToDictionary(p => Path.GetFileName(p)!,HashFile);
                    Refuse(() => NativeJapaneseEggAdmittedLifecycleResearchPublisher.PublishAsync(plan,stage),"complete existing set","NEW directory").GetAwaiter().GetResult();
                    Check(Directory.GetFiles(stage).All(p => hashes[Path.GetFileName(p)] == HashFile(p)),"Existing complete staged set unchanged.");
                }
            }),"checkpoint " + fault,"Deliberate research publication fault");
            Check(reached && !Directory.EnumerateFileSystemEntries(output).Any(),"Precommit staging cleaned by unchanged engine.");
        }
        string occupied = Path.Combine(root,"occupied"); Directory.CreateDirectory(occupied);
        string keep = Path.Combine(occupied,"keep.txt"); File.WriteAllText(keep,"preserve occupied output"); string keptHash = HashFile(keep);
        await Refuse(() => NativeJapaneseEggAdmittedLifecycleResearchPublisher.PublishAsync(plan,occupied),"occupied output","NEW directory");
        Check(HashFile(keep) == keptHash && Directory.GetFiles(occupied).Length == 1,"Occupied output preserved.");
        string empty = Path.Combine(root,"existing-empty"); Directory.CreateDirectory(empty);
        await Refuse(() => NativeJapaneseEggAdmittedLifecycleResearchPublisher.PublishAsync(plan,empty),"existing empty output","NEW directory");
        string collision = Path.Combine(root,"transaction-collision");
        await Refuse(() => NativeJapaneseEggAdmittedLifecycleResearchPublisher.PublishForSmokeAsync(plan,collision,NativeWizardResearchFault.None,(point,_) =>
        {
            if (point == NativeWizardResearchFault.BeforePublication)
                File.WriteAllText(Path.Combine(collision,NativeJapaneseEggAdmittedLifecycleResearchPlan.ImageName),"external collision sentinel");
        }),"actual new-file transaction collision");
        Check(File.ReadAllText(Path.Combine(collision,NativeJapaneseEggAdmittedLifecycleResearchPlan.ImageName)) == "external collision sentinel" &&
            Directory.GetFiles(collision).Length == 1 && !Directory.GetDirectories(collision).Any(),"Transaction never replaced foreign collision or published companions.");
        string tampered = Path.Combine(root,"tampered-staging");
        await Refuse(() => NativeJapaneseEggAdmittedLifecycleResearchPublisher.PublishForSmokeAsync(plan,tampered,NativeWizardResearchFault.None,(point,image) =>
        { if (point == NativeWizardResearchFault.BeforePublication) Flip(image,30L*2352+24); }),"outer physical complement tamper");
        Check(!Directory.EnumerateFileSystemEntries(tampered).Any(),"Bad whole-image complement never published.");
        // Only these dedicated exact copies are mutated. Original JP/USA/CUE
        // and all earlier candidates remain read-only throughout the matrix.
        string copies = Path.Combine(root,"source-copies"); Directory.CreateDirectory(copies);
        string jp = Path.Combine(copies,Path.GetFileName(plan.JapaneseSourceImagePath));
        string usa = Path.Combine(copies,Path.GetFileName(plan.SourceImagePath));
        string cue = Path.Combine(copies,Path.GetFileName(plan.SourceCuePath));
        File.Copy(plan.JapaneseSourceImagePath,jp); File.Copy(plan.SourceImagePath,usa); File.Copy(plan.SourceCuePath,cue);
        var stale = await NativeJapaneseEggAdmittedLifecycleResearchPlan.CaptureAsync(jp,usa,cue);
        foreach (var pair in new[] { ("stale-usa",usa),("stale-japan",jp) })
        {
            string output = Path.Combine(root,pair.Item1);
            await Refuse(() => NativeJapaneseEggAdmittedLifecycleResearchPublisher.PublishForSmokeAsync(stale,output,NativeWizardResearchFault.None,(point,_) =>
            { if (point == NativeWizardResearchFault.BeforePublication) Flip(pair.Item2,30L*2352+24); }),pair.Item1);
            Check(!Directory.EnumerateFileSystemEntries(output).Any(),"Stale regional source refused before final artifacts.");
            Flip(pair.Item2,30L*2352+24); await stale.RequireFreshAsync(); checks++;
        }
        File.AppendAllText(cue,"\nREM deliberately stale dedicated test copy\n"); string staleCue = Path.Combine(root,"stale-cue");
        await Refuse(() => NativeJapaneseEggAdmittedLifecycleResearchPublisher.PublishAsync(stale,staleCue),"stale CUE");
        Check(!Directory.Exists(staleCue),"Stale CUE rejected before output creation.");
        using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel(); string output = Path.Combine(root,"cancel-before-io");
            await Refuse(() => NativeJapaneseEggAdmittedLifecycleResearchPublisher.PublishAsync(plan,output,cancel.Token),"cancelled before I/O");
            Check(!Directory.Exists(output),"Cancellation created nothing.");
        }
        await plan.RequireFreshAsync(); checks++;
        Console.WriteLine($"JP_EGG_ADMITTED_RESEARCH_PUBLICATION_FAULTS_OK checks={checks} publishedImages=0 originalSourcesUnchanged=true evidence={root} sourceCopiesRetained={copies}");
    }
    private static void Flip(string path,long at)
    {
        using FileStream file = new(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        file.Position = at; int value = file.ReadByte(); if (value < 0) throw new Exception("Mutation leaves dedicated test copy");
        file.Position = at; file.WriteByte((byte)(value^1)); file.Flush(true);
    }
}
