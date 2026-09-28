using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

internal static class PublicationFaults
{
    internal static async Task Run(NativeCollectibleCardAdmissionResearchPlan plan,string directory,string expectedImage)
    {
        string root = Path.GetFullPath(directory);
        if (Directory.Exists(root) || File.Exists(root) || new DirectoryInfo(root).LinkTarget != null)
            throw new IOException("Fault tests require a NEW dedicated directory.");
        Directory.CreateDirectory(root); int checks = 0;
        void Check(bool ok,string why) { if (!ok) throw new Exception(why); checks++; }
        async Task Refuse(Func<Task> action,string why,string? message = null)
        {
            try { await action(); }
            catch (Exception e) when (e is IOException or InvalidDataException or OperationCanceledException)
            {
                Check(message == null || e.Message.Contains(message,StringComparison.Ordinal),"Actual refusal owner: " + why + ": " + e.Message);
                return;
            }
            throw new Exception("Expected refusal: " + why);
        }
        static string HashFile(string path) { using var file = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(file)); }
        foreach (var fault in new[] { NativeWizardResearchFault.AfterImageWrite,NativeWizardResearchFault.BeforePublication })
        {
            string output = Path.Combine(root,fault.ToString()); bool reached = false;
            await Refuse(() => NativeCollectibleCardAdmissionResearchPublisher.PublishForSmokeAsync(plan,output,fault,(point,image) =>
            {
                if (point != fault) return; reached = true;
                Check(Path.GetFileName(image) == NativeCollectibleCardAdmissionResearchPlan.ImageName,"Isolated staged image name.");
                if (point == NativeWizardResearchFault.BeforePublication)
                {
                    string stage = Path.GetDirectoryName(image)!;
                    var readback = NativeCollectibleCardAdmissionResearchPublisher.VerifyAsync(plan,stage).GetAwaiter().GetResult();
                    Check(readback.ImageSha256 == expectedImage && readback.WholeImageReadback && readback.GuardedReadyAdmissionEmitted &&
                        !readback.ReadyStateIssued && !readback.CardWrites && !readback.RuntimeVerified,"Complete staging readback with honest boundaries.");
                    var hashes = Directory.GetFiles(stage).ToDictionary(p => Path.GetFileName(p)!,p => HashFile(p));
                    Refuse(() => NativeCollectibleCardAdmissionResearchPublisher.PublishAsync(plan,stage),"even complete staged set cannot be republished","NEW directory").GetAwaiter().GetResult();
                    Check(Directory.GetFiles(stage).All(p => hashes[Path.GetFileName(p)] == HashFile(p)),"Existing complete set was not changed.");
                }
            }),"fault checkpoint " + fault,"Deliberate research publication fault");
            Check(reached && Directory.Exists(output) && !Directory.EnumerateFileSystemEntries(output).Any(),"Shared transaction cleaned only its precommit staging.");
        }
        string occupied = Path.Combine(root,"occupied"); Directory.CreateDirectory(occupied);
        string sentinel = Path.Combine(occupied,"keep.txt"); File.WriteAllText(sentinel,"preserve occupied output"); string sentinelHash = HashFile(sentinel);
        await Refuse(() => NativeCollectibleCardAdmissionResearchPublisher.PublishAsync(plan,occupied),"occupied output","NEW directory");
        Check(HashFile(sentinel) == sentinelHash && Directory.GetFiles(occupied).Length == 1,"Occupied output preserved.");
        string empty = Path.Combine(root,"existing-empty"); Directory.CreateDirectory(empty);
        await Refuse(() => NativeCollectibleCardAdmissionResearchPublisher.PublishAsync(plan,empty),"existing empty output","NEW directory");
        string collision = Path.Combine(root,"transaction-collision");
        await Refuse(() => NativeCollectibleCardAdmissionResearchPublisher.PublishForSmokeAsync(plan,collision,NativeWizardResearchFault.None,(point,_) =>
        {
            if (point == NativeWizardResearchFault.BeforePublication)
                File.WriteAllText(Path.Combine(collision,NativeCollectibleCardAdmissionResearchPlan.ImageName),"external collision sentinel");
        }),"actual PublishNew collision");
        Check(File.ReadAllText(Path.Combine(collision,NativeCollectibleCardAdmissionResearchPlan.ImageName)) == "external collision sentinel" &&
            Directory.GetFiles(collision).Length == 1 && !Directory.GetDirectories(collision).Any(),"Collision preserved without replacing it or leaving new companions.");
        string tampered = Path.Combine(root,"tampered-staging");
        await Refuse(() => NativeCollectibleCardAdmissionResearchPublisher.PublishForSmokeAsync(plan,tampered,NativeWizardResearchFault.None,(point,image) =>
        {
            if (point != NativeWizardResearchFault.BeforePublication) return;
            using FileStream file = new(image,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
            long at = 30L*2352+24; file.Position = at; int before = file.ReadByte(); file.Position = at; file.WriteByte((byte)(before^1)); file.Flush(true);
        }),"complete outside-owner sector readback");
        Check(!Directory.EnumerateFileSystemEntries(tampered).Any(),"Tampered complement refused before publication.");
        // Only a dedicated exact copy is ever mutated. Both source identities
        // are captured before the fault; this exercises the real freshness gate.
        string sourceCopy = Path.Combine(root,"source-copy"); Directory.CreateDirectory(sourceCopy);
        string copiedBin = Path.Combine(sourceCopy,Path.GetFileName(plan.SourceImagePath));
        string copiedCue = Path.Combine(sourceCopy,Path.GetFileName(plan.SourceCuePath));
        File.Copy(plan.SourceImagePath,copiedBin); File.Copy(plan.SourceCuePath,copiedCue);
        var stale = await NativeCollectibleCardAdmissionResearchPlan.CaptureAsync(copiedBin,copiedCue);
        string staleOutput = Path.Combine(root,"stale-source"); long mutationAt = 30L*2352+24; byte savedByte = 0;
        await Refuse(() => NativeCollectibleCardAdmissionResearchPublisher.PublishForSmokeAsync(stale,staleOutput,NativeWizardResearchFault.None,(point,_) =>
        {
            if (point != NativeWizardResearchFault.BeforePublication) return;
            using FileStream file = new(copiedBin,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
            file.Position = mutationAt; savedByte = (byte)file.ReadByte(); file.Position = mutationAt; file.WriteByte((byte)(savedByte^1)); file.Flush(true);
        }),"source changed after capture and complete staging","source image changed");
        Check(!Directory.EnumerateFileSystemEntries(staleOutput).Any(),"Stale source cannot publish any final artifact.");
        using (FileStream file = new(copiedBin,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        { file.Position = mutationAt; file.WriteByte(savedByte); file.Flush(true); }
        await stale.RequireFreshAsync(); checks++;
        File.AppendAllText(copiedCue,"\nREM deliberately stale dedicated test copy\n");
        string staleCueOutput = Path.Combine(root,"stale-cue");
        await Refuse(() => NativeCollectibleCardAdmissionResearchPublisher.PublishAsync(stale,staleCueOutput),"stale source CUE");
        Check(!Directory.Exists(staleCueOutput),"Stale CUE refused before creating destination.");
        using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel(); string output = Path.Combine(root,"cancel-before-io");
            await Refuse(() => NativeCollectibleCardAdmissionResearchPublisher.PublishAsync(plan,output,cancel.Token),"cancel before I/O");
            Check(!Directory.Exists(output),"Cancelled publication created nothing.");
        }
        await plan.RequireFreshAsync(); checks++;
        Console.WriteLine($"COLLECTIBLE_CARD_ADMISSION_RESEARCH_PUBLICATION_FAULTS_OK checks={checks} publishedImages=0 originalSourcesUnchanged=true evidence={root} copiedSourceRetained={sourceCopy}");
    }
}
