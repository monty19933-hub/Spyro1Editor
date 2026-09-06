using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Primitives;

internal static class PlanCases
{
    internal const long ImageLength = 661547040;
    internal const string OriginalHash = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    internal static int Checks { get; private set; }
    internal static readonly string[] Images =
    [
        "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188",
        "ecc8f61cc16bf86a2f5117b327c49aede18f5c1e6a602c0e67f2ba58565aed16",
        "0c3821141e50c74aee91d4ff4eeb7ef8632c3cd63daab624dd4d504d9ed83db2",
        "40d8cf3fb4168bdd870c8d6f73c2805e357160fa37486205f1b813b743855c2d"
    ];
    private static readonly string[] Bodies =
    [
        "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c",
        "bae43882a39dfc1604f4e6d578a354f34f99107f97faa9330600b3e01f34b15b",
        "055a138680475d075299fd1d722cdfa6d26d25dbf1ece1c3ba2ca9e5303c0134",
        "3c69036d98cc9734a936f9904ad7ffdd453f5376fe602bec3106a722f0cfdee9"
    ];

    internal static NativeId65FreshDocumentIntent Recipe(int field = -1, int track = 2) =>
        NativeId65FreshDocumentIntent.Create(
            field == 0 ? 8208 : 8192, field == 1 ? 8208 : 8192, field == 2 ? 8720 : 8704,
            field == 3 ? 2032 : 2048, field == 4 ? 2032 : 2048,
            field == 5 ? 9232 : 9216, field == 6 ? 9232 : 9216, field == 7 ? 97 : 96,
            new ColorRgba((byte)(field == 8 ? 73 : 72), (byte)(field == 9 ? 133 : 132), (byte)(field == 10 ? 121 : 120)),
            new ColorRgba((byte)(field == 11 ? 45 : 44), (byte)(field == 12 ? 67 : 66), (byte)(field == 13 ? 101 : 100)), track);

    internal static NativeId65FreshDocumentIntent GoldenIntent(int i) => i switch
    {
        0 => NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024, 8960, 8960, 64,
            new(80, 128, 112), new(128, 96, 64), 19),
        1 => Recipe(),
        2 => NativeId65FreshDocumentIntent.Create(0, 0, 0, 16, 16, 1, 1, 0, new(80, 128, 112), new(128, 96, 64), 2),
        3 => NativeId65FreshDocumentIntent.Create(258048, 258048, 258048, 16, 16, 258049, 258049, 0,
            new(80, 128, 112), new(128, 96, 64), 2),
        _ => throw new ArgumentOutOfRangeException(nameof(i))
    };

    internal static async Task SourceFree()
    {
        Type type = typeof(NativeId65FreshDocumentResearchPlan);
        Check(type.IsSealed && type.GetConstructors().Length == 0, "No public construction or inheritance authority.");
        Check(type.GetProperties().All(p => p.SetMethod == null), "No mutable public metadata.");
        var captures = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == "CaptureAsync").ToArray();
        Check(captures.Length == 1 && captures[0].GetParameters().Select(p => p.ParameterType).SequenceEqual(
            new[] { typeof(string), typeof(string), typeof(NativeId65FreshDocumentCompilation), typeof(CancellationToken) }),
            "Only actual Compilation input; no caller hash, flags, recipe/body pair or receipt.");
        Check(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .All(m => !m.Name.Contains("Write", StringComparison.Ordinal) && !m.Name.Contains("Publish", StringComparison.Ordinal)),
            "Plan has no output writer or publication API.");
        await Reject<ArgumentNullException>(() => NativeId65FreshDocumentResearchPlan.CaptureAsync("missing.bin", "missing.cue", null!));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Reject<OperationCanceledException>(() => NativeId65FreshDocumentResearchPlan.CaptureAsync(null!, null!, null!, cancel.Token));
        Check(Enumerable.Range(0, 4).Select(i => Hash(NativeId65FreshDocumentCodec.SerializeCanonical(GoldenIntent(i)))).Distinct().Count() == 4,
            "All four neutral golden documents are distinct without granting source authority.");
        for (int i = 0; i < 14; i++)
            Check(Hash(NativeId65FreshDocumentCodec.SerializeCanonical(Recipe(i))) != Hash(NativeId65FreshDocumentCodec.SerializeCanonical(Recipe())),
                "Each valid non-music alternative changes canonical intent.");
        var tracks = Enumerable.Range(0, 48).Where(id => MusicTrackCatalog.Find(id)?.IsSelectable == true).ToArray();
        Check(tracks.Length == 46 && tracks.All(id => Recipe(track: id).MusicTrackId == id), "All46 selectable native music values are neutral-valid.");
        await StreamCases.SourceFree();
    }

    internal static async Task Source(string original, string cue)
    {
        byte[] cueBefore = File.ReadAllBytes(cue);
        RequireOriginal(original);
        try
        {
            NativeId65FreshDocumentCompilation? edited = null;
            NativeId65FreshDocumentResearchPlan? editedPlan = null;
            string? legacyStarterPlan = null, legacyEditedPlan = null;
            for (int i = 0; i < 4; i++)
            {
                var intent = GoldenIntent(i);
                var compiled = await NativeId65FreshDocumentCompilation.CaptureAsync(original, intent);
                Check(ReferenceEquals(compiled.Intent, intent), "Actual immutable intent is retained by compilation.");
                Check(Hash(compiled.DiscPlan.CopyBody()) == Bodies[i], "Independent complete body golden remains exact.");
                var plan = await NativeId65FreshDocumentResearchPlan.CaptureAsync(original, cue, compiled);
                Check(plan.ExpectedImageSha256 == Images[i] && plan.ExpectedChangedRawSectors == 1253,
                    "Private streaming derives independently known full image, not a caller recipe hash.");
                RequireBinding(compiled, plan);
                NativeFields(compiled);
                using (var source = File.OpenRead(original))
                using (var output = new GeneratedImage(original, compiled.DiscPlan))
                {
                    var verified = plan.VerifyImage(source, output, CancellationToken.None);
                    Check(verified.OutputSha256 == Images[i] && verified.ChangedSectors == 1253,
                        "Actual successful full readback without writing candidate bytes.");
                }
                if (i == 0)
                {
                    var old = await NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(original, cue, compiled.DiscPlan);
                    legacyStarterPlan = old.PlanSha256;
                    Check(old.ExpectedImageSha256 == Images[0], "Old starter still has its independently pinned output.");
                    await Reject<InvalidDataException>(() => NativeId65FreshEditedDocumentResearchPlan.CaptureAsync(original, cue, compiled));
                }
                else
                {
                    await Reject<InvalidDataException>(() => NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(original, cue, compiled.DiscPlan));
                    if (i == 1)
                    {
                        edited = compiled; editedPlan = plan;
                        var old = await NativeId65FreshEditedDocumentResearchPlan.CaptureAsync(original, cue, compiled);
                        legacyEditedPlan = old.PlanSha256;
                        Check(old.ExpectedImageSha256 == Images[1], "Old exact edited profile remains independently pinned.");
                    }
                    else await Reject<InvalidDataException>(() => NativeId65FreshEditedDocumentResearchPlan.CaptureAsync(original, cue, compiled));
                }
                Console.WriteLine($"GENERIC_FRESH_GOLDEN_OK index={i} image={plan.ExpectedImageSha256} changed={plan.ExpectedChangedRawSectors} outputWritten=false");
            }
            var baseline = edited!;
            var baselinePlan = editedPlan!;
            for (int field = 0; field < 14; field++)
            {
                var compiled = await NativeId65FreshDocumentCompilation.CaptureAsync(original, Recipe(field));
                var plan = await NativeId65FreshDocumentResearchPlan.CaptureAsync(original, cue, compiled);
                RequireBinding(compiled, plan);
                NativeFields(compiled);
                Check(plan.ExpectedImageSha256 != baselinePlan.ExpectedImageSha256 && plan.PlanSha256 != baselinePlan.PlanSha256,
                    "Actual changed field changes compiled expected image and plan identity.");
                Check(Hash(compiled.DiscPlan.CopyExecutable()) == Hash(baseline.DiscPlan.CopyExecutable()), "Non-music field preserves full EXE.");
                await Reject<InvalidDataException>(() => NativeId65FreshEditedDocumentResearchPlan.CaptureAsync(original, cue, compiled));
                Console.WriteLine($"GENERIC_FRESH_FIELD_OK field={field}");
            }
            int tracks = 0;
            foreach (int track in Enumerable.Range(0, 48).Where(id => MusicTrackCatalog.Find(id)?.IsSelectable == true))
            {
                var compiled = track == 2 ? baseline : await NativeId65FreshDocumentCompilation.CaptureAsync(original, Recipe(track: track));
                var plan = track == 2 ? baselinePlan : await NativeId65FreshDocumentResearchPlan.CaptureAsync(original, cue, compiled);
                RequireBinding(compiled, plan);
                NativeFields(compiled);
                Check(Hash(compiled.DiscPlan.CopyBody()) == Bodies[1], "Music-only variation preserves complete body.");
                byte[] exe = compiled.DiscPlan.CopyExecutable(), oldExe = baseline.DiscPlan.CopyExecutable();
                oldExe.AsSpan(0x5F79C + 35 * 4, 4).CopyTo(exe.AsSpan(0x5F79C + 35 * 4));
                Check(exe.SequenceEqual(oldExe), "Every music variation preserves full EXE complement outside slot35.");
                Check(track == 2 || plan.ExpectedImageSha256 != baselinePlan.ExpectedImageSha256, "Music changes real physical afterimage.");
                tracks++;
                Console.WriteLine($"GENERIC_FRESH_MUSIC_OK track={track}");
            }
            Check(tracks == 46, "All46 actual compiled tracks pass generic plan capture.");
            var repeated = await NativeId65FreshDocumentResearchPlan.CaptureAsync(original, cue, baseline);
            Check(repeated.PlanSha256 == baselinePlan.PlanSha256, "Deterministic exact input identity.");
            Check((await NativeId65FreshEditedDocumentResearchPlan.CaptureAsync(original, cue, baseline)).PlanSha256 == legacyEditedPlan,
                "Old edited plan identity remains unchanged after generic alternatives.");
            var starterAgain = await NativeId65FreshDocumentCompilation.CaptureAsync(original, GoldenIntent(0));
            Check((await NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(original, cue, starterAgain.DiscPlan)).PlanSha256 == legacyStarterPlan,
                "Old starter plan identity remains unchanged after generic alternatives.");
            await StreamCases.Run(original, cue, baseline, baselinePlan);
        }
        finally
        {
            RequireOriginal(original);
            Check(File.ReadAllBytes(cue).SequenceEqual(cueBefore), "Whole original CUE bytes preserved before/after.");
            Console.WriteLine($"GENERIC_FRESH_ORIGINAL_CUE_UNCHANGED sha256={Hash(cueBefore)} bytes={cueBefore.Length}");
        }
    }

    private static void RequireBinding(NativeId65FreshDocumentCompilation compiled, NativeId65FreshDocumentResearchPlan plan)
    {
        Check(plan.CanonicalDocumentSha256 == compiled.CanonicalSha256 && plan.CopyCanonicalDocument().SequenceEqual(compiled.CopyCanonicalDocument()),
            "Canonical bytes bind the actual full compilation.");
        Check(plan.DiscPlanSha256 == compiled.DiscPlan.PlanSha256 && plan.BodyPlanSha256 == compiled.DiscPlan.BodyPlanSha256,
            "Full disc/body identity, never equal upload/staging SHA alone.");
        Check(plan.BodySha256 == Hash(compiled.DiscPlan.CopyBody()) && plan.OverlaySha256 == Hash(compiled.DiscPlan.CopyOverlay()) &&
            plan.ExecutableSha256 == Hash(compiled.DiscPlan.CopyExecutable()) && plan.WadDirectorySha256 == Hash(compiled.DiscPlan.CopyWadDirectory()) &&
            plan.IsoRootDirectorySha256 == Hash(compiled.DiscPlan.CopyIsoRootDirectory()) && plan.ExpectedImageByteLength == ImageLength,
            "Every complete owner digest and physical image length is derived from this compilation.");
        Check(plan.PendingNativeOwners.SequenceEqual(compiled.DiscPlan.PendingOwners) && plan.PendingNativeOwners is not string[],
            "Pending boundaries are copied into immutable metadata, not promoted or aliased.");
        Check(!plan.RuntimeVerified && !plan.NormalCreateBinApproved && !plan.CompleteNativeDependencyClosureProven && !plan.ProjectSaveBindingClaimed,
            "Derived expected hash is not independent recipe, runtime or project-save authority.");
        byte[] canonical = plan.CopyCanonicalDocument(); canonical[0] ^= 1;
        Check(Hash(plan.CopyCanonicalDocument()) == compiled.CanonicalSha256, "No canonical alias.");
        byte[] bytes = plan.CopyCueBytes();
        Check(Encoding.UTF8.GetString(bytes) == "FILE \"native-id65-fresh-document-research.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n",
            "Distinct generic output basename; old helper's starter bytes are not emitted.");
        bytes[0] ^= 1;
        Check(plan.CopyCueBytes()[0] != bytes[0], "No CUE alias.");
    }

    private static void NativeFields(NativeId65FreshDocumentCompilation compiled)
    {
        var i = compiled.Intent;
        byte[] body = compiled.DiscPlan.CopyBody(), exe = compiled.DiscPlan.CopyExecutable();
        int data = (int)Word(body, 8), scene = (int)Word(body, 24), environment = data + 8, sector = environment + 12;
        Check(Word(body, data) == 8 && Word(body, data + 4) == 0, "Actual untextured metadata.");
        Check((Word(body, sector + 8) >> 16) * 16 == i.FloorX && (Word(body, sector + 8) & 65535) * 16 == i.FloorY &&
            (Word(body, sector + 12) >> 16) * 16 == i.FloorZ, "Native environment origin uses all XYZ fields.");
        uint[] points = [0, (uint)(i.FloorWidth / 16) << 21, (uint)(i.FloorDepth / 16) << 10,
            (uint)(i.FloorWidth / 16) << 21 | (uint)(i.FloorDepth / 16) << 10];
        for (int at = 0; at < 4; at++)
        {
            Check(Word(body, sector + 28 + at * 4) == points[at] && Word(body, sector + 76 + at * 4) == points[at], "Native LP/HP extents.");
            foreach (int table in new[] { 44, 92, 108 })
                Check(body.AsSpan(sector + table + at * 4, 4).SequenceEqual(new byte[] { i.FloorColor.R, i.FloorColor.G, i.FloorColor.B, 0 }), "Native floor RGB.");
        }
        int collision = environment + 172 + 32 + 8, triangles = collision + 4 + (int)Word(body, collision + 20);
        Check((Word(body, triangles) & 16383) * 16 == i.FloorX && ((Word(body, triangles) >> 23) & 511) * 16 == i.FloorWidth &&
            (Word(body, triangles + 4) & 16383) * 16 == i.FloorY && ((Word(body, triangles + 4) >> 14) & 511) * 16 == i.FloorDepth &&
            Word(body, triangles + 8) * 16 == i.FloorZ, "Collision follows rendered geometry, including variable tree.");
        int sky = collision + (int)Word(body, collision);
        Check(body.AsSpan(sky + 4, 4).SequenceEqual(new byte[] { i.SkyColor.R, i.SkyColor.G, i.SkyColor.B, 0 }), "Native sky RGB.");
        Check(Word(body, scene) == i.SpawnX && Word(body, scene + 4) == i.SpawnY && Word(body, scene + 8) == i.SpawnZ && body[scene + 14] == i.Heading,
            "All native spawn/heading fields follow actual intent.");
        Check(Word(body, scene + 0xD0) == 0 && Word(body, scene + 0xCC) == 8 + 16 * 0x70 + 0x58, "Zero placed Mobys and N16 stay code-owned.");
        Check(Word(exe, 0x5F79C + 35 * 4) == i.MusicTrackId, "Actual music slot35.");
    }

    internal static void RequireOriginal(string path)
    {
        using var input = File.OpenRead(path);
        Check(input.Length == ImageLength && Convert.ToHexStringLower(SHA256.HashData(input)) == OriginalHash &&
            input.Position == ImageLength && input.ReadByte() == -1 && input.Position == ImageLength && input.Length == ImageLength,
            "Whole original SHA/length/position/actual EOF before and after source cases.");
        Console.WriteLine($"GENERIC_FRESH_ORIGINAL_BOUND sha256={OriginalHash} bytes={ImageLength} actualEof=true");
    }
    internal static uint Word(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static void Check(bool value, string why) { if (!value) throw new InvalidDataException(why); Checks++; }
    internal static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { Checks++; return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
}
