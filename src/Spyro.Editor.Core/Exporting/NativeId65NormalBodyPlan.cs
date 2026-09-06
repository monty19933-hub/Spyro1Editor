using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeId65NormalBodyDescriptor(int Index, int Offset, int ByteLength, bool RetainedAuxiliary);
public sealed record NativeId65NormalBodyRoot(int SlotIndex, int EntryRelativeOffset,
    int ModelRelativeOffset, int PayloadByteLength, int RegistrationAllocationByteLength, ushort? ClassId);
public sealed record NativeId65NormalBodyOwner(string Id, int Offset, int ByteLength,
    string Sha256, string Provenance, long? SourceWadOffset);
public sealed record NativeId65AuxiliaryBodyOwner(int DescriptorIndex, string Id, int AuxiliaryRelativeOffset,
    int BodyRelativeOffset, int ByteLength, string Sha256);
public sealed record NativeId65NormalBodyReadPhase(string Id, uint RuntimeAddress, int ByteLength,
    string NativeOperation, bool RequiresExternalCompletion);
public sealed record NativeId65NormalBodyPhaseOverlap(string EarlierOwner, string LaterOwner,
    uint RuntimeAddress, int ByteLength, string Limitation);

/// <summary>
/// Neutral fixed packing kernel. It owns bytes but does not certify their
/// native model/texture contents or source provenance. Only the private source
/// plan accepts source-issued upstream objects and captured auxiliary owners.
/// </summary>
internal sealed class NativeId65NormalBodyLayout
{
    internal static ReadOnlySpan<int> AuxiliaryByteLengths => [0x49800, 0x42800, 0x5D000, 0x20800];
    private readonly byte[] _body;

    private NativeId65NormalBodyLayout(ReadOnlySpan<byte> upload, ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> model, ReadOnlySpan<byte> scene, byte[][] auxiliary)
    {
        DataUsedByteLength = data.Length;
        byte[] paddedData = new byte[DataSectorByteLength];
        data.CopyTo(paddedData);
        byte[][] parts = [upload.ToArray(), paddedData, model.ToArray(), scene.ToArray(), .. auxiliary];
        _body = new byte[checked(0x800 + parts.Sum(p => p.Length))];
        int cursor = 0x800;
        var descriptors = new List<NativeId65NormalBodyDescriptor>(10);
        for (int i = 0; i < parts.Length; i++)
        {
            Put(i * 8, cursor);
            Put(i * 8 + 4, parts[i].Length);
            parts[i].CopyTo(_body, cursor);
            descriptors.Add(new(i, cursor, parts[i].Length, i >= 4));
            cursor = checked(cursor + parts[i].Length);
        }
        descriptors.Add(new(8, 0, 0, false));
        descriptors.Add(new(9, 0, 0, false));
        Descriptors = descriptors.AsReadOnly();
        int models = Descriptors[2].Offset;
        Put(0x50, models);
        Put(0x54, models + 0xCC30);
        BinaryPrimitives.WriteUInt16LittleEndian(_body.AsSpan(0x152), 0x78);
        ModelRoots = Array.AsReadOnly(new[]
        {
            new NativeId65NormalBodyRoot(0, models, 0, 0xCC30, 0xCC30, null),
            new NativeId65NormalBodyRoot(1, models + 0xCC30, 0xCC30, 0x2930, 0x2BD0, 0x78)
        });

        var owners = new List<NativeId65NormalBodyOwner>();
        Add("fresh-native-header-and-zero-complement", 0, 0x800, "fresh-relative-native-fields");
        Add("paired-native-upload", Descriptors[0].Offset, upload.Length, "neutral-input-not-source-authority");
        Add("fresh-used-data-components", Descriptors[1].Offset, data.Length, "neutral-input-not-source-authority");
        Add("fresh-data-sector-padding", Descriptors[1].Offset + data.Length, paddedData.Length - data.Length, "fresh-zero-CD-padding");
        Add("special-player-and-generic-companion-models", models, model.Length, "neutral-input-not-source-authority");
        Add("fresh-scene-and-dynamic-arena", Descriptors[3].Offset, scene.Length, "neutral-input-not-source-authority");
        var auxiliaryOwners = new List<NativeId65AuxiliaryBodyOwner>();
        for (int i = 0; i < 4; i++)
        {
            NativeId65NormalBodyDescriptor descriptor = Descriptors[i + 4];
            Add($"complete-auxiliary-{i}", descriptor.Offset, descriptor.ByteLength, "neutral-input-not-source-authority");
            foreach (var (id, offset, length) in DecodeAuxiliaryOwners(auxiliary[i]))
                auxiliaryOwners.Add(new(i + 4, id, offset, descriptor.Offset + offset, length,
                    NativeId65NormalResourceSource.Hash(auxiliary[i].AsSpan(offset, length))));
        }
        Owners = owners.AsReadOnly();
        AuxiliaryOwners = auxiliaryOwners.AsReadOnly();
        cursor = 0;
        foreach (var owner in Owners)
        {
            if (owner.Offset != cursor || owner.ByteLength <= 0)
                throw new InvalidDataException("Normal body lost its exact contiguous byte ownership partition.");
            cursor = checked(cursor + owner.ByteLength);
        }
        if (cursor != _body.Length || _body.Length > 0x200000)
            throw new InvalidDataException("Normal body escaped the bounded detached output profile.");
        LevelDataSha256 = NativeId65NormalResourceSource.Hash(_body);

        void Put(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(_body.AsSpan(offset), value);
        void Add(string id, int offset, int length, string provenance) => owners.Add(
            new(id, offset, length, NativeId65NormalResourceSource.Hash(_body.AsSpan(offset, length)), provenance, null));
    }

    internal bool SourceCertified => false;
    internal int DataUsedByteLength { get; }
    internal int DataSectorByteLength => 0x1000;
    internal string LevelDataSha256 { get; }
    internal IReadOnlyList<NativeId65NormalBodyDescriptor> Descriptors { get; }
    internal IReadOnlyList<NativeId65NormalBodyRoot> ModelRoots { get; }
    internal IReadOnlyList<NativeId65NormalBodyOwner> Owners { get; }
    internal IReadOnlyList<NativeId65AuxiliaryBodyOwner> AuxiliaryOwners { get; }
    internal byte[] CopyLevelData() => _body.ToArray();

    internal static NativeId65NormalBodyLayout Compile(ReadOnlySpan<byte> upload, ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> model, ReadOnlySpan<byte> scene, IReadOnlyList<byte[]> auxiliary)
    {
        ArgumentNullException.ThrowIfNull(auxiliary);
        if (upload.Length != 0xDE000 || data.Length is not (0x8C8 or 0xA38) || model.Length != 0xF800 ||
            scene.Length is not (0x2800 or 0x3000) || auxiliary.Count != 4)
            throw new InvalidDataException("Normal body requires the bounded fixed fresh profile and all four auxiliary allocations.");
        NativeId65FreshSceneLayout.DecodeCanonical(scene);
        // Bound actual enumeration as well as declared Count before cloning
        // owners. Never enumerate indefinitely or trust an arbitrary indexer.
        byte[][] snapshot = auxiliary.Take(5).ToArray();
        if (snapshot.Length != 4)
            throw new InvalidDataException("Auxiliary allocation count changed during bounded snapshot.");
        for (int i = 0; i < 4; i++)
        {
            if (snapshot[i] == null || snapshot[i].Length != AuxiliaryByteLengths[i])
                throw new InvalidDataException("Auxiliary allocation length is outside the exact profile budget.");
        }
        byte[][] copies = snapshot.Select(b => b.ToArray()).ToArray();
        foreach (byte[] copy in copies)
            _ = DecodeAuxiliaryOwners(copy);
        return new(upload, data, model, scene, copies);
    }

    internal void RequireExactLevelData(ReadOnlySpan<byte> body)
    {
        if (!body.SequenceEqual(_body))
            throw new InvalidDataException("Normal body changed a header, payload, padding or untouched auxiliary owner.");
    }

    /// <summary>Structural source-owner lookup only; no native runtime guard is emitted.</summary>
    internal void RequireAuxiliarySelector(int selector)
    {
        if (selector < 0 || selector >= 4 || Descriptors[selector + 4].ByteLength <= 0)
            throw new InvalidDataException("Auxiliary selector has no retained owner in this fixed profile; native entry is not approved.");
    }

    private static IReadOnlyList<(string Id, int Offset, int Length)> DecodeAuxiliaryOwners(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 0x24 || BinaryPrimitives.ReadInt32LittleEndian(bytes) != 0)
            throw new InvalidDataException("Auxiliary header is incomplete or its source uninterpreted word changed.");
        var owners = new List<(string, int, int)> { ("auxiliary-header", 0, 0x24) };
        string[] names = ["audio", "model-one", "model-two", "track"];
        int cursor = 0x24;
        for (int i = 0; i < 4; i++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes[(4 + i * 8)..]);
            int length = BinaryPrimitives.ReadInt32LittleEndian(bytes[(8 + i * 8)..]);
            if (offset != cursor || length <= 0 || length % 4 != 0 ||
                offset + (long)length > bytes.Length || (i == 3 && length % 24 != 0))
                throw new InvalidDataException("Auxiliary resource escaped its consecutive bounded owner or native track stride.");
            owners.Add((names[i], offset, length));
            cursor = checked(offset + length);
        }
        int padding = bytes.Length - cursor;
        if (padding < 0 || padding >= 0x800)
            throw new InvalidDataException("Auxiliary complement is not its bounded complete sector padding.");
        if (padding != 0)
            owners.Add(("retained-sector-padding", cursor, padding));
        return owners.AsReadOnly();
    }
}

/// <summary>
/// Actual detached fresh normal data/header plus exact retained Town engine and
/// four explicit auxiliary allocations. This emits no WAD/EXE/ISO patch and
/// does not install callbacks or certify complete native behavior or hardware.
/// </summary>
public sealed class NativeId65NormalBodyPlan
{
    private readonly NativeId65NormalBodyLayout _layout;
    private readonly byte[] _overlay;

    private NativeId65NormalBodyPlan(NativeId65NormalBodySource source,
        NativeId65NormalResourceRevision fresh, NativeId65NormalUploadStaging staging)
    {
        byte[] upload = fresh.CopyUploadPayload();
        byte[] data = fresh.CopyDataComponents();
        byte[] model = fresh.CopyModelPayload();
        byte[] scene = fresh.CopyScene();
        source.RequireIssuedInputs(fresh, staging, upload, data, model, scene);
        _layout = NativeId65NormalBodyLayout.Compile(upload, data, model, scene, source.CopyAuxiliaryAllocations());
        _overlay = source.CopyOverlay();
        ResourceProfile = fresh.ResourceProfile;
        UpstreamPlanSha256 = fresh.PlanSha256;
        StagingPlanSha256 = staging.PlanSha256;
        CopyBufferAddress = source.CopyBufferAddress;
        ResidentPeteAddress = source.ResidentPeteAddress;
        NormalReservedLowerAddress = source.NormalReservedLowerAddress;
        ModelRuntimeAddress = checked(CopyBufferAddress + (uint)_layout.DataUsedByteLength);
        SceneRuntimeAddress = checked(ModelRuntimeAddress + (uint)model.Length);
        KnownSceneEndAddress = checked(SceneRuntimeAddress + (uint)scene.Length);
        if (KnownSceneEndAddress > NormalReservedLowerAddress)
            throw new InvalidDataException("Known normal CD-read extents intersect fixed native reserved owners.");
        Callbacks = Array.AsReadOnly(source.Callbacks.ToArray());
        Evidence = Array.AsReadOnly(source.Evidence.Concat(fresh.Evidence).Concat(staging.Evidence).ToArray());
        var owners = new List<NativeId65NormalBodyOwner>();
        foreach (var owner in _layout.Owners)
        {
            NativeId65NormalBodyDescriptor? auxiliary = _layout.Descriptors.FirstOrDefault(d =>
                d.RetainedAuxiliary && d.Offset == owner.Offset && d.ByteLength == owner.ByteLength);
            owners.Add(auxiliary == null
                ? owner with { Provenance = owner.Provenance == "neutral-input-not-source-authority"
                    ? "private-issued-fresh-resource" : owner.Provenance }
                : owner with
                {
                    Provenance = "complete-retained-Town-auxiliary-not-normal-terrain",
                    SourceWadOffset = 0x119E000L + NativeId65NormalBodySource.AuxiliarySourceOffsets[auxiliary.Index - 4]
                });
        }
        Owners = owners.AsReadOnly();
        ReadPhases = Array.AsReadOnly(new[]
        {
            new NativeId65NormalBodyReadPhase("overlay-install", RequiredOverlayRuntimeAddress, _overlay.Length, "CD writes incoming native engine allocation", true),
            new NativeId65NormalBodyReadPhase("header-read", CopyBufferAddress, 0x800, "CD writes header then CPU copies1D0", true),
            new NativeId65NormalBodyReadPhase("vram-read", CopyBufferAddress, 0x80000, "CD writes paired VRAM transfer source", true),
            new NativeId65NormalBodyReadPhase("bank-read", CopyBufferAddress, 0x5E000, "CD overwrites first bank bytes while prior VRAM tail remains", true),
            new NativeId65NormalBodyReadPhase("cpu-spu-source", CopyBufferAddress, staging.CpuRequestedByteLength, "CPU halfword source reads; hardware FIFO timing unproved", true),
            new NativeId65NormalBodyReadPhase("dma-spu-source", CopyBufferAddress, staging.DmaProgrammedSourceByteLength, "DMA programmed source reads include rounded final16", true),
            new NativeId65NormalBodyReadPhase("data-read", CopyBufferAddress, _layout.DataSectorByteLength, "CD writes complete padded data; native used end excludes padding", true),
            new NativeId65NormalBodyReadPhase("model-read", ModelRuntimeAddress, model.Length, "CD writes at native used-data end", true),
            new NativeId65NormalBodyReadPhase("scene-read", SceneRuntimeAddress, scene.Length, "CD writes after full model CD allocation", true)
        });
        PhaseOverlaps = Array.AsReadOnly(new[]
        {
            new NativeId65NormalBodyPhaseOverlap("incoming-overlay-tail", "header-and-later-copy-buffer", CopyBufferAddress,
                checked((int)(RequiredOverlayRuntimeAddress + (uint)_overlay.Length - CopyBufferAddress)),
                "Original native overwrite pattern and direct-use scan only; indirect lifetime and async completion remain unproved."),
            new NativeId65NormalBodyPhaseOverlap("data-sector-padding", "model-CD-read", ModelRuntimeAddress,
                _layout.DataSectorByteLength - _layout.DataUsedByteLength,
                "Discarded CD sector padding is not retained live data; hardware request completion remains required.")
        });
        PlanSha256 = NativeId65NormalResourceSource.Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            ProfileId, SourceImageSha256, UpstreamPlanSha256, StagingPlanSha256, LevelDataSha256,
            OverlaySha256, CopyBufferAddress, ModelRuntimeAddress, SceneRuntimeAddress,
            KnownSceneEndAddress, ResidentPeteAddress, NormalReservedLowerAddress,
            Descriptors, ModelRoots, Owners, AuxiliaryOwners, Callbacks, ReadPhases, PhaseOverlaps, Evidence,
            CallbacksInstalled, CompleteNativeDependencyClosureProven, CompleteLevelResourcesProven,
            IndependentLevelSupported, PublicationApproved, RuntimeVerified, PeakRuntimeMemoryProven,
            TransferTimingProven, SpuWrapSemanticsProven, RescueReplayRuntimeProven, PendingOwners
        }));
        // Do not append new fields to the old anonymous hash payload. Actual
        // textured instances carry their own additional source-private branch.
        if (ResourceProfile == NativeId65NormalResourceProfile.DirectLqTwoStaticMaterials)
            PlanSha256 = NativeId65NormalResourceSource.Hash(JsonSerializer.SerializeToUtf8Bytes(new
            {
                Profile = "native-id65-textured-normal-body-direct-LQ-clean-usa-v1",
                ResourceProfile, ComposedPlanSha256 = PlanSha256,
                fresh.SourceHandles, fresh.TextureEvidence
            }));
    }

    public const string ProfileId = "native-id65-fresh-normal-body-retained-Town-aux-clean-usa-v1";
    public NativeId65NormalResourceProfile ResourceProfile { get; }
    public string SourceImageSha256 => NativeId65EngineProfileContract.OriginalImageSha256;
    public string UpstreamPlanSha256 { get; }
    public string StagingPlanSha256 { get; }
    public string PlanSha256 { get; }
    public string LevelDataSha256 => _layout.LevelDataSha256;
    public string OverlaySha256 => NativeId65NormalResourceSource.Hash(_overlay);
    public int DataUsedByteLength => _layout.DataUsedByteLength;
    public int DataSectorByteLength => _layout.DataSectorByteLength;
    public uint CopyBufferAddress { get; }
    public uint ModelRuntimeAddress { get; }
    public uint SceneRuntimeAddress { get; }
    public uint KnownSceneEndAddress { get; }
    public uint ResidentPeteAddress { get; }
    public uint NormalReservedLowerAddress { get; }
    public uint RequiredInitializerRuntimeAddress => 0x8005A898;
    public uint RequiredOverlayRuntimeAddress => 0x8007AA38;
    public bool RequiresInitializerBeforeOverlay => true;
    public bool RequiresOverlayRead => true;
    public bool OverlaySkipModeSupported => false;
    public bool CheckpointOrForeignStateSupported => false;
    public bool NoOpCallbacksAuthorized => false;
    public bool SourceCertified => true;
    public bool CallbacksInstalled => false;
    public bool CompleteNativeDependencyClosureProven => false;
    public bool CompleteLevelResourcesProven => false;
    public bool IndependentLevelSupported => false;
    public bool PublicationApproved => false;
    public bool RuntimeVerified => false;
    public bool PeakRuntimeMemoryProven => false;
    public bool TransferTimingProven => false;
    public bool SpuWrapSemanticsProven => false;
    public bool RescueReplayRuntimeProven => false;
    public IReadOnlyList<NativeId65NormalBodyDescriptor> Descriptors => _layout.Descriptors;
    public IReadOnlyList<NativeId65NormalBodyRoot> ModelRoots => _layout.ModelRoots;
    public IReadOnlyList<NativeId65NormalBodyOwner> Owners { get; }
    public IReadOnlyList<NativeId65AuxiliaryBodyOwner> AuxiliaryOwners => _layout.AuxiliaryOwners;
    public IReadOnlyList<NativeId65EngineCallback> Callbacks { get; }
    public IReadOnlyList<NativeId65NormalBodyReadPhase> ReadPhases { get; }
    public IReadOnlyList<NativeId65NormalBodyPhaseOverlap> PhaseOverlaps { get; }
    public IReadOnlyList<NativeId65EngineEvidence> Evidence { get; }
    public IReadOnlyList<string> PendingOwners { get; } = Array.AsReadOnly(new[]
    {
        "Detached bytes install neither the resident initializer nor incoming overlay. Retail ID65 default leaves callbacks stale; all existing ID65 dispatch/save/music/classifier/writer gates remain separate.",
        "Town auxiliary4..7 retain complete audio/model/track allocations and padding, not Town normal terrain. Keeping bytes does not approve rescue/replay entry, selectors4+, stale linked rows or state12.",
        "Original overlay-tail and data-padding overwrites are temporal source requirements, not indirect-lifetime closure or permission to discard CD/GPU/SPU sources before completion.",
        "CPU7EFF0 and DMA7F000 at SPU1010 remain distinct. Hardware timing, SPU wrap/low16 preservation, transient rescue restore and all transfer interference remain unproved.",
        "Known scene end below normal reserved buffers is not peak fit. PETE/pools, renderer packets, dynamic Moby demand/N+1, particle states and companion0C/42 consumer closure remain pending.",
        "The issued fresh normal/no-superflame entry remains conditional; checkpoint, foreign state, power grants and all-state closure are unavailable. No normal exporter, candidate or publication is approved."
    });

    public static async Task<NativeId65NormalBodyPlan> CaptureAsync(string originalImagePath,
        NativeId65FreshSceneBundle issuedFreshScene, NativeId65NormalUploadStaging issuedStaging,
        CancellationToken cancellationToken = default)
    {
        var revision = NativeId65NormalResourceRevision.From(issuedFreshScene);
        return await CaptureRevisionAsync(originalImagePath, revision, issuedStaging, cancellationToken);
    }

    public static async Task<NativeId65NormalBodyPlan> CaptureTexturedAsync(string originalImagePath,
        NativeId65FreshTerrainTexturePlan issuedTexture, NativeId65NormalUploadStaging issuedStaging,
        CancellationToken cancellationToken = default)
    {
        var revision = NativeId65NormalResourceRevision.From(issuedTexture);
        return await CaptureRevisionAsync(originalImagePath, revision, issuedStaging, cancellationToken);
    }

    private static async Task<NativeId65NormalBodyPlan> CaptureRevisionAsync(string originalImagePath,
        NativeId65NormalResourceRevision revision, NativeId65NormalUploadStaging issuedStaging,
        CancellationToken cancellationToken)
    {
        NativeId65NormalBodySource.RequirePair(revision, issuedStaging);
        cancellationToken.ThrowIfCancellationRequested();
        var source = await NativeId65NormalBodySource.CaptureAsync(originalImagePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var result = new NativeId65NormalBodyPlan(source, revision, issuedStaging);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    public byte[] CopyLevelData() => _layout.CopyLevelData();
    public byte[] CopyEngineOverlay() => _overlay.ToArray();

    public void RequireExactPayloads(ReadOnlySpan<byte> levelData, ReadOnlySpan<byte> overlay)
    {
        _layout.RequireExactLevelData(levelData);
        if (!overlay.SequenceEqual(_overlay))
            throw new InvalidDataException("Exact retained engine overlay changed, including its originally overwritten tail.");
    }
}
