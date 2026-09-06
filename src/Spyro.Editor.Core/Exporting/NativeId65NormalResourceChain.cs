using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeId65DataComponentOwner(string Id, int Offset, int ByteLength,
    string Sha256, string Provenance, long? SourceWadOffset);

/// <summary>
/// An actual data-component chain paired with its mandatory seven empty scene
/// lists. It does not place those lists into a complete scene or issue a native
/// level header, runtime allocation, engine profile, or publication permission.
/// </summary>
public sealed class NativeId65NormalResourceChain
{
    private readonly NativeId65NormalResourceBundle _resources;
    private readonly byte[] _data, _sceneLists;

    private NativeId65NormalResourceChain(NativeId65NormalResourceBundle resources,
        NativeId65ZeroSceneAnimationLists lists)
    {
        _resources = resources;
        SceneLists = lists;
        ScenePlacementRequirement = lists.PlacementRequirement;
        _sceneLists = lists.CopyComponents();
        byte[] tail = resources.CopyDataComponentTail();
        _data = new byte[checked(8 + tail.Length)];
        BinaryPrimitives.WriteInt32LittleEndian(_data, 8);
        // Count is zero. The source-issued FF Gouraud surface has no texture
        // metadata indices, and the paired lists prevent animation consumers
        // from dereferencing or rewriting the absent texture records.
        tail.CopyTo(_data, 8);
        var owners = new List<NativeId65DataComponentOwner>
        {
            new("fresh-zero-texture-metadata", 0, 8, Hash(_data.AsSpan(0, 8)),
                NativeId65ZeroSceneAnimationLists.ProfileId + ";paired-zero-scene-lists-required", null)
        };
        foreach (var owner in resources.Owners.Where(o => o.Domain == NativeId65ResourceDomain.DataComponentTail))
            owners.Add(new(owner.Id, checked(owner.Offset + 8), owner.ByteLength,
                owner.Sha256, owner.Provenance, owner.SourceWadOffset));
        DataOwners = owners.AsReadOnly();
        int cursor = 0;
        foreach (var owner in DataOwners)
        {
            if (owner.Offset != cursor || owner.ByteLength <= 0 ||
                owner.Offset + (long)owner.ByteLength > _data.Length ||
                Hash(_data.AsSpan(owner.Offset, owner.ByteLength)) != owner.Sha256)
                throw new InvalidDataException("Paired data components lost an exact contiguous byte owner.");
            cursor = checked(cursor + owner.ByteLength);
        }
        if (cursor != _data.Length || !_data.AsSpan(8).SequenceEqual(tail))
            throw new InvalidDataException("Paired data components lost their complete inherited tail complement.");
        DataSha256 = Hash(_data);
        SceneComponentsSha256 = Hash(_sceneLists);
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            ProfileId, SourceImageSha256, resources.UploadSha256, resources.ModelSha256,
            resources.DataTailSha256, resources.CompanionPlanSha256, resources.Surface.BundleSha256,
            DataSha256, SceneComponentsSha256, ScenePlacementRequirement, DataOwners,
            SceneOwners = lists.Lists, SceneConsumerEvidence = lists.Evidence
        }));
    }

    public const string ProfileId = "native-id65-paired-normal-resource-chain-clean-usa-v1";
    public string SourceImageSha256 => _resources.SourceImageSha256;
    internal NativeId65NormalResourceBundle Resources => _resources;
    public NativeId65ZeroSceneAnimationLists SceneLists { get; }
    public NativeId65SceneListPlacementRequirement ScenePlacementRequirement { get; }
    public IReadOnlyList<NativeId65DataComponentOwner> DataOwners { get; }
    public string DataSha256 { get; }
    public string SceneComponentsSha256 { get; }
    public string PlanSha256 { get; }
    public bool DataComponentsEncoded => true;
    public bool SceneListBytesEncoded => true;
    public bool CompleteLevelResourcesProven => false;
    public bool SceneInitialized => false;
    public bool PublicationApproved => false;
    public bool RuntimeVerified => false;
    public IReadOnlyList<string> PendingOwners { get; } = Array.AsReadOnly(new[]
    {
        "Place the exact paired list prefix at scene+0x88 after a separately source-issued native scene header, before the remaining scene components.",
        "Fresh scene actors/properties/arenas, callbacks, companion effect/state closure, full player/particle consumers and GPU/transient RAM remain unissued.",
        "Shared fixed-address PETE/player geometry and particle-pool boundary ownership remain external native dependencies.",
        "Native resource/header/mode/rescue/transition/ID65 linkage and publication remain separate unapproved stages; complete Town audio is explicitly retained."
    });

    public static NativeId65NormalResourceChain Compile(NativeId65NormalResourceBundle resources,
        NativeId65ZeroSceneAnimationLists lists)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(lists);
        if (!resources.SourceCertified || !lists.SourceCertified ||
            resources.SourceImageSha256 != lists.SourceImageSha256)
            throw new InvalidDataException("Paired data requires both private source-issued resource and scene-list captures.");
        lists.RequireSourceExecutable(resources.Surface.CopySourceExecutable());
        NativeId65ZeroSceneAnimationLists.ValidateCanonicalBytes(lists.CopyComponents());
        return new(resources, lists);
    }

    public byte[] CopyDataComponents() => _data.ToArray();
    public byte[] CopySceneAnimationComponents() => _sceneLists.ToArray();
    public byte[] CopyUploadPayload() => _resources.CopyUploadPayload();
    public byte[] CopyModelPayload() => _resources.CopyModelPayload();

    public void RequireExactPairedBytes(ReadOnlySpan<byte> dataComponents, ReadOnlySpan<byte> sceneLists)
    {
        if (!dataComponents.SequenceEqual(_data) || !sceneLists.SequenceEqual(_sceneLists))
            throw new InvalidDataException("A paired data/list byte owner, guard, length, or complete complement changed.");
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
