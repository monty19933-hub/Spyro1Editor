using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

internal static class EditedCases
{
    internal const string CanonicalHash = "b3f10d40db88cbfa3d3393e0d9c9e5e49e9a829f3fd311bcfa005082016e4cf1";
    internal const string BodyHash = "bae43882a39dfc1604f4e6d578a354f34f99107f97faa9330600b3e01f34b15b";
    internal const string ExeHash = "2c1fd87e9fa8ce5c5f16be53e738405cec90c0bfc10ac9cb8cf1ce979b2e5c57";
    internal const string ImageHash = "ecc8f61cc16bf86a2f5117b327c49aede18f5c1e6a602c0e67f2ba58565aed16";
    internal static int Checks { get; private set; }

    // Each alternative changes one VALID authoring field; no malformed surrogate
    // is allowed to stand in for the exact-profile refusal being tested.
    internal static NativeId65FreshDocumentIntent Recipe(int field = -1) =>
        NativeId65FreshDocumentIntent.Create(
            field == 0 ? 8208 : 8192, field == 1 ? 8208 : 8192, field == 2 ? 8720 : 8704,
            field == 3 ? 2032 : 2048, field == 4 ? 2032 : 2048,
            field == 5 ? 9232 : 9216, field == 6 ? 9232 : 9216, field == 7 ? 97 : 96,
            new ColorRgba((byte)(field == 8 ? 73 : 72), (byte)(field == 9 ? 133 : 132), (byte)(field == 10 ? 121 : 120)),
            new ColorRgba((byte)(field == 11 ? 45 : 44), (byte)(field == 12 ? 67 : 66), (byte)(field == 13 ? 101 : 100)),
            field == 14 ? 3 : 2);

    internal static async Task SourceFree()
    {
        Check(Hash(NativeId65FreshDocumentCodec.SerializeCanonical(Recipe())) == CanonicalHash,
            "Neutral canonical recipe matches independent JSON, not source authority.");
        for (int field = 0; field < 15; field++)
            Check(Hash(NativeId65FreshDocumentCodec.SerializeCanonical(Recipe(field))) != CanonicalHash,
                "Every per-field alternative is valid but not the exact recipe.");
        Type type = typeof(NativeId65FreshEditedDocumentResearchPlan);
        Check(type.IsSealed && type.GetConstructors().Length == 0, "No public plan construction.");
        var captures = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "CaptureAsync").ToArray();
        Check(captures.Length == 1 && captures[0].GetParameters().Select(p => p.ParameterType)
            .SequenceEqual(new[] { typeof(string), typeof(string),
                typeof(NativeId65FreshDocumentCompilation), typeof(CancellationToken) }),
            "Only actual issued compilation, not caller hash, DiscPlan pair or flags.");
        Check(type.GetProperties().All(p => p.SetMethod == null), "Public state is immutable.");
        await Reject<ArgumentNullException>(() => NativeId65FreshEditedDocumentResearchPlan
            .CaptureAsync("missing.bin", "missing.cue", null!), "issued");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Reject<OperationCanceledException>(() => NativeId65FreshEditedDocumentResearchPlan
            .CaptureAsync(null!, null!, null!, canceled.Token));
        await Reject<ArgumentNullException>(() => NativeId65FreshEditedDocumentResearchPublisher.VerifyAsync(null!, "missing-output"));
        await Reject<ArgumentNullException>(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(
            null!, "missing-output", NativeId65FreshEditedDocumentResearchFault.BeforePublication));
        await Reject<InvalidDataException>(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(
            null!, "missing-output", NativeId65FreshEditedDocumentResearchFault.None), "force a precommit fault");
    }

    internal static async Task Source(string originalBin, string originalCue)
    {
        var intent = Recipe();
        var compiled = await NativeId65FreshDocumentCompilation.CaptureAsync(originalBin, intent);
        Check(ReferenceEquals(compiled.Intent, intent) && compiled.CanonicalSha256 == CanonicalHash,
            "Actual issued intent is this recipe.");
        byte[] copied = compiled.CopyCanonicalDocument();
        copied[0] ^= 1;
        Check(Hash(compiled.CopyCanonicalDocument()) == CanonicalHash, "Canonical copy has no alias.");
        Check(Hash(compiled.DiscPlan.CopyBody()) == BodyHash, "Complete independent body.");
        Check(Hash(compiled.DiscPlan.CopyExecutable()) == ExeHash, "Actual track02 complete EXE.");
        NativeFields(compiled);
        await Reject<InvalidDataException>(() => NativeId65FreshDiscResearchCandidatePlan
            .CaptureAsync(originalBin, originalCue, compiled.DiscPlan), "exact privately issued");
        var plan = await NativeId65FreshEditedDocumentResearchPlan.CaptureAsync(originalBin, originalCue, compiled);
        Check(plan.ExpectedImageSha256 == ImageHash && plan.CanonicalDocumentSha256 == CanonicalHash,
            "Plan binds code-owned complete afterimage and actual canonical intent.");
        Check(!plan.RuntimeVerified && !plan.NormalCreateBinApproved && !plan.CompleteNativeDependencyClosureProven,
            "Readback is not native acceptance or normal-export approval.");
        var again = await NativeId65FreshEditedDocumentResearchPlan.CaptureAsync(originalBin, originalCue, compiled);
        Check(again.PlanSha256 == plan.PlanSha256, "Repeated exact source capture is deterministic.");
        await Reject<InvalidDataException>(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(
            plan, "must-not-create-output", (NativeId65FreshEditedDocumentResearchFault)999), "Unknown");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Reject<OperationCanceledException>(() => NativeId65FreshEditedDocumentResearchPlan
            .CaptureAsync("must-not-open.bin", "must-not-open.cue", compiled, canceled.Token));
        foreach (string? invalid in new string?[] { null, "", " \t" })
        {
            await Reject<ArgumentException>(() => NativeId65FreshEditedDocumentResearchPlan
                .CaptureAsync(invalid!, originalCue, compiled), "originalBin");
            await Reject<ArgumentException>(() => NativeId65FreshEditedDocumentResearchPlan
                .CaptureAsync(originalBin, invalid!, compiled), "sourceCue");
        }
        for (int field = 0; field < 15; field++)
        {
            var alternate = await NativeId65FreshDocumentCompilation.CaptureAsync(originalBin, Recipe(field));
            Check(alternate.DiscPlan.CopyBody().Length == compiled.DiscPlan.CopyBody().Length,
                "Different valid contents share the same CD allocation.");
            NativeFields(alternate);
            await Reject<InvalidDataException>(() => NativeId65FreshEditedDocumentResearchPlan
                .CaptureAsync(originalBin, originalCue, alternate), "exact edited document");
            Console.WriteLine($"EDITED_DOCUMENT_VALID_ALTERNATIVE_REFUSED field={field}");
        }
        var starterIntent = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024,
            8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), 19);
        var starter = await NativeId65FreshDocumentCompilation.CaptureAsync(originalBin, starterIntent);
        var old = await NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(originalBin, originalCue, starter.DiscPlan);
        Check(old.ExpectedImageSha256 == "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188",
            "Exact fixed starter acceptance remains independent and unchanged.");
        await Reject<InvalidDataException>(() => NativeId65FreshEditedDocumentResearchPlan
            .CaptureAsync(originalBin, originalCue, starter), "exact edited document");
        byte[] restored = compiled.DiscPlan.CopyBody(), baseline = starter.DiscPlan.CopyBody();
        int data = checked((int)Word(restored, 8)), scene = checked((int)Word(restored, 24));
        baseline.AsSpan(data + 8, 324).CopyTo(restored.AsSpan(data + 8));
        baseline.AsSpan(scene, 16).CopyTo(restored.AsSpan(scene));
        Check(restored.SequenceEqual(baseline), "Only authored surface and landing header differ from complete starter body.");
        await PhysicalCases.Run(originalBin, originalCue, compiled, plan, starter.DiscPlan);
    }

    private static void NativeFields(NativeId65FreshDocumentCompilation compiled)
    {
        var intent = compiled.Intent;
        byte[] body = compiled.DiscPlan.CopyBody(), exe = compiled.DiscPlan.CopyExecutable();
        int data = checked((int)Word(body, 8)), scene = checked((int)Word(body, 24));
        Check(Word(body, data) == 8 && Word(body, data + 4) == 0, "Actual zero texture metadata.");
        int environment = data + checked((int)Word(body, data)), sector = environment + 12;
        Check((Word(body, sector + 8) >> 16) * 16 == intent.FloorX &&
            (Word(body, sector + 8) & 65535) * 16 == intent.FloorY &&
            (Word(body, sector + 12) >> 16) * 16 == intent.FloorZ, "Actual environment origin.");
        uint[] points = [0, (uint)(intent.FloorWidth / 16) << 21, (uint)(intent.FloorDepth / 16) << 10,
            (uint)(intent.FloorWidth / 16) << 21 | (uint)(intent.FloorDepth / 16) << 10];
        for (int i = 0; i < 4; i++)
        {
            Check(Word(body, sector + 28 + i * 4) == points[i] && Word(body, sector + 76 + i * 4) == points[i],
                "Width/depth in all native LP/HP vertices.");
            foreach (int table in new[] { 44, 92, 108 })
                Check(body.AsSpan(sector + table + i * 4, 4).SequenceEqual(
                    new byte[] { intent.FloorColor.R, intent.FloorColor.G, intent.FloorColor.B, 0 }), "Exact floor RGB.");
        }
        int collision = environment + 172 + 32 + 8;
        int triangles = collision + 4 + checked((int)Word(body, collision + 20));
        Check((Word(body, triangles) & 16383) * 16 == intent.FloorX &&
            ((Word(body, triangles) >> 23) & 511) * 16 == intent.FloorWidth &&
            (Word(body, triangles + 4) & 16383) * 16 == intent.FloorY &&
            ((Word(body, triangles + 4) >> 14) & 511) * 16 == intent.FloorDepth &&
            Word(body, triangles + 8) * 16 == intent.FloorZ, "Native collision matches authored floor.");
        int sky = collision + checked((int)Word(body, collision));
        Check(body.AsSpan(sky + 4, 4).SequenceEqual(new byte[] { intent.SkyColor.R, intent.SkyColor.G, intent.SkyColor.B, 0 }),
            "Exact cyclorama RGB.");
        Check(Word(body, scene) == intent.SpawnX && Word(body, scene + 4) == intent.SpawnY &&
            Word(body, scene + 8) == intent.SpawnZ && body[scene + 14] == intent.Heading, "Actual landing XYZ/heading.");
        Check(Word(body, scene + 0xD0) == 0 && Word(body, scene + 0xCC) == 8 + 16 * 0x70 + 0x58,
            "Zero authored Mobys and nominal16 storage are unchanged.");
        Check(Word(exe, 0x5F79C + 35 * 4) == intent.MusicTrackId &&
            compiled.DiscPlan.Executable.SelectedBuiltInTrack == intent.MusicTrackId, "Actual music slot35 word.");
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static uint Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    internal static void Check(bool value, string description)
    {
        if (!value) throw new InvalidDataException(description);
        Checks++;
    }
    internal static async Task Reject<T>(Func<Task> action, string fragment = "") where T : Exception
    {
        try { await action(); }
        catch (T error)
        {
            if (!error.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Wrong refusal: " + error.Message, error);
            Checks++;
            return;
        }
        throw new InvalidOperationException("Expected precise refusal: " + typeof(T).Name + "/" + fragment);
    }
}
