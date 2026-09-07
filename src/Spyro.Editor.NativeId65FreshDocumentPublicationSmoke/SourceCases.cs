using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

internal static class SourceCases
{
    internal static readonly NativeId65FreshDocumentIntent Intent = NativeId65FreshDocumentIntent.Create(
        8192, 8192, 8704, 2048, 2048, 9216, 9216, 96, new(72, 132, 120), new(44, 66, 100), 2);
    internal const string Image = "ecc8f61cc16bf86a2f5117b327c49aede18f5c1e6a602c0e67f2ba58565aed16";
    internal static int Checks { get; private set; }

    internal static async Task Run(string original, string cue)
    {
        string before = HashFile(original), cueBefore = HashFile(cue);
        string root = Directory.CreateTempSubdirectory("fresh-document-publication-source-").FullName;
        try
        {
            var issued = await NativeId65FreshDocumentCompilation.CaptureAsync(original, Intent);
            var plan = await NativeId65FreshDocumentResearchPlan.CaptureAsync(original, cue, issued);
            var context = SaveContext(Path.Combine(root, "project"), plan);
            Check(context.Plan.ExpectedImageSha256 == Image, "independent edited image remains exact under distinct generic receipt");
            var verified = new NativeId65FreshDiscVerification(plan.ExpectedImageSha256, plan.ExpectedChangedRawSectors);
            byte[] receipt = NativeId65FreshDocumentResearchPublisher.BuildReceiptForSmoke(context, verified);
            using var json = JsonDocument.Parse(receipt);
            JsonElement value = json.RootElement;
            Check(value.GetProperty("profile").GetString() == "native-id65-fresh-saved-document-research-v1" &&
                value.GetProperty("planSha256").GetString() == plan.PlanSha256, "distinct profile retains source-neutral plan identity");
            Check(value.GetProperty("canonicalDocumentJson").GetString() == Encoding.UTF8.GetString(plan.CopyCanonicalDocument()) &&
                value.GetProperty("canonicalDocumentSha256").GetString() == plan.CanonicalDocumentSha256,
                "receipt includes exact immutable canonical document and its digest");
            JsonElement saved = value.GetProperty("savedProject");
            Check(saved.GetProperty("rootPath").GetString() == context.Snapshot.RootPath &&
                saved.GetProperty("projectId").GetString() == context.Snapshot.ProjectId &&
                saved.GetProperty("manifestSha256").GetString() == context.Snapshot.ManifestSha256 &&
                saved.GetProperty("documentSha256").GetString() == context.Snapshot.ContentSha256,
                "receipt binds actual captured raw project/document lineage, not caller metadata");
            Check(value.GetProperty("imageSha256").GetString() == Image && value.GetProperty("changedRawSectors").GetInt32() == 1253 &&
                value.GetProperty("imageByteLength").GetInt64() == 661547040 &&
                !value.GetProperty("runtimeVerified").GetBoolean() && !value.GetProperty("normalCreateBinApproved").GetBoolean(),
                "derived physical receipt is bounded research evidence, not runtime acceptance");
            Check(receipt.SequenceEqual(NativeId65FreshDocumentResearchPublisher.BuildReceiptForSmoke(context, verified)), "deterministic receipt");
            byte[] mutable = receipt.ToArray(); Array.Fill(mutable, (byte)0);
            Check(receipt.SequenceEqual(NativeId65FreshDocumentResearchPublisher.BuildReceiptForSmoke(context, verified)), "receipt is detached");
            Reject<InvalidDataException>(() => NativeId65FreshDocumentResearchPublisher.BuildReceiptForSmoke(context, verified with { ChangedSectors = 0 }));
            Reject<InvalidDataException>(() => NativeId65FreshDocumentResearchPublisher.BuildReceiptForSmoke(context, verified with { OutputSha256 = new string('0', 64) }));
            var second = SaveContext(Path.Combine(root, "other-project"), plan);
            Check(!receipt.SequenceEqual(NativeId65FreshDocumentResearchPublisher.BuildReceiptForSmoke(second, verified)) &&
                second.Plan.PlanSha256 == plan.PlanSha256, "source-neutral Plan stays exact while actual project receipt differs");

            string missing = Path.Combine(root, "missing-output");
            await RejectAsync<InvalidDataException>(() => NativeId65FreshDocumentResearchPublisher.VerifyAsync(context, missing));
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await RejectAsync<OperationCanceledException>(() => NativeId65FreshDocumentResearchPublisher.PublishAsync(context, missing, cancel.Token));
            await RejectAsync<OperationCanceledException>(() => NativeId65FreshDocumentResearchPublisher.VerifyAsync(context, missing, cancel.Token));
            Check(!Directory.Exists(missing), "invalid/canceled operations make no output");
            await RejectAsync<InvalidOperationException>(() => NativeId65FreshDocumentResearchPublisher.PublishAsync(context, context.Snapshot.RootPath));
            foreach (string directory in new[] { "", " ", "\0" })
                await RejectAsync<ArgumentException>(() => NativeId65FreshDocumentResearchPublisher.PublishAsync(context, directory));
            Check(Directory.GetFileSystemEntries(context.Snapshot.RootPath).Length == 2, "output overlap refusal preserves exact project owners");
            Check(Enum.GetValues<NativeId65FreshDiscResearchFault>().Select(v => (int)v).SequenceEqual(new[] { 0, 1, 2, 3 }) &&
                Enum.GetValues<NativeId65FreshEditedDocumentResearchFault>().Select(v => (int)v).SequenceEqual(new[] { 0, 1, 2, 3 }),
                "old fixed fault enums have no new accepted numeric values");
            var fixedEdited = await NativeId65FreshEditedDocumentResearchPlan.CaptureAsync(original, cue, issued);
            await RejectAsync<InvalidDataException>(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(fixedEdited, missing,
                (NativeId65FreshEditedDocumentResearchFault)4));
            Check(!Directory.Exists(missing), "old invalid fault4 still refuses before IO");
            Console.WriteLine($"FRESH_DOCUMENT_PUBLICATION_RECEIPT sha256={Hash(receipt)} bytes={receipt.Length} image={plan.ExpectedImageSha256} outputWritten=false");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Check(HashFile(original) == before && HashFile(cue) == cueBefore, "original BIN/CUE unchanged");
        }
        Console.WriteLine($"NATIVE_ID65_FRESH_DOCUMENT_PUBLICATION_SOURCE_OK checks={Checks} outputWritten=false");
    }

    internal static NativeId65FreshSavedDocumentExportContext SaveContext(string root, NativeId65FreshDocumentResearchPlan plan)
    {
        Directory.CreateDirectory(root);
        var project = new EditorProjectLayout(root, "publication-fixture");
        File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"publication-fixture\"}");
        var saved = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project), Intent);
        return NativeId65FreshSavedDocumentExportContext.Capture(saved, plan);
    }

    internal static string HashFile(string path) { using var source = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(source)); }
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); Checks++; }
    internal static void Reject<T>(Action action) where T : Exception
    { try { action(); } catch (T) { Checks++; return; } throw new InvalidDataException("Expected " + typeof(T).Name); }
    internal static async Task RejectAsync<T>(Func<Task<NativeId65FreshDiscResearchPublication>> action) where T : Exception
    { try { await action(); } catch (T) { Checks++; return; } throw new InvalidDataException("Expected " + typeof(T).Name); }
}
