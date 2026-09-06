using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

internal static class ResearchCases
{
    internal const string ExpectedHash = "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188";

    internal static int SourceFree()
    {
        int checks = CueStreamCases.Run();
        Type planType = typeof(NativeId65FreshDiscResearchCandidatePlan);
        Check(planType.IsSealed && planType.GetConstructors().Length == 0,
            "source-issued research plan has no public constructor or subclass authority");
        var capture = planType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(method => method.Name == "CaptureAsync").ToArray();
        Check(capture.Length == 1 && capture[0].GetParameters().Select(parameter => parameter.ParameterType)
            .SequenceEqual(new[] { typeof(string), typeof(string), typeof(NativeId65FreshDiscPlan), typeof(CancellationToken) }),
            "only public capture requires the actual privately issued disc-plan type, not caller metadata");
        Reject(() => NativeId65FreshDiscResearchCandidatePlan.CaptureAsync("never-open.bin", "never-open.cue", null!)
            .GetAwaiter().GetResult(), "null issued source before filesystem access");
        byte[] expected = "bounded companion\n"u8.ToArray();
        using (var stream = new MemoryStream(expected, writable: false))
        {
            NativeId65FreshDiscResearchCandidatePublisher.RequireCompanion(stream, expected, CancellationToken.None);
            Check(stream.Position == expected.Length, "exact companion read ends at real EOF");
        }
        foreach (byte[] bad in new[] { expected[..^1], expected.Concat(new byte[1]).ToArray(), new byte[expected.Length] })
            Reject(() => NativeId65FreshDiscResearchCandidatePublisher.RequireCompanion(
                new MemoryStream(bad, false), expected, CancellationToken.None), "wrong companion bytes/length");
        using (var lying = new ExtraEofStream(expected))
        {
            Reject(() => NativeId65FreshDiscResearchCandidatePublisher.RequireCompanion(lying, expected, CancellationToken.None),
                "real extra EOF despite matching declared length");
            Check(lying.EofReads == 1, "companion guard actually probes EOF");
        }
        using (var token = new CancellationTokenSource())
        {
            token.Cancel();
            bool cancelled = false;
            try { NativeId65FreshDiscResearchCandidatePublisher.RequireCompanion(new MemoryStream(expected, false), expected, token.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "cancelled companion read never returns success");
        }

        string root = Directory.CreateTempSubdirectory("spyro-id65-research-cue-").FullName;
        try
        {
            string bin = Path.Combine(root, "source.bin"), cue = Path.Combine(root, "source.cue");
            File.WriteAllBytes(bin, []); // Only a path-resolution fixture, never original-ROM authority.
            foreach (string newline in new[] { "\n", "\r\n", "\r" })
            foreach (bool bom in new[] { false, true })
            {
                string text = $"FILE \"source.bin\" BINARY{newline}  TRACK 01 MODE2/2352{newline}    INDEX 01 00:00:00{newline}";
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                if (bom) bytes = Encoding.UTF8.GetPreamble().Concat(bytes).ToArray();
                byte[] before = bytes.ToArray();
                File.WriteAllBytes(cue, bytes);
                Check(DiscCueSheet.ReferencesDataImageSnapshot(bytes, cue, bin) == DiscCueSheet.ReferencesDataImage(cue, bin),
                    "bounded snapshot and legacy file parser have exact LF/CRLF/CR/BOM parity");
                byte[] output = NativeId65FreshDiscResearchCandidatePlan.CompileCueForSmoke(bytes, cue, bin);
                Check(output.AsSpan().SequenceEqual(DiscCueSheet.TextEncoding.GetBytes(
                    "FILE \"native-id65-fresh-lab-track19-r01.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n")),
                    "fixed canonical research output CUE");
                Check(bytes.SequenceEqual(before), "CUE snapshot decoding never mutates caller bytes");
            }
            foreach (string text in new[]
            {
                "FILE \"other.bin\" BINARY\nTRACK 01 MODE2/2352\nINDEX 01 00:00:00\n",
                "FILE \"source.bin\" BINARY\nTRACK 01 MODE1/2048\nINDEX 01 00:00:00\n",
                "FILE \"source.bin\" BINARY\nTRACK 01 MODE2/2352\nINDEX 01 00:01:00\n",
                "FILE \"source.bin\" BINARY\nTRACK 01 MODE2/2352\nPREGAP 00:01:00\nINDEX 01 00:00:00\n",
                "FILE \"source.bin\" BINARY\nTRACK 01 MODE2/2352\nINDEX 01 00:00:00\nTRACK 02 AUDIO\nINDEX 01 00:02:00\n",
                "REM unsupported extra owner\nFILE \"source.bin\" BINARY\nTRACK 01 MODE2/2352\nINDEX 01 00:00:00\n"
            })
                Reject(() => NativeId65FreshDiscResearchCandidatePlan.CompileCueForSmoke(Encoding.UTF8.GetBytes(text), cue, bin),
                    "unsupported source geometry/reference refuses");
            Reject(() => NativeId65FreshDiscResearchCandidatePlan.CompileCueForSmoke(new byte[65537], cue, bin), "CUE cap before decoding");
            Reject(() => DiscCueSheet.ReferencesDataImageSnapshot(new byte[65537], cue, bin), "shared snapshot cap before decoding");
            byte[] utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(
                "FILE \"source.bin\" BINARY\nTRACK 01 MODE2/2352\nINDEX 01 00:00:00\n")).ToArray();
            File.WriteAllBytes(cue, utf16);
            Check(DiscCueSheet.ReferencesDataImageSnapshot(utf16, cue, bin) == DiscCueSheet.ReferencesDataImage(cue, bin),
                "shared helper retains legacy BOM detection without widening new research profile");
            Reject(() => NativeId65FreshDiscResearchCandidatePlan.CompileCueForSmoke(utf16, cue, bin), "research profile explicitly excludes non-UTF8 encoding");
        }
        finally
        {
            foreach (string name in new[] { "source.bin", "source.cue" })
                if (File.Exists(Path.Combine(root, name))) File.Delete(Path.Combine(root, name));
            if (!Directory.EnumerateFileSystemEntries(root).Any()) Directory.Delete(root);
        }
        return checks;

        void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); checks++; }
        void Reject(Action action, string label)
        {
            try { action(); }
            catch (Exception error) when (error is InvalidDataException or ArgumentException or InvalidOperationException)
            { checks++; return; }
            throw new InvalidDataException("Unexpected acceptance: " + label);
        }
    }

    internal static async Task<int> Source(NativeId65FreshDiscResearchCandidatePlan plan, string bin, string cue,
        NativeId65FreshDiscPlan issued, Func<int, int, int, Task<NativeId65FreshDiscPlan>> buildVariant)
    {
        int checks = 0;
        var repeat = await NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(bin, cue, issued);
        Check(plan.PlanSha256 == repeat.PlanSha256 && plan.ExpectedImageSha256 == ExpectedHash &&
            !plan.RuntimeVerified && !plan.NormalCreateBinApproved && !plan.CompleteNativeDependencyClosureProven,
            "exact source-issued repeat does not grant native or normal-export authority");
        string aliasRoot = Directory.CreateTempSubdirectory("spyro-id65-research-alias-").FullName;
        string alias = Path.Combine(aliasRoot, "alias.bin");
        string aliasCue = Path.Combine(aliasRoot, "source.cue");
        string different = Path.Combine(aliasRoot, "different.bin");
        try
        {
            File.CreateSymbolicLink(alias, Path.GetFullPath(bin));
            File.WriteAllBytes(different, []);
            File.WriteAllText(aliasCue, "FILE \"alias.bin\" BINARY\nTRACK 01 MODE2/2352\nINDEX 01 00:00:00\n", DiscCueSheet.TextEncoding);
            var aliasPlan = await NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(bin, aliasCue, issued);
            File.Delete(alias);
            File.CreateSymbolicLink(alias, different);
            bool staleBindingRefused = false;
            try { await aliasPlan.RequireFreshAsync(); }
            catch (InvalidDataException) { staleBindingRefused = true; }
            Check(staleBindingRefused, "unchanged CUE bytes cannot hide a retargeted third FILE alias");
        }
        finally
        {
            if (new FileInfo(alias).LinkTarget != null) File.Delete(alias);
            foreach (string path in new[] { aliasCue, different })
                if (File.Exists(path)) File.Delete(path);
            if (!Directory.EnumerateFileSystemEntries(aliasRoot).Any()) Directory.Delete(aliasRoot);
        }
        byte[] cueBytes = plan.CopyCueBytes();
        byte[] receipt = plan.BuildReceipt(new(ExpectedHash, 1253));
        using var document = JsonDocument.Parse(receipt);
        Check(document.RootElement.GetProperty("imageSha256").GetString() == ExpectedHash &&
            !document.RootElement.GetProperty("runtimeVerified").GetBoolean() &&
            !document.RootElement.GetProperty("normalCreateBinApproved").GetBoolean(),
            "receipt pins independent output while keeping gameplay/normal exporter false");
        cueBytes[0] ^= 1; receipt[0] ^= 1;
        Check(!plan.CopyCueBytes().SequenceEqual(cueBytes) && !plan.BuildReceipt(new(ExpectedHash, 1253)).SequenceEqual(receipt),
            "companion copies cannot mutate source authority");
        bool refused = false;
        try { plan.BuildReceipt(new(new string('0', 64), 1253)); }
        catch (InvalidDataException) { refused = true; }
        Check(refused, "caller-declared verification hash cannot issue receipt authority");
        foreach (var (x, rows, track) in new[] { (8976, 16, 19), (8960, 1, 19), (8960, 16, 20) })
        {
            var variant = await buildVariant(x, rows, track);
            refused = false;
            try { await NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(bin, cue, variant); }
            catch (InvalidDataException) { refused = true; }
            Check(refused, "actual privately issued other scene, short body or track cannot become exact research fixture");
        }
        await plan.RequireFreshAsync();
        return checks;
        void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); checks++; }
    }

    private sealed class ExtraEofStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        internal int EofReads { get; private set; }
        public override int ReadByte()
        {
            if (Position == Length) { EofReads++; return 0xA5; }
            return base.ReadByte();
        }
    }
}
