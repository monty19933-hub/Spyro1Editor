using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Spyro.Editor.Core.Music;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshDiscSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Bounded arithmetic only, never source or archive ownership.</summary>
internal sealed class NativeId65FreshDiscGeometry
{
    private NativeId65FreshDiscGeometry(int length) => BodyByteLength = length;
    internal bool SourceCertified => false;
    internal int OverlayWadOffset => 0x6927000;
    internal int OverlayByteLength => 0xF800;
    internal int BodyWadOffset => 0x6936800;
    internal int BodyByteLength { get; }
    internal int GrowthByteLength => checked(OverlayByteLength + BodyByteLength);
    internal int FinalWadByteLength => checked(OverlayWadOffset + GrowthByteLength);
    internal int FinalExecutableLba => checked(53875 + GrowthByteLength / 2048);
    internal int FinalExecutableEndExclusive => checked(FinalExecutableLba + 204);

    internal static NativeId65FreshDiscGeometry Compile(int bodyByteLength)
    {
        if (bodyByteLength is not (0x1FB000 or 0x1FB800))
            throw new InvalidDataException("Fresh-disc geometry supports only the two exact issued native body lengths.");
        return new(bodyByteLength);
    }

    internal NativeSkyWadRelocationPlan CreateGrowthGeometry() => new(
        true, 37, 0x6927000, FinalWadByteLength, GrowthByteLength, 53875, FinalExecutableLba,
        0x66000, 60000, checked((60000 - 54079) * 2048),
        new Dictionary<int, int>(), new Dictionary<int, long>(), Array.Empty<NativeSkyRelocatedPatch>());
}

public sealed record NativeId65FreshDiscRegion(string Id, long? OriginalLogicalOffset,
    long FinalLogicalOffset, long ByteLength);
public sealed record NativeId65FreshDiscVerification(string OutputSha256, int ChangedSectors);

/// <summary>
/// Original-source physical afterimages for an issued fresh body, exact retained
/// engine, bootstrap/music/LAB EXE and the actual reserved WAD pair. No output
/// path, CUE, writer, callback safety declaration or publication mode exists.
/// </summary>
public sealed class NativeId65FreshDiscPlan
{
    public const string ProfileId = "native-id65-fresh-normal-disc-LAB-source-only-usa-v1";
    public const string AcceptedBodyResourceProfile = "fresh-untextured-G3-two-triangle-data-8C8-v1";
    private static readonly DiscLayout Layout = new(2352, 24, 22, 2048);
    private readonly NativeId65FreshDiscSource _source;
    private readonly NativeId65FreshDiscGeometry _geometry;
    private readonly byte[] _body;
    private readonly byte[] _overlay;
    private readonly byte[] _executable;
    private readonly byte[] _wadDirectory;
    private readonly byte[] _isoRoot;

    private NativeId65FreshDiscPlan(NativeId65FreshDiscSource source,
        NativeId65NormalBodyPlan body, byte[] bodyBytes, byte[] overlay, int track)
    {
        _source = source;
        _geometry = NativeId65FreshDiscGeometry.Compile(bodyBytes.Length);
        _body = bodyBytes;
        _overlay = overlay;
        BodyPlanSha256 = body.PlanSha256;
        Executable = NativeId65FreshExecutablePlan.Compile(source.CopyExecutable(), track);
        _executable = Executable.CopyExecutable();
        _wadDirectory = source.CopyWadHeader();
        _isoRoot = source.CopyRoot();
        BinaryPrimitives.WriteInt32LittleEndian(_wadDirectory.AsSpan(79 * 8), _geometry.OverlayWadOffset);
        BinaryPrimitives.WriteInt32LittleEndian(_wadDirectory.AsSpan(79 * 8 + 4), _overlay.Length);
        BinaryPrimitives.WriteInt32LittleEndian(_wadDirectory.AsSpan(80 * 8), _geometry.BodyWadOffset);
        BinaryPrimitives.WriteInt32LittleEndian(_wadDirectory.AsSpan(80 * 8 + 4), _body.Length);
        PatchRoot("WAD.WAD", 37, _geometry.FinalWadByteLength);
        PatchRoot("SCUS_942.28", _geometry.FinalExecutableLba, 0x66000);
        string acceptedBodyProfile = body.DataUsedByteLength == 0x8C8 ? AcceptedBodyResourceProfile
            : "fresh-untextured-G3-two-triangle-variable-cell-tree-v2";
        PlanSha256 = NativeId65FreshExecutablePlan.Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            ProfileId,
            Original = NativeId65FreshDiscSource.OriginalSha256,
            BodyProfile = NativeId65NormalBodyPlan.ProfileId,
            AcceptedBodyResourceProfile = acceptedBodyProfile,
            BodyPlanSha256,
            BodySha256 = NativeId65FreshExecutablePlan.Hash(_body),
            OverlaySha256 = NativeId65FreshExecutablePlan.Hash(_overlay),
            ExecutableSha256 = Executable.FinalSha256,
            RootSha256 = NativeId65FreshExecutablePlan.Hash(_isoRoot),
            WadHeaderSha256 = NativeId65FreshExecutablePlan.Hash(_wadDirectory),
            _geometry.FinalExecutableLba,
            _geometry.FinalWadByteLength
        }));

        var original = new NativeAddressSpace(NativeAddressKind.DiscLogical,
            NativeId65FreshDiscSource.OriginalSha256, NativeAddressRevision.Original,
            NativeId65FreshDiscSource.SectorCount * 2048L);
        var final = new NativeAddressSpace(NativeAddressKind.DiscLogical, PlanSha256,
            NativeAddressRevision.Final, original.ByteLength);
        AddressMap = new(original, final,
        [
            new("ISO-root-fixed-position", new(original, 22 * 2048L, 2048), new(final, 22 * 2048L, 2048)),
            new("original-WAD-fixed-position", new(original, 37 * 2048L, 0x6927000), new(final, 37 * 2048L, 0x6927000)),
            new("fresh-overlay-and-body", null, new(final, 53875 * 2048L, _geometry.GrowthByteLength)),
            new("relocated-original-EXE-with-owned-edits", new(original, 53875 * 2048L, 0x66000),
                new(final, _geometry.FinalExecutableLba * 2048L, 0x66000))
        ]);
        Regions = Array.AsReadOnly(AddressMap.Regions.Select(r => new NativeId65FreshDiscRegion(
            r.Id, r.Original?.Offset, r.Final.Offset, r.Final.ByteLength)).ToArray());
        // Maps describe placement, not equal content: the exact byte differences
        // are owned below and every undeclared sector is independently bound.
        PendingOwners = Array.AsReadOnly(body.PendingOwners.Concat(new[]
        {
            "This byte plan does not publish or execute its initializer/overlay. Normal global transition state1 only; global state12, checkpoint and foreign-state paths remain outside authority.",
            "Fixed LAB uses an exact four-byte source owner and bounded ordinary index consumers, not arbitrary strings or proof of native glyph allocation/rendering.",
            "No CUE freshness, filesystem transaction, normal exporter, save/portal travel, hardware timing or complete resource/runtime approval is supplied."
        }).ToArray());

        void PatchRoot(string name, int lba, int length)
        {
            NativeFlatIsoFile row = source.RootFiles.Single(f => f.Name == name && !f.IsDirectory);
            int at = row.RecordOffset;
            if (at < 0 || at > _isoRoot.Length - 34 ||
                BinaryPrimitives.ReadInt32LittleEndian(_isoRoot.AsSpan(at + 2)) != row.Lba ||
                BinaryPrimitives.ReadInt32BigEndian(_isoRoot.AsSpan(at + 6)) != row.Lba ||
                BinaryPrimitives.ReadInt32LittleEndian(_isoRoot.AsSpan(at + 10)) != row.Size ||
                BinaryPrimitives.ReadInt32BigEndian(_isoRoot.AsSpan(at + 14)) != row.Size)
                throw new InvalidDataException("Fresh-disc ISO record lost its exact privately parsed preimage.");
            BinaryPrimitives.WriteInt32LittleEndian(_isoRoot.AsSpan(at + 2), lba);
            BinaryPrimitives.WriteInt32BigEndian(_isoRoot.AsSpan(at + 6), lba);
            BinaryPrimitives.WriteInt32LittleEndian(_isoRoot.AsSpan(at + 10), length);
            BinaryPrimitives.WriteInt32BigEndian(_isoRoot.AsSpan(at + 14), length);
        }
    }

    public bool SourceCertified => true;
    public bool RuntimeVerified => false;
    public bool PublicationApproved => false;
    public bool CompleteNativeDependencyClosureProven => false;
    public bool CheckpointOrForeignStateSupported => false;
    public bool NormalCreateBinEnabled => false;
    public string BodyPlanSha256 { get; }
    public string PlanSha256 { get; }
    public NativeId65FreshExecutablePlan Executable { get; }
    public string ExecutableBootstrapSha256 => Executable.BootstrapSha256;
    public string ExistingMusicInputSha256 => Executable.ExistingMusicInputSha256;
    public int FinalExecutableLba => _geometry.FinalExecutableLba;
    public int FinalWadByteLength => _geometry.FinalWadByteLength;
    public IReadOnlyList<NativeId65FreshDiscRegion> Regions { get; }
    public IReadOnlyList<string> PendingOwners { get; }
    internal NativeRelocationMap AddressMap { get; }
    public byte[] CopyBody() => _body.ToArray();
    public byte[] CopyOverlay() => _overlay.ToArray();
    public byte[] CopyExecutable() => _executable.ToArray();
    public byte[] CopyWadDirectory() => _wadDirectory.ToArray();
    public byte[] CopyIsoRootDirectory() => _isoRoot.ToArray();

    public static async Task<NativeId65FreshDiscPlan> CaptureAsync(string originalBin,
        NativeId65NormalBodyPlan issuedBody, int selectedBuiltInTrack, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issuedBody);
        if (issuedBody.ResourceProfile != NativeId65NormalResourceProfile.UntexturedGouraudTwoTriangles)
            throw new InvalidDataException("Fresh-disc issuer does not accept the distinct textured normal resource revision.");
        RequireCurrentUntexturedShape(issuedBody.DataUsedByteLength, issuedBody.DataSectorByteLength,
            issuedBody.Descriptors[0].ByteLength, issuedBody.Descriptors[2].ByteLength,
            issuedBody.Descriptors[3].ByteLength);
        if (!issuedBody.SourceCertified || issuedBody.SourceImageSha256 != NativeId65FreshDiscSource.OriginalSha256 ||
            issuedBody.RequiredInitializerRuntimeAddress != 0x8005A898 || issuedBody.RequiredOverlayRuntimeAddress != 0x8007AA38 ||
            !issuedBody.RequiresInitializerBeforeOverlay || !issuedBody.RequiresOverlayRead || issuedBody.CheckpointOrForeignStateSupported ||
            issuedBody.NoOpCallbacksAuthorized || issuedBody.OverlaySkipModeSupported ||
            issuedBody.CopyBufferAddress != 0x80089ECC ||
            issuedBody.ModelRuntimeAddress != 0x80089ECCu + (uint)issuedBody.DataUsedByteLength ||
            issuedBody.SceneRuntimeAddress != issuedBody.ModelRuntimeAddress + 0xF800u || issuedBody.ResidentPeteAddress != 0x801C5BB8 ||
            issuedBody.NormalReservedLowerAddress != 0x80187BB0 ||
            issuedBody.KnownSceneEndAddress != issuedBody.SceneRuntimeAddress + issuedBody.Descriptors[3].ByteLength ||
            issuedBody.KnownSceneEndAddress > issuedBody.NormalReservedLowerAddress ||
            !issuedBody.ModelRoots.SequenceEqual(new[]
            {
                new NativeId65NormalBodyRoot(0, 0xDF800, 0, 0xCC30, 0xCC30, null),
                new NativeId65NormalBodyRoot(1, 0xEC430, 0xCC30, 0x2930, 0x2BD0, 0x78)
            }) || MusicTrackCatalog.Find(selectedBuiltInTrack)?.IsSelectable != true)
            throw new InvalidDataException("Fresh-disc input requires the exact source-issued body, conditional engine entry and selectable native track.");
        cancellationToken.ThrowIfCancellationRequested();
        byte[] body = issuedBody.CopyLevelData();
        byte[] overlay = issuedBody.CopyEngineOverlay();
        _ = NativeId65FreshDiscGeometry.Compile(body.Length);
        if (overlay.Length != 0xF800 || NativeId65FreshExecutablePlan.Hash(overlay) !=
            "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5")
            throw new InvalidDataException("Fresh-disc engine overlay lost its complete retained source allocation.");
        issuedBody.RequireExactPayloads(body, overlay);
        using FileStream image = File.OpenRead(originalBin);
        NativeId65FreshDiscSource source = await NativeId65FreshDiscSource.CaptureAsync(image, cancellationToken);
        var result = new NativeId65FreshDiscPlan(source, issuedBody, body, overlay, selectedBuiltInTrack);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    // A current private-issued body is still not a promise to accept future
    // typed resource revisions. In particular textured data may occupy the SAME
    // CD sector/body length while changing its live used-end and model anchor.
    // This local discriminator plus exact shape is deliberately untextured only;
    // a future body instance revision must remain an explicit issuer guard.
    // Canonical untextured tree lengths move the live anchors but not the CD
    // allocation. The actual private upstream validates the complete grammar.
    internal static void RequireCurrentUntexturedShape(int dataUsed, int dataSector,
        int upload, int model, int scene)
    {
        NativeId65NormalDataLayout.RequireBoundedUsedLength(dataUsed);
        if (dataSector != 0x1000 || upload != 0xDE000 || model != 0xF800 ||
            scene is not (0x2800 or 0x3000))
            throw new InvalidDataException("Fresh-disc authority is restricted to the exact current untextured resource profile.");
    }

    internal byte[] CreateExpectedRawSector(int lba, ReadOnlySpan<byte> sourceSector)
    {
        if (sourceSector.Length != 2352)
            throw new InvalidDataException("Fresh-disc afterimage requires one exact physical source sector.");
        byte[] before = sourceSector.ToArray();
        _source.RequireRawSector(lba, before); // BEFORE either changed or untouched branch
        if (!IsChangedOwner(lba))
            return before;
        byte[] after = lba >= 54079
            ? RawMode2Form1SectorIntegrity.PrepareOwnedZeroPaddingSector(before, Layout, lba)
            : before.ToArray();
        RawMode2Form1SectorIntegrity.RequireLogicalWriteSector(after, Layout, lba);
        if (lba == 22)
            _isoRoot.CopyTo(after, 24);
        else if (lba == 37)
            _wadDirectory.CopyTo(after, 24);
        else if (lba >= 53875 && lba < _geometry.FinalExecutableLba)
        {
            int offset = checked((lba - 53875) * 2048);
            if (offset < _overlay.Length)
                _overlay.AsSpan(offset, 2048).CopyTo(after.AsSpan(24, 2048));
            else
                _body.AsSpan(offset - _overlay.Length, 2048).CopyTo(after.AsSpan(24, 2048));
        }
        else if (lba >= _geometry.FinalExecutableLba)
            _executable.AsSpan((lba - _geometry.FinalExecutableLba) * 2048, 2048).CopyTo(after.AsSpan(24, 2048));
        // Root's original terminal flags remain; WAD/EXE end flags move exactly.
        int flags = lba == 22 ? before[18] & 0x81 :
            lba == _geometry.FinalExecutableLba - 1 || lba == _geometry.FinalExecutableEndExclusive - 1 ? 0x81 : 0;
        after[18] = after[22] = (byte)((after[18] & ~0x81) | flags);
        RawMode2Form1SectorIntegrity.RebuildLogicalWriteSector(after, Layout, lba);
        return after;
    }

    internal byte[] CreateOriginalRawSector(int lba, ReadOnlySpan<byte> finalSector)
    {
        if (lba < 0 || lba >= NativeId65FreshDiscSource.SectorCount || finalSector.Length != 2352)
            throw new InvalidDataException("Fresh-disc inverse sector is out of bounds.");
        byte[] final = finalSector.ToArray();
        if (!IsChangedOwner(lba))
        {
            _source.RequireRawSector(lba, final);
            return final;
        }
        byte[] original = _source.CopyPreimage(lba);
        if (!final.SequenceEqual(CreateExpectedRawSector(lba, original)))
            throw new InvalidDataException("Fresh-disc inverse requires the complete exact final raw afterimage.");
        return original;
    }

    public NativeId65FreshDiscVerification VerifyImage(Stream source, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(output);
        cancellationToken.ThrowIfCancellationRequested();
        var result = NativeTownSquareClosurePhysicalReadback.Verify(source, output,
            NativeId65FreshDiscSource.ImageByteLength, NativeId65FreshDiscSource.OriginalSha256,
            (lba, before) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return CreateExpectedRawSector(lba, before);
            });
        // The shared verifier checks lengths/positions/digests, not actual EOF
        // or cancellation. Probe the caller's real streams, not virtual bounds.
        NativeId65FreshDiscSource.RequireActualEof(source, cancellationToken);
        NativeId65FreshDiscSource.RequireActualEof(output, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(result.Hash, result.Changed);
    }

    private bool IsChangedOwner(int lba) => lba is 22 or 37 ||
        lba >= 53874 && lba < _geometry.FinalExecutableEndExclusive;
}
