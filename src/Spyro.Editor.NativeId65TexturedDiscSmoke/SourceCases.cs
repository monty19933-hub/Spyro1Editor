using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Music;

internal static class SourceCases
{
    internal const long Size = 661547040;
    internal const string Original = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string Unopened = "textured-disc-cross-profile-must-not-open.bin";

    internal static async Task Run(string path, Action<bool, string> check)
    {
        var failures = new List<Exception>(); FileStream? held = null;
        try { held = File.OpenRead(path); RequireOriginal(held); await Core(path, check); }
        catch (Exception e) { failures.Add(e); }
        finally
        {
            if (held != null)
            {
                try { RequireOriginal(held); check(true, "Held entire original length/hash/true EOF remain exact finally."); }
                catch (Exception e) { failures.Add(e); }
                try { held.Dispose(); } catch (Exception e) { failures.Add(e); }
            }
        }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new IOException("Source test and all final owner failures.", new AggregateException(failures));
    }

    private static async Task Core(string path, Action<bool, string> check)
    {
        using Stream embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "Spyro.Editor.NativeId65TexturedDiscSmoke.source_textured_disc_golden.json")!;
        using var reference = JsonDocument.Parse(embedded);
        JsonElement golden = reference.RootElement;
        check(golden.GetProperty("all_final_owners_verified").GetBoolean() &&
            !golden.GetProperty("physical_output_written").GetBoolean(), "Reference is the retained independent preproduction no-output oracle.");
        var surface = await NativeId65AuthoredSurfaceBundle.CaptureAsync(path, new(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080));
        var companion = await NativeId65CompanionAssetPlan.CaptureAsync(path);
        var resources = await NativeId65NormalResourceBundle.CaptureAsync(path, surface, companion);
        var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(path);
        var chain = NativeId65NormalResourceChain.Compile(resources, lists);
        NativeId65FreshSceneBundle[] scenes = [
            await NativeId65FreshSceneBundle.CaptureAsync(path, chain, new(8960, 8960, 9216, 64), 16),
            await NativeId65FreshSceneBundle.CaptureAsync(path, chain, new(8976, 8960, 9216, 128), 16),
            await NativeId65FreshSceneBundle.CaptureAsync(path, chain, new(8960, 8960, 9216, 64), 1)];
        var sources = new Dictionary<(int, int), NativeStaticTerrainTextureSource>();
        foreach (int entry in new[] { 14, 70 }) foreach (int id in new[] { 0, 1 })
            sources.Add((entry, id), NativeStaticTerrainTextureSource.Capture(path, entry, id));
        var legacyStage = await NativeId65NormalUploadStaging.CaptureAsync(path, scenes[0]);
        var legacyBody = await NativeId65NormalBodyPlan.CaptureAsync(path, scenes[0], legacyStage);
        var legacy = await NativeId65FreshDiscPlan.CaptureAsync(path, legacyBody, 19);
        check(legacy.BindingProfileId == NativeId65FreshDiscPlan.ProfileId &&
            legacyBody.PlanSha256 == "48f1f0b4f7740c8a6277a4b440990e1fd26cb609db2153c3c24fc61d70dbbb94",
            "Existing actual untextured body serialization is unchanged.");
        FullPhysical(path, legacy, "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188", 1253, check);
        ShapeCases.Reject(() => NativeId65FreshDiscPlan.CaptureTexturedAsync(Unopened, legacyBody, 19).GetAwaiter().GetResult(),
            check, "Actual old body refuses the new factory before path I/O.", "textured");
        int ordinal = 0;
        foreach (JsonElement row in golden.GetProperty("outputs").EnumerateArray())
        {
            string name = row.GetProperty("name").GetString()!;
            var selection = row.GetProperty("selections").EnumerateArray().Select(s => sources[(s[0].GetInt32(), s[1].GetInt32())]).ToArray();
            var scene = scenes[name == "fixed-b" ? 1 : name == "fixed-short" ? 2 : 0];
            var texture = name.StartsWith("fixed", StringComparison.Ordinal)
                ? await NativeId65FreshTerrainTexturePlan.CaptureAsync(path, scene, selection[0], selection[1])
                : await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(path, scene, selection[0], selection[1]);
            var staging = await NativeId65NormalUploadStaging.CaptureTexturedAsync(path, texture);
            var body = await NativeId65NormalBodyPlan.CaptureTexturedAsync(path, texture, staging);
            check(texture.UploadSha256 == Text(row, "upload_sha256") && texture.DataSha256 == Text(row, "data_sha256") &&
                texture.Allocation.Records.Select(r => r.RecordSha256).SequenceEqual(row.GetProperty("record_sha256").EnumerateArray().Select(e => e.GetString())) &&
                body.LevelDataSha256 == Text(row, "body_sha256"), "Actual complete texture/body bytes match independent source reconstruction: " + name);
            var disc = await NativeId65FreshDiscPlan.CaptureTexturedAsync(path, body, 19);
            check(ReferenceEquals(disc.BodyAncestor, body) && disc.BodyPlanSha256 == body.PlanSha256 &&
                disc.BindingProfileId == ApiCases.Binding && disc.PlanSha256 != legacy.PlanSha256,
                "New profile retains the actual issued body, not a caller digest or forged texture flag.");
            disc.RequireAncestry(body);
            ShapeCases.Reject(() => disc.RequireAncestry(legacyBody), check, "Foreign actual body cannot replace retained ancestry.");
            ShapeCases.Reject(() => NativeId65FreshDiscPlan.CaptureAsync(Unopened, body, 19).GetAwaiter().GetResult(),
                check, "Old factory remains textured-refusing before I/O.", "textured");
            check(disc.CopyBody().SequenceEqual(body.CopyLevelData()) && disc.CopyOverlay().SequenceEqual(legacy.CopyOverlay()) &&
                disc.CopyExecutable().SequenceEqual(legacy.CopyExecutable()) &&
                Hash(disc.CopyIsoRootDirectory()) == Text(row, "root_sha256") && Hash(disc.CopyWadDirectory()) == Text(row, "wad_sha256") &&
                disc.FinalExecutableLba == row.GetProperty("final_executable_lba").GetInt32(),
                "Exact body, unchanged EXE/overlay, both endian metadata records and old bounded geometry agree.");
            check(!disc.RuntimeVerified && !disc.PublicationApproved && !disc.CompleteNativeDependencyClosureProven &&
                !disc.CheckpointOrForeignStateSupported && !disc.NormalCreateBinEnabled && !texture.HqRuntimeProven,
                "Complete raw bytes do not promote HQ, runtime, save, exporter or publication authority.");
            FullPhysical(path, disc, Text(row, "output_sha256"), row.GetProperty("changed_sectors").GetInt32(), check);
            if (ordinal++ == 0)
            {
                var equivalent = await NativeId65NormalBodyPlan.CaptureTexturedAsync(path, texture, staging);
                check(!ReferenceEquals(equivalent, body) && equivalent.PlanSha256 == body.PlanSha256 &&
                    equivalent.CopyLevelData().SequenceEqual(body.CopyLevelData()), "Existing body equivalent-issuance semantics remain accepted.");
                ShapeCases.Reject(() => disc.RequireAncestry(equivalent), check,
                    "New Disc instance ancestry is reference identity, not a retroactive body-pairing policy.");
                var own = await NativeId65FreshDiscPlan.CaptureTexturedAsync(path, equivalent, 19);
                own.RequireAncestry(equivalent);
                check(own.PlanSha256 == disc.PlanSha256 && ReferenceEquals(own.BodyAncestor, equivalent),
                    "Equivalent actual body may issue its own deterministic Disc binding.");
                using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
                try { await NativeId65FreshDiscPlan.CaptureTexturedAsync(Unopened, body, 19, cancelled.Token); throw new InvalidDataException("Cancellation ignored."); }
                catch (OperationCanceledException e) { check(e.CancellationToken == cancelled.Token, "Actual cancellation precedes opening an invalid source."); }
                foreach (int track in new[] { -1, 30, 35, 48, int.MaxValue })
                    ShapeCases.Reject(() => NativeId65FreshDiscPlan.CaptureTexturedAsync(Unopened, body, track).GetAwaiter().GetResult(),
                        check, "Unselectable native track cannot authorize path I/O.");
                await StreamCases.Run(path, disc, Text(row, "output_sha256"), check);
                byte[] originalExe = disc.Executable.RevertExact(disc.CopyExecutable());
                check(MusicTrackCatalog.SelectableTracks.Select(t => t.TrackId).SequenceEqual(
                    golden.GetProperty("executable_tracks").EnumerateObject().Select(e => int.Parse(e.Name))),
                    "Selectable tracks are discovered from the actual catalog; no assumed count or fake music graph.");
                foreach (var track in MusicTrackCatalog.SelectableTracks)
                {
                    var executable = NativeId65FreshExecutablePlan.Compile(originalExe, track.TrackId);
                    check(executable.FinalSha256 == golden.GetProperty("executable_tracks").GetProperty(track.TrackId.ToString()).GetString(),
                        "Whole independently composed EXE and exact reverse stages for actual selectable track.");
                    check(executable.RevertExact(executable.CopyExecutable()).SequenceEqual(originalExe), "All music alternatives preserve exact original inverse.");
                }
            }
            Console.WriteLine($"TEXTURED_DISC case={name} plan={disc.PlanSha256} output={Text(row, "output_sha256")}");
        }
        var maxSurface = await NativeId65AuthoredSurfaceBundle.CaptureAsync(path, new(63*4096, 63*4096, 63*4096, 16, 16, 0x708050, 0x406080));
        var maxResources = await NativeId65NormalResourceBundle.CaptureAsync(path, maxSurface, companion);
        var maxScene = await NativeId65FreshSceneBundle.CaptureAsync(path, NativeId65NormalResourceChain.Compile(maxResources, lists),
            new(63*4096+1, 63*4096+1, 63*4096+512, 64), 16);
        var maxStage = await NativeId65NormalUploadStaging.CaptureAsync(path, maxScene);
        var maxBody = await NativeId65NormalBodyPlan.CaptureAsync(path, maxScene, maxStage);
        check(maxBody.DataUsedByteLength == 0xA38 && maxBody.ResourceProfile == NativeId65NormalResourceProfile.UntexturedGouraudTwoTriangles,
            "Actual maximum-cell untextured body shares A38 with texture data.");
        ShapeCases.Reject(() => NativeId65FreshDiscPlan.CaptureTexturedAsync(Unopened, maxBody, 19).GetAwaiter().GetResult(), check,
            "Actual same-length untextured body cannot enter new textured branch before I/O.", "textured");
        _ = await NativeId65FreshDiscPlan.CaptureAsync(path, maxBody, 19);
        check(true, "Existing variable untextured factory still accepts its own actual maximum-cell body.");
    }

    internal static void FullPhysical(string path, NativeId65FreshDiscPlan plan, string expectedHash, int expectedChanges, Action<bool, string> check)
    {
        using var input = File.OpenRead(path);
        using var forward = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var inverse = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] raw = new byte[2352]; int changed = 0;
        for (int lba = 0; lba < Size/2352; lba++)
        {
            input.ReadExactly(raw); byte[] after = plan.CreateExpectedRawSector(lba, raw);
            forward.AppendData(after); inverse.AppendData(plan.CreateOriginalRawSector(lba, after));
            changed += after.AsSpan().SequenceEqual(raw) ? 0 : 1;
            if (lba is 0 or 22 or 37 or 53874 or 53875 or 55124 or 60000)
            {
                byte[] bad = after.ToArray(); bad[24] ^= 1;
                ShapeCases.Reject(() => plan.CreateOriginalRawSector(lba, bad), check, "Inverse rejects complete changed and untouched afterimage mutations.");
                byte[] wrong = raw.ToArray(); wrong[24] ^= 1;
                ShapeCases.Reject(() => plan.CreateExpectedRawSector(lba, wrong), check, "Every source raw sector remains same-pass authenticated.");
            }
        }
        check(input.Position == Size && input.Length == Size && input.ReadByte() == -1 &&
            Convert.ToHexStringLower(forward.GetHashAndReset()) == expectedHash && changed == expectedChanges &&
            Convert.ToHexStringLower(inverse.GetHashAndReset()) == Original,
            "Full raw forward/inverse, physical complement, actual EOF and independent whole-image golden.");
    }

    private static string Text(JsonElement e, string name) => e.GetProperty(name).GetString()!;
    internal static string Hash(ReadOnlySpan<byte> b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void RequireOriginal(FileStream f)
    {
        if (f.Length != Size) throw new InvalidDataException("Original length changed.");
        f.Position = 0;
        if (Convert.ToHexStringLower(SHA256.HashData(f)) != Original || f.Position != Size || f.ReadByte() != -1 || f.Length != Size)
            throw new InvalidDataException("Original complete hash/true EOF changed.");
    }
}
