using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

// Explicit opt-in only. Every image remains private disposable staging;
// every exercise fails/cancels before the new-only publication transaction.
internal static class PublicationFaults
{
    internal static async Task Run(NativeCollectibleCardLoadResearchPlan plan,string directory,string expectedImage)
    {
        string root = Path.GetFullPath(directory);
        if (Directory.Exists(root) || File.Exists(root)) throw new IOException("Fault exercises require a NEW exact directory.");
        Directory.CreateDirectory(root); int checks = 0; string[] names = NativeCollectibleCardLoadResearchPlan.OutputNames;
        foreach (var point in new[] { NativeWizardResearchFault.AfterImageWrite,NativeWizardResearchFault.BeforePublication })
        {
            string output = Path.Combine(root,"fault-" + point);
            await Reject(() => NativeCollectibleCardLoadResearchPublisher.PublishForSmokeAsync(plan,output,point,(at,image) =>
            {
                Check(Path.GetFileName(image) == NativeCollectibleCardLoadResearchPlan.ImageName &&
                    Path.GetFileName(Path.GetDirectoryName(image)!).StartsWith(".native-collectible-card-load-stage-",StringComparison.Ordinal),
                    "Closed staging names.");
                if (at != NativeWizardResearchFault.BeforePublication) return;
                string stage = Path.GetDirectoryName(image)!; string[] hashes = names.Select(n => Hash(Path.Combine(stage,n))).ToArray();
                var verified = NativeCollectibleCardLoadResearchPublisher.VerifyAsync(plan,stage).GetAwaiter().GetResult();
                Check(verified.ImageSha256 == expectedImage && verified.PlanSha256 == plan.PlanSha256 &&
                    verified.ResearchOnly && !verified.ReadyStateIssued && !verified.CardWrites && !verified.RuntimeVerified,
                    "Complete staged physical image and typed research-only result.");
                Reject(() => NativeCollectibleCardLoadResearchPublisher.PublishAsync(plan,stage),"NEW directory").GetAwaiter().GetResult();
                Check(names.Select(n => Hash(Path.Combine(stage,n))).SequenceEqual(hashes),"Verification and overwrite refusal preserve complete staged set.");
            }),"Deliberate"); Clean(output);
        }
        foreach (var point in new[] { NativeWizardResearchFault.AfterImageWrite,NativeWizardResearchFault.BeforePublication })
        {
            string output = Path.Combine(root,"cancel-" + point); using var cancel = new CancellationTokenSource();
            await Cancel(() => NativeCollectibleCardLoadResearchPublisher.PublishForSmokeAsync(plan,output,NativeWizardResearchFault.None,
                (at,_) => { if (at == point) cancel.Cancel(); },cancel.Token)); Clean(output);
        }
        string early = Path.Combine(root,"cancel-before-io");
        using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel(); await Cancel(() => NativeCollectibleCardLoadResearchPublisher.PublishAsync(plan,early,cancel.Token));
            Check(!Directory.Exists(early),"Pre-cancel creates no output owner.");
        }
        foreach (var change in new[] { (Name:"tampered-complement",Offset:30L*2352+2072),
            (Name:"tampered-overlay",Offset:(37L+0x5B800/0x800)*2352+24+0x100),
            (Name:"tampered-executable",Offset:(long)plan.RelocatedExecutableLba*2352+24+0x10) })
        {
            string output = Path.Combine(root,change.Name);
            await Reject(() => NativeCollectibleCardLoadResearchPublisher.PublishForSmokeAsync(plan,output,NativeWizardResearchFault.None,(at,image) =>
            {
                if (at != NativeWizardResearchFault.AfterImageWrite) return;
                using var file = new FileStream(image,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
                file.Position = change.Offset; int before = file.ReadByte(); file.Position--; file.WriteByte((byte)(before^1)); file.Flush(true);
            }),"physical readback"); Clean(output);
        }
        foreach (string name in new[] { NativeCollectibleCardLoadResearchPlan.CueName,NativeCollectibleCardLoadResearchPlan.ReceiptName })
        {
            string output = Path.Combine(root,name.EndsWith(".cue",StringComparison.Ordinal) ? "tampered-cue" : "tampered-receipt");
            await Reject(() => NativeCollectibleCardLoadResearchPublisher.PublishForSmokeAsync(plan,output,NativeWizardResearchFault.None,(at,image) =>
            {
                if (at != NativeWizardResearchFault.BeforePublication) return;
                string path = Path.Combine(Path.GetDirectoryName(image)!,name);
                using var file = new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
                file.Position = file.Length-1; int before = file.ReadByte(); file.Position--; file.WriteByte((byte)(before^1)); file.Flush(true);
            }),"companion"); Clean(output);
        }
        string occupied = Path.Combine(root,"occupied"); Directory.CreateDirectory(occupied);
        string sentinel = Path.Combine(occupied,"keep.txt"); File.WriteAllText(sentinel,"owned fault fixture");
        await Reject(() => NativeCollectibleCardLoadResearchPublisher.PublishAsync(plan,occupied),"NEW directory");
        Check(File.ReadAllText(sentinel) == "owned fault fixture" && Directory.GetFileSystemEntries(occupied).Length == 1,
            "Occupied target preserves complete existing evidence.");
        string link = Path.Combine(root,"link"); Directory.CreateSymbolicLink(link,occupied);
        try { await Reject(() => NativeCollectibleCardLoadResearchPublisher.PublishAsync(plan,link),"symbolic"); }
        finally { File.Delete(link); }
        File.Delete(sentinel); Directory.Delete(occupied);
        string empty = Path.Combine(root,"empty"); Directory.CreateDirectory(empty);
        await Reject(() => NativeCollectibleCardLoadResearchPublisher.PublishAsync(plan,empty),"NEW directory");
        Check(!Directory.GetFileSystemEntries(empty).Any(),"Even an existing empty target is refused unchanged."); Directory.Delete(empty);
        await Reject(() => NativeCollectibleCardLoadResearchPublisher.PublishAsync(plan,plan.SourceImagePath),"NEW directory");
        await Reject(() => NativeCollectibleCardLoadResearchPublisher.PublishAsync(plan,plan.SourceCuePath),"NEW directory");
        await Reject(() => NativeCollectibleCardLoadResearchPublisher.PublishAsync(plan,Path.GetDirectoryName(plan.SourceImagePath)!),"NEW directory");
        await plan.RequireFreshAsync();
        Check(!Directory.EnumerateFileSystemEntries(root).Any(),"No staged or committed artifacts remain and originals remain exact.");
        Console.WriteLine($"COLLECTIBLE_CARD_LOAD_RESEARCH_PUBLICATION_FAULTS_OK checks={checks} successfulCandidatePublications=0 originalsUnchanged=true stagedCopiesCleaned=true root={root}");

        void Check(bool ok,string why) { if (!ok) throw new InvalidDataException(why); checks++; }
        void Clean(string output)
        {
            Check(Directory.Exists(output) && !Directory.EnumerateFileSystemEntries(output).Any(),"Failure/cancel cleans only invocation-owned staging.");
            Directory.Delete(output);
        }
        async Task Reject(Func<Task<NativeCollectibleCardLoadResearchPublication>> action,string expected)
        {
            try { await action(); }
            catch (Exception e) when ((e is IOException or InvalidDataException or InvalidOperationException) &&
                e.Message.Contains(expected,StringComparison.OrdinalIgnoreCase)) { checks++; return; }
            throw new InvalidDataException("Expected research refusal: " + expected);
        }
        async Task Cancel(Func<Task<NativeCollectibleCardLoadResearchPublication>> action)
        {
            try { await action(); } catch (OperationCanceledException) { checks++; return; }
            throw new InvalidDataException("Expected research cancellation.");
        }
    }
    private static string Hash(string path)
    {
        using var input = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        return Convert.ToHexStringLower(SHA256.HashData(input));
    }
}
