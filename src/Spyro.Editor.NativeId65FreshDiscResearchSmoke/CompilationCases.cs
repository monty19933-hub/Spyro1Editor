using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

internal static class CompilationCases
{
    internal static async Task<int> SourceFree()
    {
        int checks = 0;
        Type type = typeof(NativeId65FreshDocumentCompilation);
        Check(type.IsSealed && type.GetConstructors().Length == 0, "compilation has only private source-issued construction");
        var method = type.GetMethod("CaptureAsync")!;
        Check(method.ReturnType == typeof(Task<NativeId65FreshDocumentCompilation>) &&
            method.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                new[] { typeof(string), typeof(NativeId65FreshDocumentIntent), typeof(CancellationToken) }),
            "compiler accepts only original BIN, actual validated intent and cancellation");
        Check(type.GetProperties().All(p => p.SetMethod == null) &&
            type.GetProperties().Select(p => p.Name).Order().SequenceEqual(new[] { "CanonicalSha256", "DiscPlan", "Intent" }),
            "compilation retains actual typed owners without a new approval catalog");
        string absent = Path.Combine(Path.GetTempPath(), "spyro-document-compile-no-io-" + Guid.NewGuid().ToString("N"));
        foreach (string? bad in new string?[] { null, "", " \t" })
        {
            bool refused = false;
            try { await NativeId65FreshDocumentCompilation.CaptureAsync(bad!, Baseline()); }
            catch (ArgumentException error) when (error.ParamName == "originalBin") { refused = true; }
            Check(refused, "invalid source argument is refused before I/O");
        }
        bool nullRefused = false;
        try { await NativeId65FreshDocumentCompilation.CaptureAsync(absent, null!); }
        catch (ArgumentNullException error) when (error.ParamName == "intent") { nullRefused = true; }
        Check(nullRefused, "missing issued intent refuses before opening absent source");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        bool stopped = false;
        try { await NativeId65FreshDocumentCompilation.CaptureAsync(null!, null!, cancelled.Token); }
        catch (OperationCanceledException error) when (error.CancellationToken == cancelled.Token) { stopped = true; }
        Check(stopped && !File.Exists(absent) && !Directory.Exists(absent), "pre-cancel retains token and produces no I/O output");
        return checks;
        void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); checks++; }
    }

    internal static async Task<int> Source(string original, string cue, NativeId65FreshDiscPlan oldDisc,
        NativeId65FreshDiscResearchCandidatePlan oldCandidate)
    {
        int checks = 0;
        var baselineIntent = Baseline();
        var baseline = await NativeId65FreshDocumentCompilation.CaptureAsync(original, baselineIntent);
        Check(ReferenceEquals(baseline.Intent, baselineIntent), "compiler retains the actual immutable document object");
        Check(baseline.DiscPlan.PlanSha256 == oldDisc.PlanSha256 &&
            baseline.DiscPlan.CopyBody().SequenceEqual(oldDisc.CopyBody()) &&
            baseline.DiscPlan.CopyOverlay().SequenceEqual(oldDisc.CopyOverlay()) &&
            baseline.DiscPlan.CopyExecutable().SequenceEqual(oldDisc.CopyExecutable()) &&
            baseline.DiscPlan.CopyWadDirectory().SequenceEqual(oldDisc.CopyWadDirectory()) &&
            baseline.DiscPlan.CopyIsoRootDirectory().SequenceEqual(oldDisc.CopyIsoRootDirectory()),
            "document baseline preserves the independent old disc plan and all five complete byte owners");
        var baselineCandidate = await NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(original, cue, baseline.DiscPlan);
        checks += FactoryCases.Source(oldCandidate, baselineCandidate);
        NativeFields(baseline, baselineIntent);
        byte[] canonical = baseline.CopyCanonicalDocument();
        Check(canonical.SequenceEqual(NativeId65FreshDocumentCodec.SerializeCanonical(baselineIntent)) &&
            baseline.CanonicalSha256 == Convert.ToHexStringLower(SHA256.HashData(canonical)),
            "canonical bytes and digest bind the actual immutable intent, not caller metadata");
        Array.Fill(canonical, (byte)0);
        Check(!baseline.CopyCanonicalDocument().SequenceEqual(canonical), "canonical accessor detaches mutable output bytes");

        var variants = new[]
        {
            NativeId65FreshDocumentIntent.Create(8464, 8480, 8720, 1040, 1008,
                9008, 9024, 128, new ColorRgba(0x12, 0x34, 0x56), new ColorRgba(0xA1, 0xB2, 0xC3), 20),
            NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024,
                8960, 8960, 64, new ColorRgba(80, 128, 112), new ColorRgba(128, 96, 64), 47)
        };
        foreach (var intent in variants)
        {
            var compiled = await NativeId65FreshDocumentCompilation.CaptureAsync(original, intent);
            Check(ReferenceEquals(compiled.Intent, intent) && compiled.CopyCanonicalDocument().SequenceEqual(
                NativeId65FreshDocumentCodec.SerializeCanonical(intent)), "variant retains the caller's actual complete immutable intent");
            NativeFields(compiled, intent);
            Check(compiled.CanonicalSha256 != baseline.CanonicalSha256 && compiled.DiscPlan.PlanSha256 != baseline.DiscPlan.PlanSha256,
                "edited document changes both canonical identity and actual source-issued native plan");
            Check(compiled.DiscPlan.CopyOverlay().SequenceEqual(oldDisc.CopyOverlay()) &&
                compiled.DiscPlan.CopyIsoRootDirectory().SequenceEqual(oldDisc.CopyIsoRootDirectory()) &&
                compiled.DiscPlan.CopyWadDirectory().SequenceEqual(oldDisc.CopyWadDirectory()),
                "same-cell edits retain the exact engine and physical directory placement");
            bool refused = false;
            try { await NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(original, cue, compiled.DiscPlan); }
            catch (InvalidDataException error) when (error.Message.Contains("exact privately issued untextured A/N16/track19", StringComparison.Ordinal))
            { refused = true; }
            Check(refused, "edited document cannot inherit exact fixed research publication authority");
        }
        return checks;

        void NativeFields(NativeId65FreshDocumentCompilation compiled, NativeId65FreshDocumentIntent intent)
        {
            byte[] body = compiled.DiscPlan.CopyBody(), exe = compiled.DiscPlan.CopyExecutable();
            int data = checked((int)Word(body, 8)), scene = checked((int)Word(body, 24));
            Check(Word(body, data) == 8 && Word(body, data + 4) == 0, "actual zero texture-metadata component precedes the surface bundle");
            int environment = data + checked((int)Word(body, data)), sector = environment + 12;
            Check((Word(body, sector + 8) >> 16) * 16 == intent.FloorX &&
                (Word(body, sector + 8) & 65535) * 16 == intent.FloorY &&
                (Word(body, sector + 12) >> 16) * 16 == intent.FloorZ,
                "all authored floor origins reach actual native environment words");
            uint[] points = [0, (uint)(intent.FloorWidth / 16) << 21, (uint)(intent.FloorDepth / 16) << 10,
                (uint)(intent.FloorWidth / 16) << 21 | (uint)(intent.FloorDepth / 16) << 10];
            for (int i = 0; i < 4; i++)
            {
                Check(Word(body, sector + 28 + i * 4) == points[i] && Word(body, sector + 76 + i * 4) == points[i],
                    "width/depth reach all actual LP/HP vertices");
                foreach (int table in new[] { 44, 92, 108 })
                    Check(body.AsSpan(sector + table + i * 4, 4).SequenceEqual(
                        new byte[] { intent.FloorColor.R, intent.FloorColor.G, intent.FloorColor.B, 0 }),
                        "every LP/HP near/far native color is exact display R/G/B/0");
            }
            int collision = environment + 172 + 32 + 8, triangles = collision + 4 + checked((int)Word(body, collision + 20));
            Check((Word(body, triangles) & 16383) * 16 == intent.FloorX &&
                ((Word(body, triangles) >> 23) & 511) * 16 == intent.FloorWidth &&
                (Word(body, triangles + 4) & 16383) * 16 == intent.FloorY &&
                ((Word(body, triangles + 4) >> 14) & 511) * 16 == intent.FloorDepth &&
                Word(body, triangles + 8) * 16 == intent.FloorZ,
                "native collision origin/extents/height follow the same document");
            int sky = collision + checked((int)Word(body, collision));
            Check(body.AsSpan(sky + 4, 4).SequenceEqual(new byte[] { intent.SkyColor.R, intent.SkyColor.G, intent.SkyColor.B, 0 }),
                "actual cyclorama color comes from display RGB");
            Check(Word(body, scene) == intent.SpawnX && Word(body, scene + 4) == intent.SpawnY &&
                Word(body, scene + 8) == intent.SpawnZ && body[scene + 14] == intent.Heading,
                "actual native landing XYZ/heading includes code-owned Z offset");
            Check(Word(body, scene + 0xD0) == 0 && Word(body, scene + 0xCC) == 8 + 16 * 0x70 + 0x58,
                "zero placed count and nominal16 storage remain code-owned");
            Check(Word(exe, 0x5F79C + 35 * 4) == intent.MusicTrackId &&
                compiled.DiscPlan.Executable.SelectedBuiltInTrack == intent.MusicTrackId,
                "music intent reaches the actual native slot35 executable word");
        }
        void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); checks++; }
    }

    private static NativeId65FreshDocumentIntent Baseline() => NativeId65FreshDocumentIntent.Create(
        8448, 8448, 8704, 1024, 1024, 8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), 19);
    private static uint Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
}
