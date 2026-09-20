using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Spyro.Editor.Core.Analysis;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65NormalBodySmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Private exact-original issuer. Captured byte validation does not issue a
/// public source object, engine installation, state closure, or retail level.
/// </summary>
internal sealed class NativeId65NormalBodySource
{
    private const string ExecutableSha256 = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private const string OverlaySha256 = "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
    private readonly byte[] _executable;
    private readonly byte[] _overlay;
    private readonly byte[] _townData;
    private readonly byte[][] _auxiliary;

    private static readonly NativeId65EngineEvidence[] Consumers =
    [
        new("resident-initializer-dispatch-before-incoming-overlay", "EXE", 0x4AC70, 0x30, "7dba88624efe091d8b34a22b8874a2d12ab577858f7583a324d82f11d14d5c4f"),
        new("exact-Town-nine-setter-initializer", "EXE", 0x4B098, 0x98, "fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b"),
        new("default-tail-and-normal-pool-anchor-arithmetic", "EXE", 0x4BEE0, 0xA0, "f1d94157410333d6640a99043f9e175f6a41c2150a5bfa47028fdf4f54626be4"),
        new("incoming-ID-initializer-then-conditional-overlay-read", "EXE", 0x5DE4, 0xD0, "d9463bf56b413815177dca0d36e4efa372754cce5ec53876c38e6495f1291f3a"),
        new("header-read-and-complete-descriptor-root-class-copy", "EXE", 0x5EB4, 0x70, "9083cbef89b25242064afd41f40231bfa0afc41218d6cac1ee17778bf76a88db"),
        new("used-data-end-return-and-model-CD-owner", "EXE", 0x6088, 0x1CC, "8fcb62ff217d641423a6255ea234011e8f4b5fe7769c53ae78277ad8373609ca"),
        new("local-model-clear-registration-and-scene-request", "EXE", 0x6254, 0x180, "a51ae4c014934f0a456520247ebcb1333c77a92670ebf18bbcada0162e8a7fb0"),
        new("normal-scene-entry-call-and-initialization-order", "EXE", 0x64CC, 0xC4, "1acbe741fd6f43ba61514452be82e85ae0cdfaad94e693b6462781e938aada49"),
        new("scene-reset-preserves-state12", "EXE", 0x4308, 0x80, "2115215a638bfd22821db28d414d9b939883ec492ea68c2323c34981fa633d9d"),
        new("particle-auxiliary-reset-before-null-source-companion", "EXE", 0x4C1C, 0xAC, "d13cd3912cf5ac824dd3be4b30db536f4941bc86b3e601056cd13c9849d2d1ce"),
        new("normal-load-finalization-not-foreign-state-authority", "EXE", 0x6AD4, 0xE8, "1a40ee2337ae1f946500543ac5a46e26281c61bbc4ded7201cff90294c99563b"),
        new("ordinary-frame-callback-and-player-schedule", "EXE", 0x24254, 0xF8, "bee44329f0311ddc45a50a3c5b32421c68a454280c83586d778a58c9e5948ccf"),
        new("cold-boot-BSS-zero-loop-not-transition-reset", "EXE", 0x4C0E0, 0x24, "7896dd616818db767dc8a988879948e6011c1b4c1600aff3762ec7dc645c1f48"),
        new("shared-companion-auxiliary-allocator-reset-and-native-full-return", "EXE", 0x492E8, 0xC0, "cc396c20757c749bebcf0f448e0654b3fed04131e4f51baa76ceefd6baa11879"),
        new("auxiliary-selector-subfile-read-model-SPU-restore", "EXE", 0x5370, 0x800, "609e6369854f37e17c32a5c5fdff1eca5f12e101dcdd8cc971c4b94dc542ef26"),
        new("original-rescue-entry-and-properties-selector", "EXE", 0x1D124, 0x248, "8f24d37e489577218325bab7ec639dc51a720890da1c7c729526507cea0702e7"),
        new("separate-replay-entry-and-context-owner", "EXE", 0x1D4C8, 0xAC, "04cdf4cf30b2e5907d82b7501483051b32ecbd07e01857db7b0a464915ad658b"),
        new("replay-linked-row-route-to-original-rescue", "EXE", 0x22790, 0xA0, "873d8ca60ac90d0db365522e33e27eb348556072db82018775ac40c6d5d14485")
    ];

    internal static ReadOnlySpan<int> AuxiliarySourceOffsets => [0x1D8800, 0x222000, 0x264800, 0x2C1800];
    private static readonly string[] AuxiliaryHashes =
    [
        "bdf27988f3d2b0fd4c4b905636b6980aff49ab1475d85edb4ce8aa2cc469351f",
        "23968146e46b3263997e16692a192bbdb049966b010673d17611f2ab6903cef0",
        "402bb40e21bf84922cbc35312b0e49b95292a2f96326636aba05685443686991",
        "eabadb4ea663043665bc4ba2d2e00c1c7c104688906b56bac2d7aba7c6cae8da"
    ];

    private NativeId65NormalBodySource(NativeId65EngineProfileContract engine)
    {
        _executable = engine.CopyExecutable();
        _overlay = engine.CopyOverlay();
        _townData = engine.CopyTownData();
        NativeId65DetachedEngineInitializer initializer = ValidateCapturedBytes(_executable, _overlay, _townData);
        if (initializer.CopyBufferAddress != engine.CopyBufferAddress ||
            !initializer.Callbacks.SequenceEqual(engine.Callbacks))
            throw new InvalidDataException("Body capture lost its private source-issued engine initializer identity.");
        CopyBufferAddress = initializer.CopyBufferAddress;
        Callbacks = Array.AsReadOnly(initializer.Callbacks.ToArray());
        _auxiliary = new byte[4][];
        var evidence = engine.Evidence.Concat(Consumers).ToList();
        for (int i = 0; i < 4; i++)
        {
            _auxiliary[i] = _townData.AsSpan(AuxiliarySourceOffsets[i],
                NativeId65NormalBodyLayout.AuxiliaryByteLengths[i]).ToArray();
            evidence.Add(new($"complete-retained-Town-auxiliary-{i}", "WAD",
                0x119E000L + AuxiliarySourceOffsets[i], _auxiliary[i].Length, AuxiliaryHashes[i]));
        }
        evidence.Add(new("originally-overwritten-overlay-tail-not-indirect-lifetime-closure", "WAD",
            0x119DC94, 0x36C, "2ab9f539e8903a18a59a254fa8dc21a186b4540e40376c1c1bd6d9055949b7c9"));
        Evidence = evidence.AsReadOnly();

        // Engine.CaptureAsync calls CaptureRetail with the complete original
        // PETE header and 0x39C48-byte copied allocation. This fixed count is a
        // captured native header field, not inferred from player package size.
        const uint capturedPeteCopyByteLength = 0x39C48;
        ResidentPeteAddress = checked(0x80200000 - Word(_executable, 0x65DA4) - capturedPeteCopyByteLength);
        NormalReservedLowerAddress = checked(ResidentPeteAddress - 0x6008 - 2 * 0x1C000);
        if (ResidentPeteAddress != 0x801C5BB8 || NormalReservedLowerAddress != 0x80187BB0)
            throw new InvalidDataException("Native PETE or normal reserved-buffer address binding changed.");
    }

    internal uint CopyBufferAddress { get; }
    internal uint ResidentPeteAddress { get; }
    internal uint NormalReservedLowerAddress { get; }
    internal IReadOnlyList<NativeId65EngineCallback> Callbacks { get; }
    internal IReadOnlyList<NativeId65EngineEvidence> Evidence { get; }
    internal byte[] CopyOverlay() => _overlay.ToArray();
    internal byte[][] CopyAuxiliaryAllocations() => _auxiliary.Select(b => b.ToArray()).ToArray();

    internal static async Task<NativeId65NormalBodySource> CaptureAsync(string originalImagePath,
        CancellationToken cancellationToken)
    {
        // This private factory performs whole original-image pre/post hashing,
        // exact directory reads, engine dispatch/code and PETE registration
        // capture. A neutral validator below cannot manufacture this object.
        var engine = await NativeId65EngineProfileContract.CaptureAsync(originalImagePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(engine);
    }

    internal void RequireIssuedInputs(NativeId65NormalResourceRevision fresh, NativeId65NormalUploadStaging staging,
        ReadOnlySpan<byte> upload, ReadOnlySpan<byte> data, ReadOnlySpan<byte> model, ReadOnlySpan<byte> scene)
    {
        RequirePair(fresh, staging);
        fresh.RequireExactOutputs(upload, data, model, scene);
        if (NativeId65NormalResourceSource.Hash(data) != fresh.DataSha256 ||
            NativeId65NormalResourceSource.Hash(model) != fresh.ModelSha256 ||
            upload.Length != 0xDE000 || data.Length != fresh.DataUsedByteLength || model.Length != 0xF800 ||
            !model[..0xCC30].SequenceEqual(_townData.AsSpan(0x173000, 0xCC30)) ||
            !upload[0x80000..].SequenceEqual(_townData.AsSpan(0x80800, 0x5E000)) ||
            fresh.EntryProfile.SuperflameAvailable || fresh.EntryProfile.CheckpointStateSupported ||
            fresh.EntryProfile.ForeignStateSupported || fresh.EntryProfile.AllStateClosureProven)
            throw new InvalidDataException("Normal body requires its exact fixed private fresh-resource and conditional entry profile.");
        NativeId65FreshSceneLayout.DecodeCanonical(scene).RequireExactScene(scene);
        NativeId65FreshSceneSource.ValidateSourceExecutable(_executable);
        NativeId65NormalUploadStagingSource.ValidateSourceExecutable(_executable);
        byte[] expectedTransfer = NativeId65NormalUploadTransferLayout.Compile(upload).CopyTransferSourceImage();
        staging.RequireExactTransferSource(expectedTransfer);
    }

    internal static void RequirePair(NativeId65NormalResourceRevision fresh, NativeId65NormalUploadStaging staging)
    {
        ArgumentNullException.ThrowIfNull(fresh);
        ArgumentNullException.ThrowIfNull(staging);
        if (!staging.SourceCertified || fresh.ResourceProfile != staging.ResourceProfile ||
            fresh.SourceImageSha256 != NativeId65EngineProfileContract.OriginalImageSha256 ||
            staging.SourceImageSha256 != fresh.SourceImageSha256 ||
            staging.UpstreamPlanSha256 != fresh.PlanSha256 || staging.PairedUploadSha256 != fresh.UploadSha256)
            throw new InvalidDataException("Normal body and transfer source must share the exact privately issued upstream revision.");
        if (fresh.ResourceProfile == NativeId65NormalResourceProfile.UntexturedGouraudFlatTiles &&
            (fresh.TiledSceneAncestor == null || !ReferenceEquals(fresh.TiledSceneAncestor, staging.TiledSceneAncestor)))
            throw new InvalidDataException("Tiled body and staging must retain the same actual source-issued document/scene ancestor.");
        if ((fresh.ResourceProfile is NativeId65NormalResourceProfile.DirectLqTwoStaticMaterialsFlatTiles or
            NativeId65NormalResourceProfile.DirectLqSelectedStaticMaterialsFlatTiles) &&
            (fresh.TiledMaterialAncestor == null ||
                !ReferenceEquals(fresh.TiledMaterialAncestor, staging.TiledMaterialAncestor) ||
                !ReferenceEquals(fresh.TiledSceneAncestor, fresh.TiledMaterialAncestor.FreshAncestor) ||
                !ReferenceEquals(fresh.TiledSceneAncestor, staging.TiledSceneAncestor)))
            throw new InvalidDataException("Tiled material body and staging must retain the same actual material and fresh scene instances, not equal hashes.");
    }

    internal static NativeId65DetachedEngineInitializer ValidateCapturedBytes(ReadOnlySpan<byte> executable,
        ReadOnlySpan<byte> overlay, ReadOnlySpan<byte> townData)
    {
        if (executable.Length != 0x66000 || overlay.Length != 0xF800 || townData.Length != 0x2E2000)
            throw new InvalidDataException("Body source requires complete bounded original EXE, overlay and Town owners.");
        NativeId65NormalResourceSource.RequireHash(executable, ExecutableSha256, "complete body source executable");
        NativeId65NormalResourceSource.RequireHash(overlay, OverlaySha256, "complete body source overlay");
        NativeId65NormalResourceSource.RequireHash(townData, NativeId65EngineProfileDecoder.TownDataSha256,
            "complete body source Town data");
        foreach (var consumer in Consumers)
            NativeId65NormalResourceSource.RequireHash(executable.Slice(checked((int)consumer.Offset), consumer.ByteLength),
                consumer.Sha256, consumer.Name);
        if (Word(executable, 0x1BA0) != 0x8007AA38 || Word(executable, 0x1CA8) != 0x8005B6E0)
            throw new InvalidDataException("Body source cannot replace the native overlay base or promote retail ID65 default dispatch.");
        var initializer = NativeId65EngineProfileDecoder.DecodeInitializer(executable.Slice(0x4B098, 0x98),
            executable.Slice(0x4BEE0, 0x18));
        foreach (var callback in initializer.Callbacks)
        {
            int offset = checked((int)(callback.RuntimeAddress - 0x8007AA38));
            if (offset < 0 || callback.CodeByteLength <= 0 ||
                offset + (long)callback.CodeByteLength > 0xF494)
                throw new InvalidDataException("Required callback allocation crosses the native copy anchor.");
            NativeId65NormalResourceSource.RequireHash(overlay.Slice(offset, callback.CodeByteLength),
                callback.CodeSha256, callback.Name);
        }
        NativeId65NormalResourceSource.RequireHash(overlay[0xF494..],
            "2ab9f539e8903a18a59a254fa8dc21a186b4540e40376c1c1bd6d9055949b7c9", "originally overwritten overlay tail");
        for (int i = 0; i < 4; i++)
        {
            int length = NativeId65NormalBodyLayout.AuxiliaryByteLengths[i];
            if (Word(townData, (i + 4) * 8) != AuxiliarySourceOffsets[i] ||
                Word(townData, (i + 4) * 8 + 4) != length)
                throw new InvalidDataException("Retained auxiliary descriptor differs from its exact original allocation.");
            NativeId65NormalResourceSource.RequireHash(townData.Slice(AuxiliarySourceOffsets[i], length),
                AuxiliaryHashes[i], "complete retained auxiliary allocation");
        }
        if (townData.Slice(0x40, 0x10).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Source-absent descriptors8/9 changed; absence is not a native range guard.");
        return initializer;
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
