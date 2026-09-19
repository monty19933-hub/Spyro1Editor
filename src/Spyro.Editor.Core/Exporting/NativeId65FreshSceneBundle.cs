using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeId65SceneSpawn(int X, int Y, int Z, int Heading);
public sealed record NativeId65FreshSceneOwner(string Id, int Offset, int ByteLength, string Sha256, string Provenance);
public sealed record NativeId65SceneRuntimeAnchor(string Id, uint NativeGlobalAddress,
    int SceneRelativeOffset, int NativePointerWidthBytes, bool IsOnePastArena);

/// <summary>
/// Neutral fixed scene grammar. Its nominal N slots are authored storage, not
/// measured runtime demand; the native allocator does not refuse allocation N+1.
/// </summary>
public sealed class NativeId65FreshSceneLayout
{
    private readonly byte[] _scene;

    private NativeId65FreshSceneLayout(NativeId65SceneSpawn spawn, int slots,
        ReadOnlySpan<byte> tiledefs, ReadOnlySpan<byte> lists)
    {
        Spawn = spawn;
        NativeNominalCapacity = slots;
        DynamicArenaByteLength = checked(0x70 * slots + 0x58);
        int properties = DynamicArenaOffset + DynamicArenaByteLength;
        int pods = properties + 4;
        int buckets = pods + 8;
        BucketOffset = buckets + 4;
        int fixups = BucketOffset + BucketByteLength;
        int unpadded = fixups + 4;
        _scene = new byte[(unpadded + 0x7FF) & ~0x7FF];
        Put(0, spawn.X);
        Put(4, spawn.Y);
        Put(8, spawn.Z);
        _scene[14] = (byte)spawn.Heading;
        tiledefs.CopyTo(_scene.AsSpan(0x10, 120));
        lists.CopyTo(_scene.AsSpan(0x88, 0x44));
        Put(MobyComponentOffset, 8 + DynamicArenaByteLength);
        Put(properties, 4);
        Put(pods, 8);
        Put(buckets, 4 + BucketByteLength);
        var owners = new List<NativeId65FreshSceneOwner>();
        Add("authored-spawn-and-explicit-reserved-bytes", 0, 16, "fresh-authored-native-widths");
        Add("header-presentation-Tiledefs", 0x10, 120, "passed-bytes-not-neutral-source-authority");
        Add("paired-seven-zero-lists-and-three-guards", 0x88, 0x44, "canonical-paired-prefix");
        Add("Moby-length-and-zero-placed-count", MobyComponentOffset, 8, "fresh-native-length-count");
        Add("dynamic-rows-properties-and-complete-next-row-reserve", DynamicArenaOffset, DynamicArenaByteLength, "fresh-zero-dynamic-storage");
        Add("empty-placed-properties", properties, 4, "fresh-length-only-component");
        Add("empty-numeric-pod-list", pods, 8, "fresh-length-and-zero-count");
        Add("spatial-bucket-component-length", buckets, 4, "fresh-native-length");
        Add("two-complete-spatial-Moby-bucket-banks", BucketOffset, BucketByteLength, "fresh-zero-bucket-pointers");
        Add("zero-scene-pointer-fixup-count", fixups, 4, "fresh-raw-count");
        Add("sector-padding", unpadded, _scene.Length - unpadded, "fresh-explicit-padding");
        Owners = owners.AsReadOnly();
        RuntimeAnchors = Array.AsReadOnly(new[]
        {
            new NativeId65SceneRuntimeAnchor("placed-row-base", 0x80075828, DynamicArenaOffset, 4, false),
            new NativeId65SceneRuntimeAnchor("dynamic-row-base", 0x80075890, DynamicArenaOffset, 4, false),
            new NativeId65SceneRuntimeAnchor("dynamic-row-cursor", 0x8007573C, DynamicArenaOffset, 4, false),
            new NativeId65SceneRuntimeAnchor("dynamic-properties-end", 0x800756E8, properties, 4, true),
            new NativeId65SceneRuntimeAnchor("dynamic-properties-cursor", 0x80075930, properties, 4, true),
            new NativeId65SceneRuntimeAnchor("two-spatial-bucket-banks", 0x80075778, BucketOffset, 4, false)
        });
        int cursor = 0;
        foreach (var owner in Owners)
        {
            if (owner.Offset != cursor || owner.ByteLength <= 0)
                throw new InvalidDataException("Fresh scene lost a contiguous nonempty byte owner.");
            cursor = checked(cursor + owner.ByteLength);
        }
        if (cursor != _scene.Length || DynamicArenaByteLength / 0x70 != slots)
            throw new InvalidDataException("Fresh scene storage and native nominal capacity disagree.");

        void Put(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(_scene.AsSpan(offset), value);
        void Add(string id, int offset, int length, string provenance) => owners.Add(
            new(id, offset, length, NativeId65NormalResourceSource.Hash(_scene.AsSpan(offset, length)), provenance));
    }

    public NativeId65SceneSpawn Spawn { get; }
    public int DynamicArenaByteLength { get; }
    public int NativeNominalCapacity { get; }
    public int MobyComponentOffset => 0xCC;
    public int DynamicArenaOffset => 0xD4;
    public int BucketOffset { get; }
    public int BucketByteLength => 0x2000;
    public int PlacedCount => 0;
    public int FixupCount => 0;
    public bool SourceCertified => false;
    public bool RuntimeVerified => false;
    public bool NativeArenaExhaustionSafe => false;
    public IReadOnlyList<NativeId65FreshSceneOwner> Owners { get; }
    public IReadOnlyList<NativeId65SceneRuntimeAnchor> RuntimeAnchors { get; }
    public byte[] CopyScene() => _scene.ToArray();

    public void RequireExactScene(ReadOnlySpan<byte> bytes)
    {
        if (!bytes.SequenceEqual(_scene))
            throw new InvalidDataException("Fresh scene bytes changed, including an owner, guard, arena or padding complement.");
    }

    internal static NativeId65FreshSceneLayout Compile(NativeId65SceneSpawn spawn, int slots,
        ReadOnlySpan<byte> tiledefs, ReadOnlySpan<byte> lists)
    {
        ValidateAuthoredInput(spawn, slots);
        if (tiledefs.Length != 120)
            throw new InvalidDataException("Fresh scene requires all fifteen eight-byte header descriptors.");
        NativeId65ZeroSceneAnimationLists.ValidateCanonicalBytes(lists);
        return new(spawn, slots, tiledefs, lists);
    }

    internal static void ValidateAuthoredInput(NativeId65SceneSpawn spawn, int slots)
    {
        ArgumentNullException.ThrowIfNull(spawn);
        if (slots < 1 || slots > 32 || spawn.X < 0 || spawn.X > 262143 ||
            spawn.Y < 0 || spawn.Y > 262143 || spawn.Z < 0 || spawn.Z > 262143 ||
            spawn.Heading < 0 || spawn.Heading > 255)
            throw new InvalidDataException("Unsupported authored scene coordinate, heading or dynamic-slot budget.");
    }

    internal static NativeId65FreshSceneLayout DecodeCanonical(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is not (0x2800 or 0x3000))
            throw new InvalidDataException("Fresh scene exceeds the fixed canonical byte budget.");
        int arenaLength = BinaryPrimitives.ReadInt32LittleEndian(bytes[0xCC..]) - 8;
        if (arenaLength < 0xC8 || arenaLength > 0xE58 || (arenaLength - 0x58) % 0x70 != 0)
            throw new InvalidDataException("Fresh scene arena does not encode N complete native pairs plus a row reserve.");
        var spawn = new NativeId65SceneSpawn(BinaryPrimitives.ReadInt32LittleEndian(bytes),
            BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]), BinaryPrimitives.ReadInt32LittleEndian(bytes[8..]), bytes[14]);
        var decoded = Compile(spawn, (arenaLength - 0x58) / 0x70, bytes.Slice(0x10, 120), bytes.Slice(0x88, 0x44));
        decoded.RequireExactScene(bytes);
        return decoded;
    }
}

/// <summary>
/// Actual fresh scene plus an expanded, paired presentation upload. Data/model
/// outputs remain the issued chain's exact bytes. This is not a native level
/// header, callback installation, complete engine, RAM approval or disc plan.
/// </summary>
public sealed class NativeId65FreshSceneBundle
{
    private readonly NativeId65NormalResourceChain _chain;
    private readonly byte[] _upload;

    private NativeId65FreshSceneBundle(NativeId65FreshSceneSource source, NativeId65NormalResourceChain chain,
        NativeId65SceneSpawn spawn, int dynamicSlotBudget)
    {
        if (chain.Resources.SurfaceRevision.TiledSurface is { } tiled)
        {
            RequireTiledChain(chain);
            var document = tiled.Document;
            if (dynamicSlotBudget != 16 || spawn != new NativeId65SceneSpawn(document.SpawnX,
                document.SpawnY, document.SpawnZ, document.Heading))
                throw new InvalidDataException("Tiled scene must preserve the actual document spawn/heading and fixed16-slot budget.");
        }
        _chain = chain;
        byte[] executable = source.CopyExecutable();
        chain.SceneLists.RequireSourceExecutable(executable);
        if (!chain.Resources.SurfaceRevision.CopySourceExecutable().AsSpan().SequenceEqual(executable))
            throw new InvalidDataException("Fresh scene and issued resources belong to different native source captures.");
        HeaderTextures = source.HeaderTextures;
        EntryProfile = source.EntryProfile;
        Evidence = source.Evidence;
        SceneLayout = NativeId65FreshSceneLayout.Compile(spawn, dynamicSlotBudget,
            HeaderTextures.CopyTiledefs(), chain.CopySceneAnimationComponents());
        byte[] scene = SceneLayout.CopyScene();
        NativeId65FreshSceneLayout.DecodeCanonical(scene).RequireExactScene(scene);
        chain.RequireExactPairedBytes(chain.CopyDataComponents(), scene.AsSpan(0x88, 0x44));
        byte[] prior = chain.CopyUploadPayload();
        var atlas = new NativeId65VramAssembly(source.CopyAtlas());
        foreach (var owner in chain.Resources.Owners.Where(o => o.Domain == NativeId65ResourceDomain.UploadPayload && o.Offset < 0x80000))
        {
            if (owner.Offset < 0 || owner.ByteLength <= 0 || owner.Offset + (long)owner.ByteLength > 0x80000)
                throw new InvalidDataException("An issued upstream VRAM owner escaped its fixed source domain.");
            switch (owner.Provenance)
            {
                case "fresh-zero-upload-background":
                    if (prior.AsSpan(owner.Offset, owner.ByteLength).ContainsAnyExcept((byte)0))
                        throw new InvalidDataException("Fresh upstream upload background was not zero.");
                    break;
                case "retained-fixed-Town-VRAM":
                    if (owner.SourceWadOffset != 0x119E800L + owner.Offset)
                        throw new InvalidDataException("An upstream source texture address cannot be rebound by matching bytes.");
                    atlas.CopySourceRange(owner.Offset, owner.ByteLength);
                    break;
                case "issued-detached-companion-VRAM":
                    atlas.CopyDetachedRange(owner.Id, owner.Offset, prior.AsSpan(owner.Offset, owner.ByteLength));
                    break;
                default:
                    throw new InvalidDataException("Unknown upstream VRAM ownership profile.");
            }
        }
        foreach (var footprint in HeaderTextures.Footprints)
        {
            foreach (var row in footprint.PixelRows)
                atlas.CopySourceRange(row.VramRelativeOffset, row.ByteLength);
            foreach (var row in footprint.PaletteRows)
                atlas.CopySourceRange(row.VramRelativeOffset, row.ByteLength);
        }
        _upload = prior.ToArray();
        atlas.CopyBytes().CopyTo(_upload, 0);
        foreach (var owner in chain.Resources.Owners.Where(o => o.Domain == NativeId65ResourceDomain.UploadPayload &&
            o.Provenance != "fresh-zero-upload-background"))
        {
            if (!_upload.AsSpan(owner.Offset, owner.ByteLength).SequenceEqual(prior.AsSpan(owner.Offset, owner.ByteLength)))
                throw new InvalidDataException("Expanded presentation upload changed an existing protected source/detached/audio owner.");
        }
        SourceVramOwnedByteCount = atlas.SourceOwnedByteCount;
        var uploadOwners = new List<NativeId65NormalResourceOwner>();
        byte[] ownership = atlas.CopyOwnership();
        for (int start = 0; start < ownership.Length;)
        {
            int end = start + 1;
            while (end < ownership.Length && ownership[end] == ownership[start]) end++;
            string provenance = ownership[start] switch
            {
                0 => "fresh-zero-upload-background",
                1 => "retained-fixed-Town-presentation-VRAM",
                _ => "issued-detached-companion-VRAM"
            };
            uploadOwners.Add(new("fresh-scene-vram-" + start.ToString("X"), NativeId65ResourceDomain.UploadPayload,
                start, end - start, NativeId65NormalResourceSource.Hash(_upload.AsSpan(start, end - start)),
                provenance, ownership[start] == 1 ? 0x119E800L + start : null));
            start = end;
        }
        uploadOwners.AddRange(chain.Resources.Owners.Where(o => o.Domain == NativeId65ResourceDomain.UploadPayload && o.Offset >= 0x80000));
        UploadOwners = uploadOwners.AsReadOnly();
        SceneSha256 = NativeId65NormalResourceSource.Hash(scene);
        UploadSha256 = NativeId65NormalResourceSource.Hash(_upload);
        PlanSha256 = NativeId65NormalResourceSource.Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            ProfileId, SourceImageSha256, UpstreamPlanSha256 = chain.PlanSha256,
            EntryProfile = NativeId65FreshNormalEntryProfile.ProfileId, SceneSha256, UploadSha256,
            DataSha256, ModelSha256, SourceVramOwnedByteCount, SceneLayout.Spawn,
            SceneLayout.NativeNominalCapacity, SceneLayout.Owners, SceneLayout.RuntimeAnchors,
            UploadOwners, HeaderTextures.Footprints, HeaderTextures.UnavailableDescriptorIds, Evidence
        }));
        if (TiledSurfaceAncestor is { } tiledAncestor)
            PlanSha256 = NativeId65NormalResourceSource.Hash(JsonSerializer.SerializeToUtf8Bytes(new
            {
                Profile = "native-id65-tiled-scene-two-sector-data-clean-usa-v1",
                tiledAncestor.DocumentSha256, ComposedPlanSha256 = PlanSha256
            }));
    }

    public const string ProfileId = "native-id65-fresh-scene-presentation-assets-clean-usa-v1";
    internal NativeId65NormalResourceChain ResourceChainAncestor => _chain;
    internal NativeId65TiledSurfaceBundle? TiledSurfaceAncestor => _chain.Resources.SurfaceRevision.TiledSurface;
    public string SourceImageSha256 => _chain.SourceImageSha256;
    public NativeId65FreshSceneLayout SceneLayout { get; }
    public NativeId65SceneHeaderTextureOwners HeaderTextures { get; }
    public NativeId65FreshNormalEntryProfile EntryProfile { get; }
    public IReadOnlyList<NativeId65EngineEvidence> Evidence { get; }
    public IReadOnlyList<NativeId65NormalResourceOwner> UploadOwners { get; }
    public int SourceVramOwnedByteCount { get; }
    public string SceneSha256 { get; }
    public string UploadSha256 { get; }
    public string DataSha256 => _chain.DataSha256;
    public string ModelSha256 => _chain.Resources.ModelSha256;
    public string PlanSha256 { get; }
    public bool SourceCertified => true;
    public bool CompleteLevelResourcesProven => false;
    public bool CompleteHeaderTextureConsumerClosureProven => false;
    public bool CompletePlayerParticleClosureProven => false;
    public bool IndependentLevelSupported => false;
    public bool PublicationApproved => false;
    public bool RuntimeVerified => false;
    public IReadOnlyList<string> PendingOwners { get; } = Array.AsReadOnly(new[]
    {
        "This fresh-normal/no-superflame profile excludes checkpoint, foreign state and power-granting behavior; Tiledef13 remains unavailable with no VRAM0 owner.",
        "Native allocator N+1 is unsafe, and N authored slots do not prove runtime demand or safe exhaustion. Future dynamic coordinates must retain valid native bucket indexing.",
        "Full retained header pages and fade CLUTs are shared source presentation, not donor terrain or a measured GTE/GPU envelope; complete player/particle state closure remains unproved.",
        "All eight engine callbacks, companion behavior/effect closure, fixed-address PETE/pool owners, transient/GPU RAM and native entry still need separate integration.",
        "Native resource descriptors, auxiliary/rescue routes, ID65 save/music/dispatch linkage and publication are not emitted or approved; complete Town audio remains retained."
    });

    public static async Task<NativeId65FreshSceneBundle> CaptureAsync(string originalImagePath,
        NativeId65NormalResourceChain issuedChain, NativeId65SceneSpawn spawn, int dynamicSlotBudget,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issuedChain);
        RequireLegacySurface(issuedChain);
        NativeId65FreshSceneLayout.ValidateAuthoredInput(spawn, dynamicSlotBudget);
        NativeId65FreshSceneSource source = await NativeId65FreshSceneSource.CaptureAsync(originalImagePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(source, issuedChain, spawn, dynamicSlotBudget);
    }

    private static void RequireLegacySurface(NativeId65NormalResourceChain chain)
    {
        if (chain.Resources.SurfaceRevision.TiledSurface != null)
            throw new InvalidDataException("The legacy fresh-scene profile does not accept tiled source ancestry.");
    }

    internal void RequireLegacyResourceProfile() => RequireLegacySurface(_chain);

    internal static async Task<NativeId65FreshSceneBundle> CaptureTiledAsync(string originalImagePath,
        NativeId65NormalResourceChain issuedChain, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tiled = RequireTiledChain(issuedChain);
        var document = tiled.Document;
        var source = await NativeId65FreshSceneSource.CaptureAsync(originalImagePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(source, issuedChain, new(document.SpawnX, document.SpawnY, document.SpawnZ, document.Heading), 16);
    }

    private static NativeId65TiledSurfaceBundle RequireTiledChain(NativeId65NormalResourceChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        var tiled = chain.Resources.SurfaceRevision.TiledSurface ??
            throw new InvalidDataException("Tiled scene requires actual source-issued tiled document ancestry.");
        var decoded = NativeId65TiledNormalDataLayout.DecodeForFixedBody(chain.CopyDataComponents());
        tiled.RequireExactBundle(decoded.Surface.CopyBundle());
        return tiled;
    }

    public byte[] CopyScene() => SceneLayout.CopyScene();
    public byte[] CopyUploadPayload() => _upload.ToArray();
    public byte[] CopyDataComponents() => _chain.CopyDataComponents();
    public byte[] CopyModelPayload() => _chain.CopyModelPayload();

    public void RequireExactOutputs(ReadOnlySpan<byte> scene, ReadOnlySpan<byte> upload)
    {
        SceneLayout.RequireExactScene(scene);
        if (!upload.SequenceEqual(_upload))
            throw new InvalidDataException("Expanded presentation upload changed an owner or its complete complement.");
    }
}
