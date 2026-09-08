using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using static Inspection;

internal static class BindingCases
{
    internal static void Free()
    {
        var binding = Type("NativeId65FreshResearchPublicationBinding");
        Check(binding.IsSealed && binding.GetConstructors().Length == 0, "Current closed union baseline, no public caller construction.");
        foreach (string factory in new[] { "FromStarter", "FromEdited", "FromSaved", "FromWizard" })
            Refuse<ArgumentNullException>(() => Call(null, binding, factory, new object?[] { null }), "Existing synchronous binding null guard.");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Func<Task>[] calls = [
            () => NativeId65FreshDiscResearchCandidatePublisher.PublishAsync(null!, "\0", cancel.Token),
            () => NativeId65FreshDiscResearchCandidatePublisher.VerifyAsync(null!, "\0", cancel.Token),
            () => NativeId65FreshEditedDocumentResearchPublisher.PublishAsync(null!, "\0", cancel.Token),
            () => NativeId65FreshEditedDocumentResearchPublisher.VerifyAsync(null!, "\0", cancel.Token),
            () => NativeId65FreshDocumentResearchPublisher.PublishAsync(null!, "\0", cancel.Token),
            () => NativeId65FreshDocumentResearchPublisher.VerifyAsync(null!, "\0", cancel.Token),
            () => NativeId65FreshWizardResearchCandidatePublisher.PublishAsync(null!, "\0", cancel.Token),
            () => NativeId65FreshWizardResearchCandidatePublisher.VerifyAsync(null!, "\0", cancel.Token)];
        foreach (var call in calls)
        {
            Task task = call(); // Any synchronous throw is a different API timing failure.
            Check(task.IsFaulted, "Existing public null wrapper returns a faulted Task.");
            Refuse<ArgumentNullException>(() => task.GetAwaiter().GetResult(), "Null binding beats invalid path/cancellation in old wrapper.");
        }
    }

    internal static async Task Source(NativeId65FreshDiscResearchCandidatePlan starter,
        NativeId65FreshEditedDocumentResearchPlan edited, NativeId65FreshSavedDocumentExportContext saved,
        NativeId65FreshWizardResearchCandidatePlan wizard, NativeId65FreshWizardResearchCandidatePlan separated)
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var cases = new[]
        {
            new Mapping("FromStarter", starter, starter, "Fresh LAB", "native-id65-fresh-lab-track19-r01", ".native-id65-fresh.writer.lock", ".native-id65-fresh-stage-", false, true, 1253),
            new Mapping("FromEdited", edited, edited, "Edited fresh LAB", "native-id65-fresh-edited-landing-track02-r01", ".native-id65-fresh-edited.writer.lock", ".native-id65-fresh-edited-stage-", false, false, 1253),
            new Mapping("FromSaved", saved, saved.Plan, "Saved fresh LAB", "native-id65-fresh-document-research", ".native-id65-fresh-document-research.writer.lock", ".native-id65-fresh-document-research-stage-", true, false, 1253),
            new Mapping("FromWizard", wizard, wizard, "Fresh Wizard", "native-id65-fresh-wizard-p1-n32-r01", ".native-id65-fresh-wizard.writer.lock", ".native-id65-fresh-wizard-stage-", true, false, 1290),
            new Mapping("FromWizard", separated, separated, "Fresh Wizard", "native-id65-fresh-wizard-separated-p1-n32-r01", ".native-id65-fresh-wizard.writer.lock", ".native-id65-fresh-wizard-stage-", true, false, 1290)
        };
        foreach (var row in cases)
        {
            object b = Call(null, Type("NativeId65FreshResearchPublicationBinding"), row.Factory, row.Owner)!;
            ExtractionCases.ActualOwner(b, row.Owner);
            foreach (string name in new[] { "SourceImagePath", "SourceCuePath", "PlanSha256" })
                Check(Get<string>(b, name) == Get<string>(row.Plan, name), "Binding forwards actual " + name + ": " + row.Factory);
            Check(Get<string>(b, "Label") == row.Label && Get<string>(b, "LeaseName") == row.Lease &&
                Get<string>(b, "StagePrefix") == row.Stage, "Exact existing labels and private names: " + row.Factory);
            string[] names = [row.Stem + ".bin", row.Stem + ".receipt.json", row.Stem + ".cue"];
            Check(Get<string>(b, "ImageName") == names[0] && Get<string>(b, "ReceiptName") == names[1] &&
                Get<string>(b, "CueName") == names[2] && Get<string[]>(b, "OutputNames").SequenceEqual(names), "Exact artifact names/order, CUE last.");
            var detached = Get<string[]>(b, "OutputNames"); detached[0] = "not-owned";
            Check(Get<string[]>(b, "OutputNames").SequenceEqual(names), "Returned names cannot mutate binding.");
            Check(Get<bool>(b, "TracksCommittedFailure") == row.Typed &&
                ReferenceEquals(Get<object?>(b, "SavedContext"), row.Owner is NativeId65FreshSavedDocumentExportContext ? row.Owner : null) &&
                ReferenceEquals(Get<object?>(b, "WizardPlan"), row.Owner is NativeId65FreshWizardResearchCandidatePlan ? row.Owner : null),
                "Old committed-policy/context mappings are actual, not caller flags.");
            var token = (CancellationToken)Call(b, "PostCommitToken", cancelled.Token)!;
            Check(row.Cancellable ? token == cancelled.Token : token == CancellationToken.None, "Historical starter late-cancel distinction remains exact.");
            byte[] cue = (byte[])Call(b, "CopyCueBytes")!, directCue = (byte[])Call(row.Plan, "CopyCueBytes")!;
            Check(cue.SequenceEqual(directCue) && Encoding.UTF8.GetString(cue) == $"FILE \"{names[0]}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n",
                "Exact CUE bytes and direct-plan mapping.");
            string imageHash = Get<string>(row.Plan, "ExpectedImageSha256");
            var verification = new NativeId65FreshDiscVerification(imageHash, row.Changes); // Prospective serializer input, not physical readback.
            byte[] receipt = (byte[])Call(b, "BuildReceipt", verification)!;
            byte[] direct = row.Owner is NativeId65FreshSavedDocumentExportContext context
                ? (byte[])Call(null, typeof(NativeId65FreshDocumentResearchPublisher), "BuildReceipt", context, verification)!
                : (byte[])Call(row.Plan, "BuildReceipt", verification)!;
            Check(receipt.SequenceEqual(direct), "Full receipt byte identity with existing actual serializer; no normalized replacement.");
            HistoricalGoldens(row, directCue, direct);
            string receiptHash = Hash(receipt); receipt[0] ^= 1; cue[0] ^= 1;
            Check(Hash((byte[])Call(b, "BuildReceipt", verification)!) == receiptHash && ((byte[])Call(b, "CopyCueBytes")!).SequenceEqual(directCue),
                "Binding companions are defensive snapshots.");
            using var json = JsonDocument.Parse(direct);
            var root = json.RootElement;
            Check(root.GetProperty("planSha256").GetString() == Get<string>(row.Plan, "PlanSha256") &&
                root.GetProperty("imageSha256").GetString() == imageHash && root.GetProperty("changedRawSectors").GetInt32() == row.Changes &&
                root.GetProperty("sourceImagePath").GetString() == Get<string>(row.Plan, "SourceImagePath") &&
                !root.GetProperty("runtimeVerified").GetBoolean() && !root.GetProperty("normalCreateBinApproved").GetBoolean(),
                "Actual source and false approval receipt boundaries retained.");
            if (row.Owner is NativeId65FreshSavedDocumentExportContext actual)
            {
                var p = root.GetProperty("savedProject");
                Check(p.GetProperty("rootPath").GetString() == actual.Snapshot.RootPath &&
                    p.GetProperty("manifestSha256").GetString() == actual.Snapshot.ManifestSha256 &&
                    p.GetProperty("documentSha256").GetString() == actual.Snapshot.ContentSha256,
                    "Saved receipt binds actual tiny Store snapshot, not a mocked context.");
            }
            Refuse<InvalidDataException>(() => Call(b, "BuildReceipt", verification with { ChangedSectors = 0 }), "Wrong actual readback count.");
            Refuse<InvalidDataException>(() => Call(b, "BuildReceipt", verification with { OutputSha256 = new string('0', 64) }), "Wrong actual readback image.");
            await RefuseAsync<OperationCanceledException>(() => (Task)Call(b, "RequireFreshAsync", cancelled.Token)!, "Binding forwards cancellation before source open.");
            await (Task)Call(b, "RequireFreshAsync", CancellationToken.None)!;
            foreach (string method in new[] { "PublishDocumentPostcommitForSmokeAsync", "PublishWizardPostcommitForSmokeAsync", "PublishWizardUnconfirmedForSmokeAsync" })
            {
                bool ownKind = method.StartsWith("PublishDocument", StringComparison.Ordinal)
                    ? row.Owner is NativeId65FreshSavedDocumentExportContext : row.Owner is NativeId65FreshWizardResearchCandidatePlan;
                Func<Task> action = () => (Task)Call(null, Type("NativeId65FreshResearchPublicationEngine"), method, b, "must-not-create", null, CancellationToken.None)!;
                if (ownKind) await RefuseAsync<ArgumentNullException>(action, "Actual correct kind still needs explicit smoke callback.");
                else await RefuseAsync<InvalidDataException>(action, "Actual wrong kind refuses before path or callback.");
            }
            Console.WriteLine($"BINDING_BASELINE factory={row.Factory} stem={row.Stem} plan={Get<string>(row.Plan, "PlanSha256")} cue={Hash(directCue)} receipt={receiptHash} receiptBytes={direct.Length} prospectiveOnly=true");
        }
    }

    private static void HistoricalGoldens(Mapping row, byte[] cue, byte[] receipt)
    {
        // These historical full Plan/receipt bytes include the reviewed source
        // paths. A byte-identical copied source is not this path-specific fixture.
        const string source = "/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA)";
        Check(Get<string>(row.Plan, "SourceImagePath") == source + ".bin" && Get<string>(row.Plan, "SourceCuePath") == source + ".cue",
            "Historical receipt fixture uses the exact reviewed source paths.");
        (string Plan, string Cue, string? Receipt, int Bytes) golden = row.Stem switch
        {
            "native-id65-fresh-lab-track19-r01" => ("29d7debbfca1c48019449cdb9fd35f7bd9be37330534dc34eefb97fcab71f84d", "7e2bd7f0dcf23af6fc7ed5e7be56accf7b7ee354b335c238bd446c863f13023f", "fd4ebbf408b06e5be3d3ff83c4926362856236b70feda4ef2095fd59587eabbd", 3455),
            "native-id65-fresh-edited-landing-track02-r01" => ("fba3e61a3f93b9dff27d9a5304eb746b7c2ef9aa95a71cfeda6adaee62b571c2", "1fcb9bade2f3c9c59b6e6d7b57bd8a2bd0773534d151374d62fcb2ce442e663c", "3b545c4dd0e2102586e9f520dec96fda3ea8e84a088003d5b7a7a5ef701373d7", 4027),
            "native-id65-fresh-document-research" => ("046febda9b44fd8c4fb2bf2a4cc0ecffecb8202a3f37b9f4871b892916ac6f8d", "f272d1a83b7962c1617d6b0d25c0786232b3e951d16d8d2675f87830ffbca1e8", null, 0),
            "native-id65-fresh-wizard-p1-n32-r01" => ("3278bedaf4d24a564bfc6b3fc4554a959bf520bc51608bd53b73c8a112a1ac8b", "78c40516cbfe54286b839453d8ccf5786ac685d23a69623e6e51d82106238a6e", "af41aa8b0af10504c626f76cbaa2446e2207486551ce6ba8766794261a63b63e", 4877),
            "native-id65-fresh-wizard-separated-p1-n32-r01" => ("c4e638fd34ad6a60165ead6bbe32c94c2bc7ce09c39dca32b82b8083d533e73a", "572af87a5fef9497fa32cab6588cdc87f61304c430dfc5e58c8dd6aae070c556", "7c590c9d1454b9f22b51c643084a6f49220a78c49cc2e657983d9030d1a1932d", 5875),
            _ => throw new InvalidDataException("Unreviewed historical binding fixture.")
        };
        Check(Get<string>(row.Plan, "PlanSha256") == golden.Plan && Hash(cue) == golden.Cue, "Prior deterministic Plan/CUE golden, not self-equality.");
        if (golden.Receipt != null)
            Check(receipt.Length == golden.Bytes && Hash(receipt) == golden.Receipt, "Prior deterministic complete receipt golden.");
        // Saved-context receipt also owns a fresh random project root; its full
        // bytes are compared to the actual serializer and actual saved fields.
    }

    private sealed record Mapping(string Factory, object Owner, object Plan, string Label, string Stem, string Lease,
        string Stage, bool Typed, bool Cancellable, int Changes);
}
