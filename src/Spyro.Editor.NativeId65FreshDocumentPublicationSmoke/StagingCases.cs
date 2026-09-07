using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

internal static class StagingCases
{
    internal static async Task Run(string original, string cue, string workspace)
    {
        string root = Path.GetFullPath(workspace);
        if (Directory.Exists(root) || File.Exists(root) || new FileInfo(root).LinkTarget != null || new DirectoryInfo(root).LinkTarget != null)
            throw new IOException("Fault workspace must be absent and not linked.");
        string before = SourceCases.HashFile(original), cueBefore = SourceCases.HashFile(cue);
        var issued = await NativeId65FreshDocumentCompilation.CaptureAsync(original, SourceCases.Intent);
        var originalPlan = await NativeId65FreshDocumentResearchPlan.CaptureAsync(original, cue, issued);
        Directory.CreateDirectory(root);
        string privateCue = Path.Combine(root, "private-source.cue");
        byte[] privateCueBytes = DiscCueSheet.TextEncoding.GetBytes($"FILE \"{Path.GetFullPath(original)}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        File.WriteAllBytes(privateCue, privateCueBytes);
        var plan = await NativeId65FreshDocumentResearchPlan.CaptureAsync(original, privateCue, issued);
        int checks = 0;
        foreach (var fault in new[] { NativeId65FreshDocumentResearchFault.AfterFirstSector,
            NativeId65FreshDocumentResearchFault.AfterImageWrite, NativeId65FreshDocumentResearchFault.BeforePublication,
            NativeId65FreshDocumentResearchFault.AfterSavedGuard })
        {
            var context = SourceCases.SaveContext(Path.Combine(root, "project-" + fault), plan);
            string output = Path.Combine(root, "output-" + fault);
            bool reached = false;
            await Refuse(() => NativeId65FreshDocumentResearchPublisher.PublishForSmokeAsync(context, output, fault, (point, image) =>
            {
                if (point != fault) return;
                reached = true;
                if (point == NativeId65FreshDocumentResearchFault.BeforePublication)
                {
                    var stage = NativeId65FreshDocumentResearchPublisher.VerifyAsync(context, Path.GetDirectoryName(image)!).GetAwaiter().GetResult();
                    Check(stage.ImageSha256 == SourceCases.Image && stage.PlanSha256 == plan.PlanSha256, "complete stage uses actual generic readback and receipt");
                }
                if (point == NativeId65FreshDocumentResearchFault.AfterSavedGuard)
                {
                    string lockPath = Path.Combine(context.Snapshot.RootPath, ".native-id65-fresh-document.writer.lock");
                    Check(File.Exists(lockPath), "real Store guard is held at final precommit seam");
                    bool refused = false;
                    try { NativeId65FreshDocumentStore.Save(context.Snapshot, context.Snapshot.Intent!); }
                    catch (IOException) { refused = true; }
                    Check(refused, "cooperating Save cannot interleave with final commit");
                }
            }), "Deliberate");
            Check(reached, "requested real checkpoint reached"); Empty(output);
            Check(!File.Exists(Path.Combine(context.Snapshot.RootPath, ".native-id65-fresh-document.writer.lock")), "owned project guard released on forced fault");
        }

        foreach (string changed in new[] { "document", "manifest", "foreign-project-lease", "cancel-after-guard", "receipt", "parity", "source-cue" })
        {
            var context = SourceCases.SaveContext(Path.Combine(root, "project-" + changed), plan);
            string output = Path.Combine(root, "output-" + changed);
            using var token = new CancellationTokenSource();
            bool reached = false;
            try
            {
                Func<Task<NativeId65FreshDiscResearchPublication>> action = () => NativeId65FreshDocumentResearchPublisher.PublishForSmokeAsync(
                    context, output, NativeId65FreshDocumentResearchFault.AfterSavedGuard, (point, image) =>
                    {
                        var at = changed == "parity" ? NativeId65FreshDocumentResearchFault.AfterImageWrite :
                            changed == "cancel-after-guard" ? NativeId65FreshDocumentResearchFault.AfterSavedGuard : NativeId65FreshDocumentResearchFault.BeforePublication;
                        if (point != at) return;
                        reached = true;
                        switch (changed)
                        {
                            case "document": File.AppendAllText(context.Snapshot.Path, " "); break;
                            case "manifest": File.AppendAllText(new EditorProjectLayout(context.Snapshot.RootPath, context.Snapshot.ProjectId).ManifestPath, " "); break;
                            case "foreign-project-lease": File.WriteAllText(Path.Combine(context.Snapshot.RootPath, ".native-id65-fresh-document.writer.lock"), "foreign owner"); break;
                            case "cancel-after-guard": token.Cancel(); break;
                            case "source-cue": File.AppendAllText(privateCue, "PREGAP 00:01:00\n"); break;
                            case "receipt":
                                string path = Path.Combine(Path.GetDirectoryName(image)!, "native-id65-fresh-document-research.receipt.json");
                                byte[] bytes = File.ReadAllBytes(path); bytes[0] ^= 1; File.WriteAllBytes(path, bytes); break;
                            case "parity":
                                using (var bytesFile = new FileStream(image, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                                { bytesFile.Position = 30L * 2352 + 2072; int b = bytesFile.ReadByte(); bytesFile.Position--; bytesFile.WriteByte((byte)(b ^ 1)); bytesFile.Flush(true); }
                                break;
                        }
                    }, token.Token);
                if (changed == "cancel-after-guard")
                {
                    bool canceled = false; try { await action(); } catch (OperationCanceledException) { canceled = true; }
                    Check(canceled, "after-guard cancellation is still precommit, not a deliberate fault or success");
                }
                else await Refuse(action, changed is "document" or "manifest" ? "changed since capture" :
                    changed == "receipt" ? "companion" : changed == "source-cue" ? "source CUE" : changed == "parity" ? "readback" : null);
            }
            finally { if (changed == "source-cue") File.WriteAllBytes(privateCue, privateCueBytes); }
            Check(reached, "late attack reached actual stage/guard"); Empty(output);
            string lockPath = Path.Combine(context.Snapshot.RootPath, ".native-id65-fresh-document.writer.lock");
            Check(changed == "foreign-project-lease" ? File.ReadAllText(lockPath) == "foreign owner" : !File.Exists(lockPath),
                "owned lease cleaned; foreign project lock preserved");
        }

        var retained = SourceCases.SaveContext(Path.Combine(root, "project-retained"), plan);
        string retainedOutput = Path.Combine(root, "output-retained"), retainedStage = "";
        await Refuse(() => NativeId65FreshDocumentResearchPublisher.PublishForSmokeAsync(retained, retainedOutput,
            NativeId65FreshDocumentResearchFault.AfterFirstSector, (point, image) =>
            {
                if (point != NativeId65FreshDocumentResearchFault.AfterFirstSector) return;
                retainedStage = Path.GetDirectoryName(image)!;
                File.WriteAllText(Path.Combine(retainedStage, "foreign.txt"), "preserve foreign stage owner");
            }), "Deliberate");
        Check(File.ReadAllText(Path.Combine(retainedStage, "foreign.txt")) == "preserve foreign stage owner" &&
            File.Exists(Path.Combine(retainedOutput, ".native-id65-fresh-document-research.writer.lock")), "unknown staged owner and output lease retained");
        Check(!File.Exists(Path.Combine(retainedStage, "native-id65-fresh-document-research.bin")), "only known partial image removed");
        await originalPlan.RequireFreshAsync(); await plan.RequireFreshAsync();
        Check(SourceCases.HashFile(original) == before && SourceCases.HashFile(cue) == cueBefore, "original inputs unchanged");
        Console.WriteLine($"NATIVE_ID65_FRESH_DOCUMENT_PUBLICATION_STAGING_OK checks={checks} successfulPublication=false");

        void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); checks++; }
        void Empty(string output) => Check(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "ordinary failed stage leaves no output");
        async Task Refuse(Func<Task<NativeId65FreshDiscResearchPublication>> action, string? fragment)
        {
            try { await action(); }
            catch (Exception error) when (error is IOException or InvalidDataException)
            {
                if (error is NativeId65FreshDocumentResearchCommittedException ||
                    (fragment == null ? error.Message.Contains("Deliberate") : !error.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Wrong precommit refusal: " + error.Message, error);
                checks++; return;
            }
            throw new InvalidDataException("Unexpected successful publication in forced-precommit mode");
        }
    }
}
