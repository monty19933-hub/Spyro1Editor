using System.Buffers.Binary;
using System.Collections;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

internal static class NativeBodyCases
{
    internal static readonly int[] AuxiliaryLengths = [0x49800, 0x42800, 0x5D000, 0x20800];
    internal static readonly int[][] AuxiliaryHeaders =
    [
        [0, 0x24, 0x234C0, 0x234E4, 0x21E20, 0x45304, 0x2268, 0x4756C, 0x2220],
        [0, 0x24, 0x1FF00, 0x1FF24, 0x1E6D4, 0x3E5F8, 0x2268, 0x40860, 0x1EF0],
        [0, 0x24, 0x2D980, 0x2D9A4, 0x2A0DC, 0x57A80, 0x2268, 0x59CE8, 0x2B68],
        [0, 0x24, 0x6F00, 0x6F24, 0x16BDC, 0x1DB00, 0x2268, 0x1FD68, 0xA80]
    ];

    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        byte[] upload = Pattern(0xDE000, 3);
        byte[] data = Pattern(0x8C8, 17);
        byte[] model = Pattern(0xF800, 29);
        byte[] scene = Scene(16);
        byte[][] auxiliary = Enumerable.Range(0, 4).Select(Auxiliary).ToArray();
        var layout = NativeId65NormalBodyLayout.Compile(upload, data, model, scene, auxiliary);
        byte[] expected = Assemble(upload, data, model, scene, auxiliary);
        check(!layout.SourceCertified && layout.CopyLevelData().SequenceEqual(expected),
            "neutral packing emits exact actual bytes but cannot issue native source authority");
        check(expected.Length == 0x1FB800 && layout.LevelDataSha256 == Hash(expected),
            "four retained auxiliary allocations are real bytes, not zero header placeholders");
        CheckNativeHeader(expected, scene.Length, check);
        check(layout.DataUsedByteLength == 0x8C8 && layout.DataSectorByteLength == 0x1000,
            "native used-data end differs from the padded CD descriptor length");
        check(layout.Descriptors.Count == 10 && layout.ModelRoots.Count == 2 &&
            layout.ModelRoots[0].SlotIndex == 0 && layout.ModelRoots[0].ClassId == null &&
            layout.ModelRoots[1].SlotIndex == 1 && layout.ModelRoots[1].ClassId == 0x78,
            "special root0 and generic local companion have separate typed registration owners");
        check(layout.ModelRoots[0].PayloadByteLength == 0xCC30 &&
            layout.ModelRoots[0].RegistrationAllocationByteLength == 0xCC30 &&
            layout.ModelRoots[1].PayloadByteLength == 0x2930 &&
            layout.ModelRoots[1].RegistrationAllocationByteLength == 0x2BD0 &&
            layout.ModelRoots[1].ModelRelativeOffset + layout.ModelRoots[1].RegistrationAllocationByteLength == 0xF800,
            "companion payload and complete native registration allocation distinguish retained padding");
        int cursor = 0;
        foreach (var owner in layout.Owners)
        {
            check(owner.Offset == cursor && owner.ByteLength > 0 &&
                Hash(expected.AsSpan(owner.Offset, owner.ByteLength)) == owner.Sha256,
                "typed output owners partition all actual bytes with exact owner hashes");
            cursor += owner.ByteLength;
        }
        check(cursor == expected.Length, "no unowned output complement remains");
        for (int index = 0; index < 4; index++)
        {
            var descriptor = layout.Descriptors[index + 4];
            check(expected.AsSpan(descriptor.Offset, descriptor.ByteLength).SequenceEqual(auxiliary[index]),
                "auxiliary model/audio/track allocation and sector padding remain complete");
            layout.RequireAuxiliarySelector(index);
        }
        foreach (int selector in new[] { -1, 4, 5, 6, int.MinValue, int.MaxValue })
            reject(() => layout.RequireAuxiliarySelector(selector),
                "unsupported auxiliary selector refuses structural resolution, not an inserted native guard");
        foreach (int at in new[] { 0, 0x20, 0x40, 0x50, 0x54, 0x150, 0x152, 0x1D0, 0x7FF,
            0xDE800, 0xDF0C8, 0xDF7FF, 0xDF800, 0xEF000, 0xF2000, expected.Length - 1 })
        {
            byte[] changed = expected.ToArray();
            changed[at] ^= 1;
            reject(() => layout.RequireExactLevelData(changed), "changed header, padding, normal or auxiliary byte refuses");
        }
        reject(() => layout.RequireExactLevelData(expected[..^1]), "truncated final allocation refuses");
        reject(() => layout.RequireExactLevelData(expected.Concat(new byte[1]).ToArray()), "foreign suffix refuses");
        layout.RequireExactLevelData(expected);
        upload[0] ^= 1;
        data[0] ^= 1;
        model[0] ^= 1;
        scene[0] ^= 1;
        auxiliary[0][0x24] ^= 1;
        byte[] exposed = layout.CopyLevelData();
        exposed[0] ^= 1;
        check(layout.CopyLevelData().SequenceEqual(expected), "all input/output snapshots are immutable");

        foreach (int n in new[] { 1, 2, 16, 32 })
        {
            byte[] freshScene = Scene(n);
            var other = NativeId65NormalBodyLayout.Compile(Pattern(0xDE000, 3), Pattern(0x8C8, 17),
                Pattern(0xF800, 29), freshScene, Enumerable.Range(0, 4).Select(Auxiliary).ToArray());
            CheckNativeHeader(other.CopyLevelData(), freshScene.Length, check);
            check(other.CopyLevelData().Length == 0x1F8800 + freshScene.Length,
                "all supported arena capacities derive subsequent auxiliary offsets from actual scene length");
        }
        foreach (int which in Enumerable.Range(0, 4))
        {
            byte[][] inputs = [Pattern(0xDE000, 3), Pattern(0x8C8, 17), Pattern(0xF800, 29), Scene(16)];
            inputs[which] = inputs[which][..^1];
            reject(() => NativeId65NormalBodyLayout.Compile(inputs[0], inputs[1], inputs[2], inputs[3],
                Enumerable.Range(0, 4).Select(Auxiliary).ToArray()), "fixed grammar bounds precede copying and parsing");
        }
        foreach (int index in Enumerable.Range(0, 4))
        {
            foreach (int field in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 })
            {
                byte[][] invalid = Enumerable.Range(0, 4).Select(Auxiliary).ToArray();
                Put(invalid[index], field * 4, int.MaxValue);
                reject(() => NativeId65NormalBodyLayout.Compile(Pattern(0xDE000, 3), Pattern(0x8C8, 17),
                    Pattern(0xF800, 29), Scene(16), invalid), "auxiliary nested owner fields cannot overflow or escape allocation");
            }
        }
        foreach (int count in new[] { 0, 3, 5 })
            reject(() => NativeId65NormalBodyLayout.Compile(Pattern(0xDE000, 3), Pattern(0x8C8, 17),
                Pattern(0xF800, 29), Scene(16), Enumerable.Range(0, count).Select(i => Auxiliary(i % 4)).ToArray()),
                "exact four retained auxiliary owners are mandatory in this fixed profile");
        foreach (var hostile in new[] { new HostileAuxiliaryList(-1), new HostileAuxiliaryList(int.MaxValue), new HostileAuxiliaryList(4) })
            reject(() => NativeId65NormalBodyLayout.Compile(Pattern(0xDE000, 3), Pattern(0x8C8, 17),
                Pattern(0xF800, 29), Scene(16), hostile), "bounded auxiliary snapshot rejects false or nonterminating counts");
        reject(() => NativeId65NormalBodySource.ValidateCapturedBytes([], [], []), "empty byte metadata is not source authority");
        reject(() => NativeId65NormalBodyPlan.CaptureAsync("unopened.bin", null!, null!).GetAwaiter().GetResult(),
            "missing private upstream inputs refuse before original-source I/O");
    }

    internal static async Task Source(string path, NativeId65CompanionAssetPlan companion,
        NativeId65FreshSceneBundle first, NativeId65FreshSceneBundle second,
        Action<bool, string> check, Action<Action, string> reject)
    {
        var stage = await NativeId65NormalUploadStaging.CaptureAsync(path, first);
        var secondStage = await NativeId65NormalUploadStaging.CaptureAsync(path, second);
        var plan = await NativeId65NormalBodyPlan.CaptureAsync(path, first, stage);
        var repeat = await NativeId65NormalBodyPlan.CaptureAsync(path, first, stage);
        var other = await NativeId65NormalBodyPlan.CaptureAsync(path, second, secondStage);
        byte[] exe = companion.EngineProfile.CopyExecutable();
        byte[] overlay = companion.EngineProfile.CopyOverlay();
        byte[] town = companion.EngineProfile.CopyTownData();
        byte[][] auxiliary = Enumerable.Range(4, 4).Select(i =>
            town.AsSpan(Get(town, i * 8), Get(town, i * 8 + 4)).ToArray()).ToArray();
        byte[] expected = Assemble(first.CopyUploadPayload(), first.CopyDataComponents(),
            first.CopyModelPayload(), first.CopyScene(), auxiliary);
        check(plan.CopyLevelData().SequenceEqual(expected) && plan.CopyEngineOverlay().SequenceEqual(overlay),
            "full source-bound body and engine match independent assembly from separately captured owners");
        check(plan.LevelDataSha256 == "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c" &&
            other.LevelDataSha256 == "1314b299759ad0f66eed054db853c18e8db6dabda7c56a28cabd3fd74ea39643" &&
            Hash(expected.AsSpan(0, 0x800)) == "cd5658f83da4a953c5a3e66c848a8be606aabde9fea8b32d049c2bcf364c472d",
            "both complete bodies and full fresh header match independent raw-sector oracle before production implementation");
        check(plan.SourceCertified && !plan.CallbacksInstalled && !plan.CompleteNativeDependencyClosureProven &&
            !plan.CompleteLevelResourcesProven && !plan.IndependentLevelSupported && !plan.PublicationApproved &&
            !plan.RuntimeVerified && !plan.PeakRuntimeMemoryProven && !plan.TransferTimingProven &&
            !plan.SpuWrapSemanticsProven && !plan.RescueReplayRuntimeProven,
            "an actual native header/body is not an installed engine or complete hardware/gameplay certificate");
        check(plan.UpstreamPlanSha256 == first.PlanSha256 && plan.StagingPlanSha256 == stage.PlanSha256 &&
            plan.PlanSha256 == repeat.PlanSha256 && plan.LevelDataSha256 == Hash(expected),
            "private preceding producer identities bind even byte-identical repeated source captures");
        check(other.PlanSha256 != plan.PlanSha256 && other.LevelDataSha256 != plan.LevelDataSha256 &&
            other.CopyEngineOverlay().SequenceEqual(overlay), "distinct scene intent produces distinct complete body without rewriting native code");
        reject(() => NativeId65NormalBodyPlan.CaptureAsync(path, second, stage).GetAwaiter().GetResult(),
            "equal upload bytes cannot rebind staging issued for a different complete scene intent");
        check(plan.Callbacks.Count == 8 && plan.CopyBufferAddress == 0x80089ECC &&
            plan.ModelRuntimeAddress == 0x8008A794 && plan.SceneRuntimeAddress == 0x80099F94 &&
            plan.KnownSceneEndAddress == 0x8009CF94 && plan.NormalReservedLowerAddress == 0x80187BB0,
            "source-issued exact anchors expose known extents but not peak runtime fit");
        check(plan.RequiredInitializerRuntimeAddress == 0x8005A898 && plan.RequiredOverlayRuntimeAddress == 0x8007AA38 &&
            plan.RequiresInitializerBeforeOverlay && plan.RequiresOverlayRead && !plan.OverlaySkipModeSupported &&
            !plan.CheckpointOrForeignStateSupported && !plan.NoOpCallbacksAuthorized,
            "missing installation, stale-pointer and foreign-entry modes cannot silently gain authority");
        CheckNativeHeader(expected, first.CopyScene().Length, check);
        var reads = plan.ReadPhases;
        check(reads.Count == 9 && reads.Select(r => r.Id).SequenceEqual(new[]
        {
            "overlay-install", "header-read", "vram-read", "bank-read", "cpu-spu-source",
            "dma-spu-source", "data-read", "model-read", "scene-read"
        }), "native temporal read domains include separate CPU and DMA observations");
        check(reads[0].RuntimeAddress == 0x8007AA38 && reads[0].ByteLength == 0xF800 &&
            reads[1].RuntimeAddress == 0x80089ECC && reads[1].ByteLength == 0x800 &&
            reads[4].ByteLength == 0x7EFF0 && reads[5].ByteLength == 0x7F000,
            "overlay tail overwrite and CPU versus rounded DMA source size are preserved");
        check(plan.PhaseOverlaps.Count == 2 && plan.PhaseOverlaps[0].RuntimeAddress == 0x80089ECC &&
            plan.PhaseOverlaps[0].ByteLength == 0x36C && plan.PhaseOverlaps[1].RuntimeAddress == 0x8008A794 &&
            plan.PhaseOverlaps[1].ByteLength == 0x738,
            "the two non-staging source-lifetime intersections are explicit, not permanent disjointness");
        NativeBodySourceWindows.ReadRequests(exe, expected, plan, check, reject);
        NativeId65NormalBodySource.ValidateCapturedBytes(exe, overlay, town);
        foreach (var (domain, bytes, offsets) in new[]
        {
            (0, exe, new[] { 0, 0x1BA0, 0x1CA8, 0x4B098, 0x4BEE0, 0x5DE4, 0x4328, exe.Length - 1 }),
            (1, overlay, new[] { 0, 0x8580, 0xD1D0, 0xF494, overlay.Length - 1 }),
            (2, town, new[] { 0, 0x20, 0x40, 0x1D8800, 0x222000, 0x264800, 0x2C1800, town.Length - 1 })
        })
        {
            foreach (int at in offsets)
            {
                byte[] bad = bytes.ToArray();
                bad[at] ^= 1;
                reject(() => NativeId65NormalBodySource.ValidateCapturedBytes(domain == 0 ? bad : exe,
                    domain == 1 ? bad : overlay, domain == 2 ? bad : town),
                    "exact source consumers, callback/padding owners and complete source complement are rebound");
            }
        }
        foreach (int at in new[] { 0, 0x20, 0x40, 0x50, 0x152, 0x1D0, 0xDF0C8, 0xEF000, expected.Length - 1 })
        {
            byte[] changed = expected.ToArray();
            changed[at] ^= 1;
            reject(() => plan.RequireExactPayloads(changed, overlay), "complete final body readback refuses any changed owner");
        }
        foreach (int at in new[] { 0, 0xF494, overlay.Length - 1 })
        {
            byte[] changed = overlay.ToArray();
            changed[at] ^= 1;
            reject(() => plan.RequireExactPayloads(expected, changed), "disk overlay tail is retained byte-exact despite its later RAM reuse");
        }
        plan.RequireExactPayloads(expected, overlay);
        check(first.SceneSha256 == "4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433" &&
            first.UploadSha256 == "b769a9a298285aa402956fdbd5721132736388c6f3f1dc1bfcd7e15a9afeb66d" &&
            first.ModelSha256 == "88c78343c283256de6ce8d3151b43fa72ba4d96b046d869bd0a7209cbff4c23d" &&
            stage.TransferSourceSha256 == "626ae349b98cb4bea955ac53020586f645f872225085a67bc809f46f4ab0fb07",
            "all established fresh resource/scene/staging byte goldens remain unchanged");
        Console.WriteLine($"BODY bytes={expected.Length} sha256={plan.LevelDataSha256} other={other.LevelDataSha256} plan={plan.PlanSha256}");
    }

    internal static byte[] Assemble(byte[] upload, byte[] data, byte[] model, byte[] scene, byte[][] auxiliary)
    {
        byte[] padded = new byte[(data.Length + 0x7FF) & ~0x7FF];
        data.CopyTo(padded, 0);
        byte[][] parts = new[] { upload, padded, model, scene }.Concat(auxiliary).ToArray();
        byte[] output = new byte[0x800 + parts.Sum(p => p.Length)];
        int cursor = 0x800;
        for (int i = 0; i < parts.Length; i++)
        {
            Put(output, i * 8, cursor);
            Put(output, i * 8 + 4, parts[i].Length);
            parts[i].CopyTo(output, cursor);
            cursor += parts[i].Length;
        }
        Put(output, 0x50, Get(output, 0x10));
        Put(output, 0x54, Get(output, 0x10) + 0xCC30);
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(0x152), 0x78);
        return output;
    }

    private static void CheckNativeHeader(byte[] body, int sceneLength, Action<bool, string> check)
    {
        check(Get(body, 0) == 0x800 && Get(body, 4) == 0xDE000 && Get(body, 8) == 0xDE800 &&
            Get(body, 12) == 0x1000 && Get(body, 16) == 0xDF800 && Get(body, 20) == 0xF800 &&
            Get(body, 24) == 0xEF000 && Get(body, 28) == sceneLength, "independent native four-descriptor grammar");
        check(Get(body, 0x50) == 0xDF800 && Get(body, 0x54) == 0xEC430 &&
            BinaryPrimitives.ReadInt16LittleEndian(body.AsSpan(0x152)) == 0x78,
            "native root words are entry-relative while class IDs use signed halfwords");
        check(body.AsSpan(0x40, 0x10).ToArray().All(b => b == 0) &&
            body.AsSpan(0x58, 0xF8).ToArray().All(b => b == 0) &&
            body.AsSpan(0x154, 0x6AC).ToArray().All(b => b == 0),
            "absent source slots and remaining header complement are explicit zeros, not absent-selector authority");
        int cursor = 0xEF000 + sceneLength;
        for (int i = 0; i < 4; i++)
        {
            check(Get(body, (i + 4) * 8) == cursor && Get(body, (i + 4) * 8 + 4) == AuxiliaryLengths[i],
                "every auxiliary descriptor resolves its complete sequential owner");
            cursor += AuxiliaryLengths[i];
        }
        check(cursor == body.Length && body.AsSpan(0xDF0C8, 0x738).ToArray().All(b => b == 0),
            "fresh sector padding is complete and not counted as native used-data storage");
    }

    private static byte[] Auxiliary(int index)
    {
        byte[] bytes = Pattern(AuxiliaryLengths[index], 61 + index);
        for (int i = 0; i < 9; i++)
            Put(bytes, i * 4, AuxiliaryHeaders[index][i]);
        return bytes;
    }

    private static byte[] Scene(int n)
    {
        int props = 0xD4 + 0x70 * n + 0x58;
        int end = props + 4 + 8 + 4 + 0x2000 + 4;
        byte[] scene = new byte[(end + 0x7FF) & ~0x7FF];
        int cursor = 0x88;
        foreach (int length in new[] { 12, 12, 8, 8, 12, 8, 8 })
        {
            Put(scene, cursor, length);
            cursor += length;
        }
        Put(scene, 0xCC, 8 + 0x70 * n + 0x58);
        Put(scene, props, 4);
        Put(scene, props + 4, 8);
        Put(scene, props + 12, 0x2004);
        return scene;
    }

    private static byte[] Pattern(int length, int seed) => Enumerable.Range(0, length).Select(i => (byte)(i * 73 + (i >> 8) + seed)).ToArray();
    internal static int Get(byte[] bytes, int at) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at));
    internal static void Put(byte[] bytes, int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at), value);
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed class HostileAuxiliaryList(int count) : IReadOnlyList<byte[]>
    {
        public int Count => count;
        public byte[] this[int index] => throw new Exception("untrusted indexer must not be used");
        public IEnumerator<byte[]> GetEnumerator()
        {
            for (int i = 0; i < 5; i++)
                yield return Auxiliary(i % 4);
            throw new Exception("bounded snapshot must stop before the sixth request");
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
